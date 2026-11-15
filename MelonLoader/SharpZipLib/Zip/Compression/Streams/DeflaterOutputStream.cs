using MelonLoader.ICSharpCode.SharpZipLib.Encryption;
using System;
using System.IO;
using System.Security.Cryptography;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Streams
{
	public class DeflaterOutputStream : Stream
	{
		#region Constructors

		public DeflaterOutputStream(Stream baseOutputStream)
			: this(baseOutputStream, new Deflater(), 512)
		{
		}

		public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater)
			: this(baseOutputStream, deflater, 512)
		{
		}

		public DeflaterOutputStream(Stream baseOutputStream, Deflater deflater, int bufferSize)
		{
			if (baseOutputStream == null)
			{
				throw new ArgumentNullException(nameof(baseOutputStream));
			}

			if (baseOutputStream.CanWrite == false)
			{
				throw new ArgumentException("Must support writing", nameof(baseOutputStream));
			}

			if (bufferSize < 512)
			{
				throw new ArgumentOutOfRangeException(nameof(bufferSize));
			}

			baseOutputStream_ = baseOutputStream;
			buffer_ = new byte[bufferSize];
			deflater_ = deflater ?? throw new ArgumentNullException(nameof(deflater));
		}

		#endregion Constructors

		#region Public API

		public virtual void Finish()
		{
			deflater_.Finish();
			while (!deflater_.IsFinished)
			{
				int len = deflater_.Deflate(buffer_, 0, buffer_.Length);
				if (len <= 0)
				{
					break;
				}

				if (cryptoTransform_ != null)
				{
					EncryptBlock(buffer_, 0, len);
				}

				baseOutputStream_.Write(buffer_, 0, len);
			}

			if (!deflater_.IsFinished)
			{
				throw new SharpZipBaseException("Can't deflate all input?");
			}

			baseOutputStream_.Flush();

			if (cryptoTransform_ != null)
			{
				if (cryptoTransform_ is ZipAESTransform)
				{
					AESAuthCode = ((ZipAESTransform)cryptoTransform_).GetAuthCode();
				}
				cryptoTransform_.Dispose();
				cryptoTransform_ = null;
			}
		}

		public bool IsStreamOwner { get; set; } = true;

		public bool CanPatchEntries
		{
			get
			{
				return baseOutputStream_.CanSeek;
			}
		}

		#endregion Public API

		#region Encryption

		protected ICryptoTransform cryptoTransform_;

		protected byte[] AESAuthCode;

		protected void EncryptBlock(byte[] buffer, int offset, int length)
		{
			cryptoTransform_.TransformBlock(buffer, 0, length, buffer, 0);
		}

		#endregion Encryption

		#region Deflation Support

		protected void Deflate()
		{
			Deflate(false);
		}

		private void Deflate(bool flushing)
		{
			while (flushing || !deflater_.IsNeedingInput)
			{
				int deflateCount = deflater_.Deflate(buffer_, 0, buffer_.Length);

				if (deflateCount <= 0)
				{
					break;
				}
				if (cryptoTransform_ != null)
				{
					EncryptBlock(buffer_, 0, deflateCount);
				}

				baseOutputStream_.Write(buffer_, 0, deflateCount);
			}

			if (!deflater_.IsNeedingInput)
			{
				throw new SharpZipBaseException("DeflaterOutputStream can't deflate all input?");
			}
		}

		#endregion Deflation Support

		#region Stream Overrides

		public override bool CanRead
		{
			get
			{
				return false;
			}
		}

		public override bool CanSeek
		{
			get
			{
				return false;
			}
		}

		public override bool CanWrite
		{
			get
			{
				return baseOutputStream_.CanWrite;
			}
		}

		public override long Length
		{
			get
			{
				return baseOutputStream_.Length;
			}
		}

		public override long Position
		{
			get
			{
				return baseOutputStream_.Position;
			}
			set
			{
				throw new NotSupportedException("Position property not supported");
			}
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("DeflaterOutputStream Seek not supported");
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("DeflaterOutputStream SetLength not supported");
		}

		public override int ReadByte()
		{
			throw new NotSupportedException("DeflaterOutputStream ReadByte not supported");
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("DeflaterOutputStream Read not supported");
		}

		public override void Flush()
		{
			deflater_.Flush();
			Deflate(true);
			baseOutputStream_.Flush();
		}

		protected override void Dispose(bool disposing)
		{
			if (!isClosed_)
			{
				isClosed_ = true;

				try
				{
					Finish();
					if (cryptoTransform_ != null)
					{
						GetAuthCodeIfAES();
						cryptoTransform_.Dispose();
						cryptoTransform_ = null;
					}
				}
				finally
				{
					if (IsStreamOwner)
					{
						baseOutputStream_.Dispose();
					}
				}
			}
		}

		protected void GetAuthCodeIfAES()
		{
			if (cryptoTransform_ is ZipAESTransform)
			{
				AESAuthCode = ((ZipAESTransform)cryptoTransform_).GetAuthCode();
			}
		}

		public override void WriteByte(byte value)
		{
			byte[] b = new byte[1];
			b[0] = value;
			Write(b, 0, 1);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			deflater_.SetInput(buffer, offset, count);
			Deflate();
		}

		#endregion Stream Overrides

		#region Instance Fields

		private byte[] buffer_;

		protected Deflater deflater_;

		protected Stream baseOutputStream_;

		private bool isClosed_;

		#endregion Instance Fields
	}
}
