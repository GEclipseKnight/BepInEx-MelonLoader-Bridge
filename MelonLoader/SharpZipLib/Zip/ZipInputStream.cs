using MelonLoader.ICSharpCode.SharpZipLib.Checksum;
using MelonLoader.ICSharpCode.SharpZipLib.Encryption;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public class ZipInputStream : InflaterInputStream
	{
		#region Instance Fields

		private delegate int ReadDataHandler(byte[] b, int offset, int length);

		private ReadDataHandler internalReader;

		private Crc32 crc = new Crc32();
		private ZipEntry entry;

		private long size;
		private CompressionMethod method;
		private int flags;
		private string password;

		#endregion Instance Fields

		#region Constructors

		public ZipInputStream(Stream baseInputStream)
			: base(baseInputStream, new Inflater(true))
		{
			internalReader = new ReadDataHandler(ReadingNotAvailable);
		}

		public ZipInputStream(Stream baseInputStream, int bufferSize)
			: base(baseInputStream, new Inflater(true), bufferSize)
		{
			internalReader = new ReadDataHandler(ReadingNotAvailable);
		}

		#endregion Constructors

		public string Password
		{
			get
			{
				return password;
			}
			set
			{
				password = value;
			}
		}

		public bool CanDecompressEntry 
			=> entry != null
			&& IsEntryCompressionMethodSupported(entry)
			&& entry.CanDecompress
			&& (!entry.HasFlag(GeneralBitFlags.Descriptor) || entry.CompressionMethod != CompressionMethod.Stored || entry.IsCrypted);

		private static bool IsEntryCompressionMethodSupported(ZipEntry entry)
		{
			var entryCompressionMethod = entry.CompressionMethodForHeader;

			return entryCompressionMethod == CompressionMethod.Deflated ||
				   entryCompressionMethod == CompressionMethod.Stored;
		}

		public ZipEntry GetNextEntry()
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed.");
			}

			if (entry != null)
			{
				CloseEntry();
			}

			int header = inputBuffer.ReadLeInt();

			if (header == ZipConstants.CentralHeaderSignature ||
				header == ZipConstants.EndOfCentralDirectorySignature ||
				header == ZipConstants.CentralHeaderDigitalSignature ||
				header == ZipConstants.ArchiveExtraDataSignature ||
				header == ZipConstants.Zip64CentralFileHeaderSignature)
			{
				Dispose();
				return null;
			}

			if ((header == ZipConstants.SpanningTempSignature) || (header == ZipConstants.SpanningSignature))
			{
				header = inputBuffer.ReadLeInt();
			}

			if (header != ZipConstants.LocalHeaderSignature)
			{
				throw new ZipException("Wrong Local header signature: 0x" + String.Format("{0:X}", header));
			}

			var versionRequiredToExtract = (short)inputBuffer.ReadLeShort();

			flags = inputBuffer.ReadLeShort();
			method = (CompressionMethod)inputBuffer.ReadLeShort();
			var dostime = (uint)inputBuffer.ReadLeInt();
			int crc2 = inputBuffer.ReadLeInt();
			csize = inputBuffer.ReadLeInt();
			size = inputBuffer.ReadLeInt();
			int nameLen = inputBuffer.ReadLeShort();
			int extraLen = inputBuffer.ReadLeShort();

			bool isCrypted = (flags & 1) == 1;

			byte[] buffer = new byte[nameLen];
			inputBuffer.ReadRawBuffer(buffer);

			string name = ZipStrings.ConvertToStringExt(flags, buffer);

			entry = new ZipEntry(name, versionRequiredToExtract, ZipConstants.VersionMadeBy, method)
			{
				Flags = flags,
			};

			if ((flags & 8) == 0)
			{
				entry.Crc = crc2 & 0xFFFFFFFFL;
				entry.Size = size & 0xFFFFFFFFL;
				entry.CompressedSize = csize & 0xFFFFFFFFL;

				entry.CryptoCheckValue = (byte)((crc2 >> 24) & 0xff);
			}
			else
			{
				if (crc2 != 0)
				{
					entry.Crc = crc2 & 0xFFFFFFFFL;
				}

				if (size != 0)
				{
					entry.Size = size & 0xFFFFFFFFL;
				}

				if (csize != 0)
				{
					entry.CompressedSize = csize & 0xFFFFFFFFL;
				}

				entry.CryptoCheckValue = (byte)((dostime >> 8) & 0xff);
			}

			entry.DosTime = dostime;


			if (extraLen > 0)
			{
				byte[] extra = new byte[extraLen];
				inputBuffer.ReadRawBuffer(extra);
				entry.ExtraData = extra;
			}

			entry.ProcessExtraData(true);
			if (entry.CompressedSize >= 0)
			{
				csize = entry.CompressedSize;
			}

			if (entry.Size >= 0)
			{
				size = entry.Size;
			}

			if (method == CompressionMethod.Stored && (!isCrypted && csize != size || (isCrypted && csize - ZipConstants.CryptoHeaderSize != size)))
			{
				throw new ZipException("Stored, but compressed != uncompressed");
			}

			if (IsEntryCompressionMethodSupported(entry))
			{
				internalReader = new ReadDataHandler(InitialRead);
			}
			else
			{
				internalReader = new ReadDataHandler(ReadingNotSupported);
			}

			return entry;
		}

		private void ReadDataDescriptor()
		{
			if (inputBuffer.ReadLeInt() != ZipConstants.DataDescriptorSignature)
			{
				throw new ZipException("Data descriptor signature not found");
			}

			entry.Crc = inputBuffer.ReadLeInt() & 0xFFFFFFFFL;

			if (entry.LocalHeaderRequiresZip64)
			{
				csize = inputBuffer.ReadLeLong();
				size = inputBuffer.ReadLeLong();
			}
			else
			{
				csize = inputBuffer.ReadLeInt();
				size = inputBuffer.ReadLeInt();
			}
			entry.CompressedSize = csize;
			entry.Size = size;
		}

		private void CompleteCloseEntry(bool testCrc)
		{
			StopDecrypting();

			if ((flags & 8) != 0)
			{
				ReadDataDescriptor();
			}

			size = 0;

			if (testCrc &&
				((crc.Value & 0xFFFFFFFFL) != entry.Crc) && (entry.Crc != -1))
			{
				throw new ZipException("CRC mismatch");
			}

			crc.Reset();

			if (method == CompressionMethod.Deflated)
			{
				inf.Reset();
			}
			entry = null;
		}

		public void CloseEntry()
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed");
			}

			if (entry == null)
			{
				return;
			}

			if (method == CompressionMethod.Deflated)
			{
				if ((flags & 8) != 0)
				{
					byte[] tmp = new byte[4096];

					while (Read(tmp, 0, tmp.Length) > 0)
					{
					}
					return;
				}

				csize -= inf.TotalIn;
				inputBuffer.Available += inf.RemainingInput;
			}

			if ((inputBuffer.Available > csize) && (csize >= 0))
			{
				inputBuffer.Available = (int)((long)inputBuffer.Available - csize);
			}
			else
			{
				csize -= inputBuffer.Available;
				inputBuffer.Available = 0;
				while (csize != 0)
				{
					long skipped = Skip(csize);

					if (skipped <= 0)
					{
						throw new ZipException("Zip archive ends early.");
					}

					csize -= skipped;
				}
			}

			CompleteCloseEntry(false);
		}

		public override int Available
		{
			get
			{
				return entry != null ? 1 : 0;
			}
		}

		public override long Length
		{
			get
			{
				if (entry != null)
				{
					if (entry.Size >= 0)
					{
						return entry.Size;
					}
					else
					{
						throw new ZipException("Length not available for the current entry");
					}
				}
				else
				{
					throw new InvalidOperationException("No current entry");
				}
			}
		}

		public override int ReadByte()
		{
			byte[] b = new byte[1];
			if (Read(b, 0, 1) <= 0)
			{
				return -1;
			}
			return b[0] & 0xff;
		}

		private int ReadingNotAvailable(byte[] destination, int offset, int count)
		{
			throw new InvalidOperationException("Unable to read from this stream");
		}

		private int ReadingNotSupported(byte[] destination, int offset, int count)
		{
			throw new ZipException("The compression method for this entry is not supported");
		}

		private int StoredDescriptorEntry(byte[] destination, int offset, int count) =>
			throw new StreamUnsupportedException(
				"The combination of Stored compression method and Descriptor flag is not possible to read using ZipInputStream");
		

		private int InitialRead(byte[] destination, int offset, int count)
		{
			var usesDescriptor = (entry.Flags & (int)GeneralBitFlags.Descriptor) != 0;

			if (entry.IsCrypted)
			{
				if (password == null)
				{
					throw new ZipException("No password set.");
				}

				var managed = new PkzipClassicManaged();
				byte[] key = PkzipClassic.GenerateKeys(ZipStrings.ConvertToArray(password));

				inputBuffer.CryptoTransform = managed.CreateDecryptor(key, null);

				byte[] cryptbuffer = new byte[ZipConstants.CryptoHeaderSize];
				inputBuffer.ReadClearTextBuffer(cryptbuffer, 0, ZipConstants.CryptoHeaderSize);

				if (cryptbuffer[ZipConstants.CryptoHeaderSize - 1] != entry.CryptoCheckValue)
				{
					throw new ZipException("Invalid password");
				}

				if (csize >= ZipConstants.CryptoHeaderSize)
				{
					csize -= ZipConstants.CryptoHeaderSize;
				}
				else if (!usesDescriptor)
				{
					throw new ZipException($"Entry compressed size {csize} too small for encryption");
				}
			}
			else
			{
				inputBuffer.CryptoTransform = null;
			}

			if (csize > 0 || usesDescriptor)
			{
				if (method == CompressionMethod.Deflated && inputBuffer.Available > 0)
				{
					inputBuffer.SetInflaterInput(inf);
				}

				if (!entry.IsCrypted && method == CompressionMethod.Stored && usesDescriptor)
				{
					internalReader = StoredDescriptorEntry;
					return StoredDescriptorEntry(destination, offset, count);
				}

				if (!CanDecompressEntry)
				{
					internalReader = ReadingNotSupported;
					return ReadingNotSupported(destination, offset, count);
				}

				internalReader = BodyRead;
				return BodyRead(destination, offset, count);
			}
			

			internalReader = ReadingNotAvailable;
			return 0;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
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

			return internalReader(buffer, offset, count);
		}

		private int BodyRead(byte[] buffer, int offset, int count)
		{
			if (crc == null)
			{
				throw new InvalidOperationException("Closed");
			}

			if ((entry == null) || (count <= 0))
			{
				return 0;
			}

			if (offset + count > buffer.Length)
			{
				throw new ArgumentException("Offset + count exceeds buffer size");
			}

			bool finished = false;

			switch (method)
			{
				case CompressionMethod.Deflated:
					count = base.Read(buffer, offset, count);
					if (count <= 0)
					{
						if (!inf.IsFinished)
						{
							throw new ZipException("Inflater not finished!");
						}
						inputBuffer.Available = inf.RemainingInput;

						if ((flags & 8) == 0 &&
							(inf.TotalIn != csize && csize != 0xFFFFFFFF && csize != -1 || inf.TotalOut != size))
						{
							throw new ZipException("Size mismatch: " + csize + ";" + size + " <-> " + inf.TotalIn + ";" + inf.TotalOut);
						}
						inf.Reset();
						finished = true;
					}
					break;

				case CompressionMethod.Stored:
					if ((count > csize) && (csize >= 0))
					{
						count = (int)csize;
					}

					if (count > 0)
					{
						count = inputBuffer.ReadClearTextBuffer(buffer, offset, count);
						if (count > 0)
						{
							csize -= count;
							size -= count;
						}
					}

					if (csize == 0)
					{
						finished = true;
					}
					else
					{
						if (count < 0)
						{
							throw new ZipException("EOF in stored block");
						}
					}
					break;
			}

			if (count > 0)
			{
				crc.Update(new ArraySegment<byte>(buffer, offset, count));
			}

			if (finished)
			{
				CompleteCloseEntry(true);
			}

			return count;
		}

		protected override void Dispose(bool disposing)
		{
			internalReader = new ReadDataHandler(ReadingNotAvailable);
			crc = null;
			entry = null;

			base.Dispose(disposing);
		}
	}
}
