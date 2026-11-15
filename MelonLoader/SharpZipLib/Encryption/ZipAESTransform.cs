using System;
using System.Security.Cryptography;
using MelonLoader.ICSharpCode.SharpZipLib.Core;

namespace MelonLoader.ICSharpCode.SharpZipLib.Encryption
{
	internal class ZipAESTransform : ICryptoTransform
	{
		class IncrementalHash : HMACSHA1
		{
			bool _finalised;
			public IncrementalHash(byte[] key) : base(key) { }
			public static IncrementalHash CreateHMAC(string n, byte[] key) => new IncrementalHash(key);
			public void AppendData(byte[] buffer, int offset, int count) => TransformBlock(buffer, offset, count, buffer, offset);
			public byte[] GetHashAndReset()
			{
				if (!_finalised)
				{
					byte[] dummy = new byte[0];
					TransformFinalBlock(dummy, 0, 0);
					_finalised = true;
				}
				return Hash;
			}
		}

		static class HashAlgorithmName
		{
			public static string SHA1 = null;
		}

		private const int PWD_VER_LENGTH = 2;

		private const int KEY_ROUNDS = 1000;

		private const int ENCRYPT_BLOCK = 16;

		private int _blockSize;
		private readonly ICryptoTransform _encryptor;
		private readonly byte[] _counterNonce;
		private byte[] _encryptBuffer;
		private int _encrPos;
		private byte[] _pwdVerifier;
		private IncrementalHash _hmacsha1;
		private byte[] _authCode = null;

		private bool _writeMode;

		public ZipAESTransform(string key, byte[] saltBytes, int blockSize, bool writeMode)
		{
			if (blockSize != 16 && blockSize != 32) // 24 valid for AES but not supported by Winzip
				throw new Exception("Invalid blocksize " + blockSize + ". Must be 16 or 32.");
			if (saltBytes.Length != blockSize / 2)
				throw new Exception("Invalid salt len. Must be " + blockSize / 2 + " for blocksize " + blockSize);
			_blockSize = blockSize;
			_encryptBuffer = new byte[_blockSize];
			_encrPos = ENCRYPT_BLOCK;

			var pdb = new Rfc2898DeriveBytes(key, saltBytes, KEY_ROUNDS);
			var rm = Aes.Create();
			rm.Mode = CipherMode.ECB;           // No feedback from cipher for CTR mode
			_counterNonce = new byte[_blockSize];
			byte[] key1bytes = pdb.GetBytes(_blockSize);
			byte[] key2bytes = pdb.GetBytes(_blockSize);

			_encryptor = rm.CreateEncryptor(key1bytes, new byte[16]);
			_pwdVerifier = pdb.GetBytes(PWD_VER_LENGTH);
			_hmacsha1 = IncrementalHash.CreateHMAC(HashAlgorithmName.SHA1, key2bytes);
			_writeMode = writeMode;
		}

		public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
		{
			if (!_writeMode)
			{
				_hmacsha1.AppendData(inputBuffer, inputOffset, inputCount);
			}
			int ix = 0;
			while (ix < inputCount)
			{
				if (_encrPos == ENCRYPT_BLOCK)
				{
					/* increment encryption nonce   */
					int j = 0;
					while (++_counterNonce[j] == 0)
					{
						++j;
					}
					/* encrypt the nonce to form next xor buffer    */
					_encryptor.TransformBlock(_counterNonce, 0, _blockSize, _encryptBuffer, 0);
					_encrPos = 0;
				}
				outputBuffer[ix + outputOffset] = (byte)(inputBuffer[ix + inputOffset] ^ _encryptBuffer[_encrPos++]);
				ix++;
			}
			if (_writeMode)
			{
				_hmacsha1.AppendData(outputBuffer, outputOffset, inputCount);
			}
			return inputCount;
		}

		public byte[] PwdVerifier
		{
			get
			{
				return _pwdVerifier;
			}
		}

		public byte[] GetAuthCode()
		{
			if (_authCode == null)
			{
				_authCode = _hmacsha1.GetHashAndReset();
			}
			return _authCode;
		}

		#region ICryptoTransform Members

		public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
		{
			if(inputCount > 0)
			{
				throw new NotImplementedException("TransformFinalBlock is not implemented and inputCount is greater than 0");
			}
			return Empty.Array<byte>();
		}

		public int InputBlockSize
		{
			get
			{
				return _blockSize;
			}
		}

		public int OutputBlockSize
		{
			get
			{
				return _blockSize;
			}
		}

		public bool CanTransformMultipleBlocks
		{
			get
			{
				return true;
			}
		}

		public bool CanReuseTransform
		{
			get
			{
				return true;
			}
		}

		public void Dispose()
		{
			_encryptor.Dispose();
		}

		#endregion ICryptoTransform Members
	}
}
