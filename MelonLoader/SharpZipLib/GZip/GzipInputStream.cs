using MelonLoader.ICSharpCode.SharpZipLib.Checksum;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.IO;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.GZip
{
	public class GZipInputStream : InflaterInputStream
	{
		#region Instance Fields

		protected Crc32 crc;

		private bool readGZIPHeader;

		private bool completedLastBlock;

		private string fileName;

		#endregion Instance Fields

		#region Constructors

		public GZipInputStream(Stream baseInputStream)
			: this(baseInputStream, 4096)
		{
		}

		public GZipInputStream(Stream baseInputStream, int size)
			: base(baseInputStream, new Inflater(true), size)
		{
		}

		#endregion Constructors

		#region Stream overrides

		public override int Read(byte[] buffer, int offset, int count)
		{
			while (true)
			{
				if (!readGZIPHeader)
				{
					try
					{
						if (!ReadHeader())
						{
							return 0;
						}
					}
					catch (Exception ex) when (completedLastBlock && (ex is GZipException || ex is EndOfStreamException))
					{
						return 0;
					}
				}

				int bytesRead = base.Read(buffer, offset, count);
				if (bytesRead > 0)
				{
					crc.Update(new ArraySegment<byte>(buffer, offset, bytesRead));
				}

				if (inf.IsFinished)
				{
					ReadFooter();
				}

				if (bytesRead > 0 || count == 0)
				{
					return bytesRead;
				}
			}
		}

		public string GetFilename()
		{
			return fileName;
		}

		#endregion Stream overrides

		#region Support routines

		private bool ReadHeader()
		{
			crc = new Crc32();

			if (inputBuffer.Available <= 0)
			{
				inputBuffer.Fill();
				if (inputBuffer.Available <= 0)
				{
					return false;
				}
			}

			var headCRC = new Crc32();


			var magic = inputBuffer.ReadLeByte();
			headCRC.Update(magic);
			if (magic != GZipConstants.ID1)
			{
				throw new GZipException("Error GZIP header, first magic byte doesn't match");
			}

			magic = inputBuffer.ReadLeByte();
			if (magic != GZipConstants.ID2)
			{
				throw new GZipException("Error GZIP header,  second magic byte doesn't match");
			}
			headCRC.Update(magic);

			var compressionType = inputBuffer.ReadLeByte();

			if (compressionType != GZipConstants.CompressionMethodDeflate)
			{
				throw new GZipException("Error GZIP header, data not in deflate format");
			}
			headCRC.Update(compressionType);

			var flagsByte = inputBuffer.ReadLeByte();

			headCRC.Update(flagsByte);


			if ((flagsByte & 0xE0) != 0)
			{
				throw new GZipException("Reserved flag bits in GZIP header != 0");
			}

			var flags = (GZipFlags)flagsByte;

			for (int i = 0; i < 6; i++)
			{
				headCRC.Update(inputBuffer.ReadLeByte());
			}

			if (flags.HasFlag(GZipFlags.FEXTRA))
			{
				var len1 = inputBuffer.ReadLeByte();
				var len2 = inputBuffer.ReadLeByte();

				headCRC.Update(len1);
				headCRC.Update(len2);

				int extraLen = (len2 << 8) | len1;      // gzip is LSB first
				for (int i = 0; i < extraLen; i++)
				{
					headCRC.Update(inputBuffer.ReadLeByte());
				}
			}

			if (flags.HasFlag(GZipFlags.FNAME))
			{
				var fname = new byte[1024];
				var fnamePos = 0;
				int readByte;
				while ((readByte = inputBuffer.ReadLeByte()) > 0)
				{
					if (fnamePos < 1024)
					{
						fname[fnamePos++] = (byte)readByte;
					}
					headCRC.Update(readByte);
				}

				headCRC.Update(readByte);

				fileName = GZipConstants.Encoding.GetString(fname, 0, fnamePos);
			}
			else
			{
				fileName = null;
			}

			if (flags.HasFlag(GZipFlags.FCOMMENT))
			{
				int readByte;
				while ((readByte = inputBuffer.ReadLeByte()) > 0)
				{
					headCRC.Update(readByte);
				}

				headCRC.Update(readByte);
			}

			if (flags.HasFlag(GZipFlags.FHCRC))
			{
				int tempByte;
				int crcval = inputBuffer.ReadLeByte();
				if (crcval < 0)
				{
					throw new EndOfStreamException("EOS reading GZIP header");
				}

				tempByte = inputBuffer.ReadLeByte();
				if (tempByte < 0)
				{
					throw new EndOfStreamException("EOS reading GZIP header");
				}

				crcval = (crcval << 8) | tempByte;
				if (crcval != ((int)headCRC.Value & 0xffff))
				{
					throw new GZipException("Header CRC value mismatch");
				}
			}

			readGZIPHeader = true;
			return true;
		}

		private void ReadFooter()
		{
			byte[] footer = new byte[8];

			long bytesRead = inf.TotalOut & 0xffffffff;
			inputBuffer.Available += inf.RemainingInput;
			inf.Reset();

			int needed = 8;
			while (needed > 0)
			{
				int count = inputBuffer.ReadClearTextBuffer(footer, 8 - needed, needed);
				if (count <= 0)
				{
					throw new EndOfStreamException("EOS reading GZIP footer");
				}
				needed -= count; // Jewel Jan 16
			}

			int crcval = (footer[0] & 0xff) | ((footer[1] & 0xff) << 8) | ((footer[2] & 0xff) << 16) | (footer[3] << 24);
			if (crcval != (int)crc.Value)
			{
				throw new GZipException("GZIP crc sum mismatch, theirs \"" + crcval + "\" and ours \"" + (int)crc.Value);
			}

			uint total =
				(uint)((uint)footer[4] & 0xff) |
				(uint)(((uint)footer[5] & 0xff) << 8) |
				(uint)(((uint)footer[6] & 0xff) << 16) |
				(uint)((uint)footer[7] << 24);

			if (bytesRead != total)
			{
				throw new GZipException("Number of bytes mismatch in footer");
			}

			readGZIPHeader = false;

			completedLastBlock = true;
		}

		#endregion Support routines
	}
}
