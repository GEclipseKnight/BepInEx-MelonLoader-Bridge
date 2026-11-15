using MelonLoader.ICSharpCode.SharpZipLib.Checksum;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using MelonLoader.ICSharpCode.SharpZipLib.Encryption;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public class ZipOutputStream : DeflaterOutputStream
	{
		#region Constructors

		public ZipOutputStream(Stream baseOutputStream)
			: base(baseOutputStream, new Deflater(Deflater.DEFAULT_COMPRESSION, true))
		{
		}

		public ZipOutputStream(Stream baseOutputStream, int bufferSize)
			: base(baseOutputStream, new Deflater(Deflater.DEFAULT_COMPRESSION, true), bufferSize)
		{
		}

		#endregion Constructors

		public bool IsFinished
		{
			get
			{
				return entries == null;
			}
		}

		public void SetComment(string comment)
		{
			byte[] commentBytes = ZipStrings.ConvertToArray(comment);
			if (commentBytes.Length > 0xffff)
			{
				throw new ArgumentOutOfRangeException(nameof(comment));
			}
			zipComment = commentBytes;
		}

		public void SetLevel(int level)
		{
			deflater_.SetLevel(level);
			defaultCompressionLevel = level;
		}

		public int GetLevel()
		{
			return deflater_.GetLevel();
		}

		public UseZip64 UseZip64
		{
			get { return useZip64_; }
			set { useZip64_ = value; }
		}

		public INameTransform NameTransform { get; set; } = new PathTransformer();

		public string Password
		{
			get
			{
				return password;
			}
			set
			{
				if ((value != null) && (value.Length == 0))
				{
					password = null;
				}
				else
				{
					password = value;
				}
			}
		}

		private void WriteLeShort(int value)
		{
			unchecked
			{
				baseOutputStream_.WriteByte((byte)(value & 0xff));
				baseOutputStream_.WriteByte((byte)((value >> 8) & 0xff));
			}
		}

		private void WriteLeInt(int value)
		{
			unchecked
			{
				WriteLeShort(value);
				WriteLeShort(value >> 16);
			}
		}

		private void WriteLeLong(long value)
		{
			unchecked
			{
				WriteLeInt((int)value);
				WriteLeInt((int)(value >> 32));
			}
		}

		private void TransformEntryName(ZipEntry entry)
		{
			if (this.NameTransform != null)
			{
				if (entry.IsDirectory)
				{
					entry.Name = this.NameTransform.TransformDirectory(entry.Name);
				}
				else
				{
					entry.Name = this.NameTransform.TransformFile(entry.Name);
				}
			}
		}

		public void PutNextEntry(ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			if (entries == null)
			{
				throw new InvalidOperationException("ZipOutputStream was finished");
			}

			if (curEntry != null)
			{
				CloseEntry();
			}

			if (entries.Count == int.MaxValue)
			{
				throw new ZipException("Too many entries for Zip file");
			}

			CompressionMethod method = entry.CompressionMethod;

			if (method != CompressionMethod.Deflated && method != CompressionMethod.Stored)
			{
				throw new NotImplementedException("Compression method not supported");
			}

			if (entry.AESKeySize > 0 && string.IsNullOrEmpty(this.Password))
			{
				throw new InvalidOperationException("The Password property must be set before AES encrypted entries can be added");
			}

			int compressionLevel = defaultCompressionLevel;

			entry.Flags &= (int)GeneralBitFlags.UnicodeText;
			patchEntryHeader = false;

			bool headerInfoAvailable;

			if (entry.Size == 0)
			{
				entry.CompressedSize = entry.Size;
				entry.Crc = 0;
				method = CompressionMethod.Stored;
				headerInfoAvailable = true;
			}
			else
			{
				headerInfoAvailable = (entry.Size >= 0) && entry.HasCrc && entry.CompressedSize >= 0;

				if (method == CompressionMethod.Stored)
				{
					if (!headerInfoAvailable)
					{
						if (!CanPatchEntries)
						{
							method = CompressionMethod.Deflated;
							compressionLevel = 0;
						}
					}
					else // entry.size must be > 0
					{
						entry.CompressedSize = entry.Size;
						headerInfoAvailable = entry.HasCrc;
					}
				}
			}

			if (headerInfoAvailable == false)
			{
				if (CanPatchEntries == false)
				{

					entry.Flags |= 8;
				}
				else
				{
					patchEntryHeader = true;
				}
			}

			if (Password != null)
			{
				entry.IsCrypted = true;
				if (entry.Crc < 0)
				{
					entry.Flags |= 8;
				}
			}

			entry.Offset = offset;
			entry.CompressionMethod = (CompressionMethod)method;

			curMethod = method;
			sizePatchPos = -1;

			if ((useZip64_ == UseZip64.On) || ((entry.Size < 0) && (useZip64_ == UseZip64.Dynamic)))
			{
				entry.ForceZip64();
			}

			WriteLeInt(ZipConstants.LocalHeaderSignature);

			WriteLeShort(entry.Version);
			WriteLeShort(entry.Flags);
			WriteLeShort((byte)entry.CompressionMethodForHeader);
			WriteLeInt((int)entry.DosTime);

			if (headerInfoAvailable)
			{
				WriteLeInt((int)entry.Crc);
				if (entry.LocalHeaderRequiresZip64)
				{
					WriteLeInt(-1);
					WriteLeInt(-1);
				}
				else
				{
					WriteLeInt((int)entry.CompressedSize + entry.EncryptionOverheadSize);
					WriteLeInt((int)entry.Size);
				}
			}
			else
			{
				if (patchEntryHeader)
				{
					crcPatchPos = baseOutputStream_.Position;
				}
				WriteLeInt(0);  // Crc

				if (patchEntryHeader)
				{
					sizePatchPos = baseOutputStream_.Position;
				}

				if (entry.LocalHeaderRequiresZip64 || patchEntryHeader)
				{
					WriteLeInt(-1);
					WriteLeInt(-1);
				}
				else
				{
					WriteLeInt(0);  // Compressed size
					WriteLeInt(0);  // Uncompressed size
				}
			}

			TransformEntryName(entry);
			byte[] name = ZipStrings.ConvertToArray(entry.Flags, entry.Name);

			if (name.Length > 0xFFFF)
			{
				throw new ZipException("Entry name too long.");
			}

			var ed = new ZipExtraData(entry.ExtraData);

			if (entry.LocalHeaderRequiresZip64)
			{
				ed.StartNewEntry();
				if (headerInfoAvailable)
				{
					ed.AddLeLong(entry.Size);
					ed.AddLeLong(entry.CompressedSize + entry.EncryptionOverheadSize);
				}
				else
				{
					ed.AddLeLong(-1);
					ed.AddLeLong(-1);
				}
				ed.AddNewEntry(1);

				if (!ed.Find(1))
				{
					throw new ZipException("Internal error cant find extra data");
				}

				if (patchEntryHeader)
				{
					sizePatchPos = ed.CurrentReadIndex;
				}
			}
			else
			{
				ed.Delete(1);
			}

			if (entry.AESKeySize > 0)
			{
				AddExtraDataAES(entry, ed);
			}
			byte[] extra = ed.GetEntryData();

			WriteLeShort(name.Length);
			WriteLeShort(extra.Length);

			if (name.Length > 0)
			{
				baseOutputStream_.Write(name, 0, name.Length);
			}

			if (entry.LocalHeaderRequiresZip64 && patchEntryHeader)
			{
				sizePatchPos += baseOutputStream_.Position;
			}

			if (extra.Length > 0)
			{
				baseOutputStream_.Write(extra, 0, extra.Length);
			}

			offset += ZipConstants.LocalHeaderBaseSize + name.Length + extra.Length;
			if (entry.AESKeySize > 0)
				offset += entry.AESOverheadSize;

			curEntry = entry;
			crc.Reset();
			if (method == CompressionMethod.Deflated)
			{
				deflater_.Reset();
				deflater_.SetLevel(compressionLevel);
			}
			size = 0;

			if (entry.IsCrypted)
			{
				if (entry.AESKeySize > 0)
				{
					WriteAESHeader(entry);
				}
				else
				{
					if (entry.Crc < 0)
					{            // so testing Zip will says its ok
						WriteEncryptionHeader(entry.DosTime << 16);
					}
					else
					{
						WriteEncryptionHeader(entry.Crc);
					}
				}
			}
		}

		public void CloseEntry()
		{
			if (curEntry == null)
			{
				throw new InvalidOperationException("No open entry");
			}

			long csize = size;

			if (curMethod == CompressionMethod.Deflated)
			{
				if (size >= 0)
				{
					base.Finish();
					csize = deflater_.TotalOut;
				}
				else
				{
					deflater_.Reset();
				}
			}
			else if (curMethod == CompressionMethod.Stored)
			{
				base.GetAuthCodeIfAES();
			}

			if (curEntry.AESKeySize > 0)
			{
				baseOutputStream_.Write(AESAuthCode, 0, 10);
				curEntry.Crc = 0;
			}
			else
			{
				if (curEntry.Crc < 0)
				{
					curEntry.Crc = crc.Value;
				}
				else if (curEntry.Crc != crc.Value)
				{
					throw new ZipException($"crc was {crc.Value}, but {curEntry.Crc} was expected");
				}
			}

			if (curEntry.Size < 0)
			{
				curEntry.Size = size;
			}
			else if (curEntry.Size != size)
			{
				throw new ZipException($"size was {size}, but {curEntry.Size} was expected");
			}

			if (curEntry.CompressedSize < 0)
			{
				curEntry.CompressedSize = csize;
			}
			else if (curEntry.CompressedSize != csize)
			{
				throw new ZipException($"compressed size was {csize}, but {curEntry.CompressedSize} expected");
			}

			offset += csize;

			if (curEntry.IsCrypted)
			{
				curEntry.CompressedSize += curEntry.EncryptionOverheadSize;
			}

			if (patchEntryHeader)
			{
				patchEntryHeader = false;

				long curPos = baseOutputStream_.Position;
				baseOutputStream_.Seek(crcPatchPos, SeekOrigin.Begin);
				WriteLeInt((int)curEntry.Crc);

				if (curEntry.LocalHeaderRequiresZip64)
				{
					if (sizePatchPos == -1)
					{
						throw new ZipException("Entry requires zip64 but this has been turned off");
					}

					baseOutputStream_.Seek(sizePatchPos, SeekOrigin.Begin);
					WriteLeLong(curEntry.Size);
					WriteLeLong(curEntry.CompressedSize);
				}
				else
				{
					WriteLeInt((int)curEntry.CompressedSize);
					WriteLeInt((int)curEntry.Size);
				}
				baseOutputStream_.Seek(curPos, SeekOrigin.Begin);
			}

			if ((curEntry.Flags & 8) != 0)
			{
				WriteLeInt(ZipConstants.DataDescriptorSignature);
				WriteLeInt(unchecked((int)curEntry.Crc));

				if (curEntry.LocalHeaderRequiresZip64)
				{
					WriteLeLong(curEntry.CompressedSize);
					WriteLeLong(curEntry.Size);
					offset += ZipConstants.Zip64DataDescriptorSize;
				}
				else
				{
					WriteLeInt((int)curEntry.CompressedSize);
					WriteLeInt((int)curEntry.Size);
					offset += ZipConstants.DataDescriptorSize;
				}
			}

			entries.Add(curEntry);
			curEntry = null;
		}

		private void InitializePassword(string password)
		{
			var pkManaged = new PkzipClassicManaged();
			byte[] key = PkzipClassic.GenerateKeys(ZipStrings.ConvertToArray(password));
			cryptoTransform_ = pkManaged.CreateEncryptor(key, null);
		}

		private void InitializeAESPassword(ZipEntry entry, string rawPassword,
											out byte[] salt, out byte[] pwdVerifier)
		{
			salt = new byte[entry.AESSaltLen];

			_aesRnd.GetBytes(salt);

			int blockSize = entry.AESKeySize / 8;   // bits to bytes

			cryptoTransform_ = new ZipAESTransform(rawPassword, salt, blockSize, true);
			pwdVerifier = ((ZipAESTransform)cryptoTransform_).PwdVerifier;
		}

		private void WriteEncryptionHeader(long crcValue)
		{
			offset += ZipConstants.CryptoHeaderSize;

			InitializePassword(Password);

			byte[] cryptBuffer = new byte[ZipConstants.CryptoHeaderSize];
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(cryptBuffer);

			cryptBuffer[11] = (byte)(crcValue >> 24);

			EncryptBlock(cryptBuffer, 0, cryptBuffer.Length);
			baseOutputStream_.Write(cryptBuffer, 0, cryptBuffer.Length);
		}

		private static void AddExtraDataAES(ZipEntry entry, ZipExtraData extraData)
		{
			const int VENDOR_VERSION = 2;
			const int VENDOR_ID = 0x4541; //not 6965;
			extraData.StartNewEntry();
			extraData.AddLeShort(VENDOR_VERSION);               // 2 = AE-2
			extraData.AddLeShort(VENDOR_ID);                    // "AE"
			extraData.AddData(entry.AESEncryptionStrength);     //  1 = 128, 2 = 192, 3 = 256
			extraData.AddLeShort((int)entry.CompressionMethod); // The actual compression method used to compress the file
			extraData.AddNewEntry(0x9901);
		}

		private void WriteAESHeader(ZipEntry entry)
		{
			byte[] salt;
			byte[] pwdVerifier;
			InitializeAESPassword(entry, Password, out salt, out pwdVerifier);
			baseOutputStream_.Write(salt, 0, salt.Length);
			baseOutputStream_.Write(pwdVerifier, 0, pwdVerifier.Length);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			if (curEntry == null)
			{
				throw new InvalidOperationException("No open entry.");
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), "Cannot be negative");
			}

			if (count < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(count), "Cannot be negative");
			}

			if ((buffer.Length - offset) < count)
			{
				throw new ArgumentException("Invalid offset/count combination");
			}

			if (curEntry.AESKeySize == 0)
			{
				crc.Update(new ArraySegment<byte>(buffer, offset, count));
			}

			size += count;

			switch (curMethod)
			{
				case CompressionMethod.Deflated:
					base.Write(buffer, offset, count);
					break;

				case CompressionMethod.Stored:
					if (Password != null)
					{
						CopyAndEncrypt(buffer, offset, count);
					}
					else
					{
						baseOutputStream_.Write(buffer, offset, count);
					}
					break;
			}
		}

		private void CopyAndEncrypt(byte[] buffer, int offset, int count)
		{
			const int CopyBufferSize = 4096;
			byte[] localBuffer = new byte[CopyBufferSize];
			while (count > 0)
			{
				int bufferCount = (count < CopyBufferSize) ? count : CopyBufferSize;

				Array.Copy(buffer, offset, localBuffer, 0, bufferCount);
				EncryptBlock(localBuffer, 0, bufferCount);
				baseOutputStream_.Write(localBuffer, 0, bufferCount);
				count -= bufferCount;
				offset += bufferCount;
			}
		}

		public override void Finish()
		{
			if (entries == null)
			{
				return;
			}

			if (curEntry != null)
			{
				CloseEntry();
			}

			long numEntries = entries.Count;
			long sizeEntries = 0;

			foreach (ZipEntry entry in entries)
			{
				WriteLeInt(ZipConstants.CentralHeaderSignature);
				WriteLeShort((entry.HostSystem << 8) | entry.VersionMadeBy);
				WriteLeShort(entry.Version);
				WriteLeShort(entry.Flags);
				WriteLeShort((short)entry.CompressionMethodForHeader);
				WriteLeInt((int)entry.DosTime);
				WriteLeInt((int)entry.Crc);

				if (entry.IsZip64Forced() ||
					(entry.CompressedSize >= uint.MaxValue))
				{
					WriteLeInt(-1);
				}
				else
				{
					WriteLeInt((int)entry.CompressedSize);
				}

				if (entry.IsZip64Forced() ||
					(entry.Size >= uint.MaxValue))
				{
					WriteLeInt(-1);
				}
				else
				{
					WriteLeInt((int)entry.Size);
				}

				byte[] name = ZipStrings.ConvertToArray(entry.Flags, entry.Name);

				if (name.Length > 0xffff)
				{
					throw new ZipException("Name too long.");
				}

				var ed = new ZipExtraData(entry.ExtraData);

				if (entry.CentralHeaderRequiresZip64)
				{
					ed.StartNewEntry();
					if (entry.IsZip64Forced() ||
						(entry.Size >= 0xffffffff))
					{
						ed.AddLeLong(entry.Size);
					}

					if (entry.IsZip64Forced() ||
						(entry.CompressedSize >= 0xffffffff))
					{
						ed.AddLeLong(entry.CompressedSize);
					}

					if (entry.Offset >= 0xffffffff)
					{
						ed.AddLeLong(entry.Offset);
					}

					ed.AddNewEntry(1);
				}
				else
				{
					ed.Delete(1);
				}

				if (entry.AESKeySize > 0)
				{
					AddExtraDataAES(entry, ed);
				}
				byte[] extra = ed.GetEntryData();

				byte[] entryComment =
					(entry.Comment != null) ?
					ZipStrings.ConvertToArray(entry.Flags, entry.Comment) :
					Empty.Array<byte>();

				if (entryComment.Length > 0xffff)
				{
					throw new ZipException("Comment too long.");
				}

				WriteLeShort(name.Length);
				WriteLeShort(extra.Length);
				WriteLeShort(entryComment.Length);
				WriteLeShort(0);    // disk number
				WriteLeShort(0);    // internal file attributes

				if (entry.ExternalFileAttributes != -1)
				{
					WriteLeInt(entry.ExternalFileAttributes);
				}
				else
				{
					if (entry.IsDirectory)
					{                         // mark entry as directory (from nikolam.AT.perfectinfo.com)
						WriteLeInt(16);
					}
					else
					{
						WriteLeInt(0);
					}
				}

				if (entry.Offset >= uint.MaxValue)
				{
					WriteLeInt(-1);
				}
				else
				{
					WriteLeInt((int)entry.Offset);
				}

				if (name.Length > 0)
				{
					baseOutputStream_.Write(name, 0, name.Length);
				}

				if (extra.Length > 0)
				{
					baseOutputStream_.Write(extra, 0, extra.Length);
				}

				if (entryComment.Length > 0)
				{
					baseOutputStream_.Write(entryComment, 0, entryComment.Length);
				}

				sizeEntries += ZipConstants.CentralHeaderBaseSize + name.Length + extra.Length + entryComment.Length;
			}

			using (ZipHelperStream zhs = new ZipHelperStream(baseOutputStream_))
			{
				zhs.WriteEndOfCentralDirectory(numEntries, sizeEntries, offset, zipComment);
			}

			entries = null;
		}

		public override void Flush()
		{
			if(curMethod == CompressionMethod.Stored)
			{
				baseOutputStream_.Flush();
			} 
			else
			{
				base.Flush();
			}
		}

		#region Instance Fields

		private List<ZipEntry> entries = new List<ZipEntry>();

		private Crc32 crc = new Crc32();

		private ZipEntry curEntry;

		private int defaultCompressionLevel = Deflater.DEFAULT_COMPRESSION;

		private CompressionMethod curMethod = CompressionMethod.Deflated;

		private long size;

		private long offset;

		private byte[] zipComment = Empty.Array<byte>();

		private bool patchEntryHeader;

		private long crcPatchPos = -1;

		private long sizePatchPos = -1;

		private UseZip64 useZip64_ = UseZip64.Dynamic;

		private string password;

		#endregion Instance Fields

		#region Static Fields

		private static RandomNumberGenerator _aesRnd = RandomNumberGenerator.Create();

		#endregion Static Fields
	}
}
