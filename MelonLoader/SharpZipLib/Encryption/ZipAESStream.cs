using System;
using System.IO;
using System.Security.Cryptography;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using MelonLoader.ICSharpCode.SharpZipLib.Zip;

namespace MelonLoader.ICSharpCode.SharpZipLib.Encryption
{
	internal class ZipAESStream : CryptoStream
	{
		public ZipAESStream(Stream stream, ZipAESTransform transform, CryptoStreamMode mode)
			: base(stream, transform, mode)
		{
			_stream = stream;
			_transform = transform;
			_slideBuffer = new byte[1024];

			if (mode != CryptoStreamMode.Read)
			{
				throw new Exception("ZipAESStream only for read");
			}
		}

		private const int AUTH_CODE_LENGTH = 10;

		private const int CRYPTO_BLOCK_SIZE = 16;

		private const int BLOCK_AND_AUTH = CRYPTO_BLOCK_SIZE + AUTH_CODE_LENGTH;

		private Stream _stream;
		private ZipAESTransform _transform;
		private byte[] _slideBuffer;
		private int _slideBufStartPos;
		private int _slideBufFreePos;

		private byte[] _transformBuffer = null;// new byte[CRYPTO_BLOCK_SIZE];
		private int _transformBufferFreePos;
		private int _transformBufferStartPos;

		private bool HasBufferedData =>_transformBuffer != null && _transformBufferStartPos < _transformBufferFreePos;

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (count == 0)
				return 0;

			int nBytes = 0;
			if (HasBufferedData)
			{
				nBytes = ReadBufferedData(buffer, offset, count);

				if (nBytes == count)
					return nBytes;

				offset += nBytes;
				count -= nBytes;
			}

			if (_slideBuffer != null)
				nBytes += ReadAndTransform(buffer, offset, count);

			return nBytes;
		}

		/*
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			var readCount = Read(buffer, offset, count);
			return Task.FromResult(readCount);
		}
		*/

		private int ReadAndTransform(byte[] buffer, int offset, int count)
		{
			int nBytes = 0;
			while (nBytes < count)
			{
				int bytesLeftToRead = count - nBytes;

				int byteCount = _slideBufFreePos - _slideBufStartPos;

				int lengthToRead = BLOCK_AND_AUTH - byteCount;
				if (_slideBuffer.Length - _slideBufFreePos < lengthToRead)
				{
					int iTo = 0;
					for (int iFrom = _slideBufStartPos; iFrom < _slideBufFreePos; iFrom++, iTo++)
					{
						_slideBuffer[iTo] = _slideBuffer[iFrom];
					}
					_slideBufFreePos -= _slideBufStartPos;      // Note the -=
					_slideBufStartPos = 0;
				}
				int obtained = StreamUtils.ReadRequestedBytes(_stream, _slideBuffer, _slideBufFreePos, lengthToRead);
				_slideBufFreePos += obtained;

				byteCount = _slideBufFreePos - _slideBufStartPos;
				if (byteCount >= BLOCK_AND_AUTH)
				{
					var read = TransformAndBufferBlock(buffer, offset, bytesLeftToRead, CRYPTO_BLOCK_SIZE);
					nBytes += read;
					offset += read;
				}
				else
				{
					if (byteCount > AUTH_CODE_LENGTH)
					{
						int finalBlock = byteCount - AUTH_CODE_LENGTH;
						nBytes += TransformAndBufferBlock(buffer, offset, bytesLeftToRead, finalBlock);
					}
					else if (byteCount < AUTH_CODE_LENGTH)
						throw new ZipException("Internal error missed auth code"); // Coding bug
					byte[] calcAuthCode = _transform.GetAuthCode();
					for (int i = 0; i < AUTH_CODE_LENGTH; i++)
					{
						if (calcAuthCode[i] != _slideBuffer[_slideBufStartPos + i])
						{
							throw new ZipException("AES Authentication Code does not match. This is a super-CRC check on the data in the file after compression and encryption. \r\n"
								+ "The file may be damaged.");
						}
					}

					_slideBuffer = null;

					break;  // Reached the auth code
				}
			}
			return nBytes;
		}

		private int ReadBufferedData(byte[] buffer, int offset, int count)
		{
			int copyCount = Math.Min(count, _transformBufferFreePos - _transformBufferStartPos);

			Array.Copy(_transformBuffer, _transformBufferStartPos, buffer, offset, copyCount);
			_transformBufferStartPos += copyCount;

			return copyCount;
		}

		private int TransformAndBufferBlock(byte[] buffer, int offset, int count, int blockSize)
		{
			bool bufferRequired = (blockSize > count);

			if (bufferRequired && _transformBuffer == null)
				_transformBuffer = new byte[CRYPTO_BLOCK_SIZE];

			var targetBuffer = bufferRequired ? _transformBuffer : buffer;
			var targetOffset = bufferRequired ? 0 : offset;

			_transform.TransformBlock(_slideBuffer,
									  _slideBufStartPos,
									  blockSize,
									  targetBuffer,
									  targetOffset);

			_slideBufStartPos += blockSize;

			if (!bufferRequired)
			{
				return blockSize;
			}
			else
			{
				Array.Copy(_transformBuffer, 0, buffer, offset, count);
				_transformBufferStartPos = count;
				_transformBufferFreePos = blockSize;

				return count;
			}
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}
	}
}
