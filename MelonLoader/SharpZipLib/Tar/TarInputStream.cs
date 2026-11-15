using System;
using System.IO;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.Tar
{
	public class TarInputStream : Stream
	{
		#region Constructors

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public TarInputStream(Stream inputStream)
			: this(inputStream, TarBuffer.DefaultBlockFactor, null)
		{
		}
		public TarInputStream(Stream inputStream, Encoding nameEncoding)
			: this(inputStream, TarBuffer.DefaultBlockFactor, nameEncoding)
		{
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public TarInputStream(Stream inputStream, int blockFactor)
		{
			this.inputStream = inputStream;
			tarBuffer = TarBuffer.CreateInputTarBuffer(inputStream, blockFactor);
			encoding = null;
		}

		public TarInputStream(Stream inputStream, int blockFactor, Encoding nameEncoding)
		{
			this.inputStream = inputStream;
			tarBuffer = TarBuffer.CreateInputTarBuffer(inputStream, blockFactor);
			encoding = nameEncoding;
		}

		#endregion Constructors

		public bool IsStreamOwner
		{
			get { return tarBuffer.IsStreamOwner; }
			set { tarBuffer.IsStreamOwner = value; }
		}

		#region Stream Overrides

		public override bool CanRead
		{
			get
			{
				return inputStream.CanRead;
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
				return false;
			}
		}

		public override long Length
		{
			get
			{
				return inputStream.Length;
			}
		}

		public override long Position
		{
			get
			{
				return inputStream.Position;
			}
			set
			{
				throw new NotSupportedException("TarInputStream Seek not supported");
			}
		}

		public override void Flush()
		{
			inputStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("TarInputStream Seek not supported");
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("TarInputStream SetLength not supported");
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("TarInputStream Write not supported");
		}

		public override void WriteByte(byte value)
		{
			throw new NotSupportedException("TarInputStream WriteByte not supported");
		}

		public override int ReadByte()
		{
			byte[] oneByteBuffer = new byte[1];
			int num = Read(oneByteBuffer, 0, 1);
			if (num <= 0)
			{
				return -1;
			}
			return oneByteBuffer[0];
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int totalRead = 0;

			if (entryOffset >= entrySize)
			{
				return 0;
			}

			long numToRead = count;

			if ((numToRead + entryOffset) > entrySize)
			{
				numToRead = entrySize - entryOffset;
			}

			if (readBuffer != null)
			{
				int sz = (numToRead > readBuffer.Length) ? readBuffer.Length : (int)numToRead;

				Array.Copy(readBuffer, 0, buffer, offset, sz);

				if (sz >= readBuffer.Length)
				{
					readBuffer = null;
				}
				else
				{
					int newLen = readBuffer.Length - sz;
					byte[] newBuf = new byte[newLen];
					Array.Copy(readBuffer, sz, newBuf, 0, newLen);
					readBuffer = newBuf;
				}

				totalRead += sz;
				numToRead -= sz;
				offset += sz;
			}

			while (numToRead > 0)
			{
				byte[] rec = tarBuffer.ReadBlock();
				if (rec == null)
				{
					throw new TarException("unexpected EOF with " + numToRead + " bytes unread");
				}

				var sz = (int)numToRead;
				int recLen = rec.Length;

				if (recLen > sz)
				{
					Array.Copy(rec, 0, buffer, offset, sz);
					readBuffer = new byte[recLen - sz];
					Array.Copy(rec, sz, readBuffer, 0, recLen - sz);
				}
				else
				{
					sz = recLen;
					Array.Copy(rec, 0, buffer, offset, recLen);
				}

				totalRead += sz;
				numToRead -= sz;
				offset += sz;
			}

			entryOffset += totalRead;

			return totalRead;
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				tarBuffer.Close();
			}
		}

		#endregion Stream Overrides

		public void SetEntryFactory(IEntryFactory factory)
		{
			entryFactory = factory;
		}

		public int RecordSize
		{
			get { return tarBuffer.RecordSize; }
		}

		[Obsolete("Use RecordSize property instead")]
		public int GetRecordSize()
		{
			return tarBuffer.RecordSize;
		}

		public long Available
		{
			get
			{
				return entrySize - entryOffset;
			}
		}

		public void Skip(long skipCount)
		{
			byte[] skipBuf = new byte[8 * 1024];

			for (long num = skipCount; num > 0;)
			{
				int toRead = num > skipBuf.Length ? skipBuf.Length : (int)num;
				int numRead = Read(skipBuf, 0, toRead);

				if (numRead == -1)
				{
					break;
				}

				num -= numRead;
			}
		}

		public bool IsMarkSupported
		{
			get
			{
				return false;
			}
		}

		public void Mark(int markLimit)
		{
		}

		public void Reset()
		{
		}

		public TarEntry GetNextEntry()
		{
			if (hasHitEOF)
			{
				return null;
			}

			if (currentEntry != null)
			{
				SkipToNextEntry();
			}

			byte[] headerBuf = tarBuffer.ReadBlock();

			if (headerBuf == null)
			{
				hasHitEOF = true;
			}
			else if (TarBuffer.IsEndOfArchiveBlock(headerBuf))
			{
				hasHitEOF = true;

				tarBuffer.ReadBlock();
			}
			else
			{
				hasHitEOF = false;
			}

			if (hasHitEOF)
			{
				currentEntry = null;
			}
			else
			{
				try
				{
					var header = new TarHeader();
					header.ParseBuffer(headerBuf, encoding);
					if (!header.IsChecksumValid)
					{
						throw new TarException("Header checksum is invalid");
					}
					this.entryOffset = 0;
					this.entrySize = header.Size;

					StringBuilder longName = null;

					if (header.TypeFlag == TarHeader.LF_GNU_LONGNAME)
					{
						byte[] nameBuffer = new byte[TarBuffer.BlockSize];
						long numToRead = this.entrySize;

						longName = new StringBuilder();

						while (numToRead > 0)
						{
							int numRead = this.Read(nameBuffer, 0, (numToRead > nameBuffer.Length ? nameBuffer.Length : (int)numToRead));

							if (numRead == -1)
							{
								throw new InvalidHeaderException("Failed to read long name entry");
							}

							longName.Append(TarHeader.ParseName(nameBuffer, 0, numRead, encoding).ToString());
							numToRead -= numRead;
						}

						SkipToNextEntry();
						headerBuf = this.tarBuffer.ReadBlock();
					}
					else if (header.TypeFlag == TarHeader.LF_GHDR)
					{  // POSIX global extended header
						SkipToNextEntry();
						headerBuf = this.tarBuffer.ReadBlock();
					}
					else if (header.TypeFlag == TarHeader.LF_XHDR)
					{  // POSIX extended header
						byte[] nameBuffer = new byte[TarBuffer.BlockSize];
						long numToRead = this.entrySize;

						var xhr = new TarExtendedHeaderReader();

						while (numToRead > 0)
						{
							int numRead = this.Read(nameBuffer, 0, (numToRead > nameBuffer.Length ? nameBuffer.Length : (int)numToRead));

							if (numRead == -1)
							{
								throw new InvalidHeaderException("Failed to read long name entry");
							}

							xhr.Read(nameBuffer, numRead);
							numToRead -= numRead;
						}

						if (xhr.Headers.TryGetValue("path", out string name))
						{
							longName = new StringBuilder(name);
						}

						SkipToNextEntry();
						headerBuf = this.tarBuffer.ReadBlock();
					}
					else if (header.TypeFlag == TarHeader.LF_GNU_VOLHDR)
					{
						SkipToNextEntry();
						headerBuf = this.tarBuffer.ReadBlock();
					}
					else if (header.TypeFlag != TarHeader.LF_NORMAL &&
							 header.TypeFlag != TarHeader.LF_OLDNORM &&
							 header.TypeFlag != TarHeader.LF_LINK &&
							 header.TypeFlag != TarHeader.LF_SYMLINK &&
							 header.TypeFlag != TarHeader.LF_DIR)
					{
						SkipToNextEntry();
						headerBuf = tarBuffer.ReadBlock();
					}

					if (entryFactory == null)
					{
						currentEntry = new TarEntry(headerBuf, encoding);
						if (longName != null)
						{
							currentEntry.Name = longName.ToString();
						}
					}
					else
					{
						currentEntry = entryFactory.CreateEntry(headerBuf);
					}


					entryOffset = 0;

					entrySize = this.currentEntry.Size;
				}
				catch (InvalidHeaderException ex)
				{
					entrySize = 0;
					entryOffset = 0;
					currentEntry = null;
					string errorText = string.Format("Bad header in record {0} block {1} {2}",
						tarBuffer.CurrentRecord, tarBuffer.CurrentBlock, ex.Message);
					throw new InvalidHeaderException(errorText);
				}
			}
			return currentEntry;
		}

		public void CopyEntryContents(Stream outputStream)
		{
			byte[] tempBuffer = new byte[32 * 1024];

			while (true)
			{
				int numRead = Read(tempBuffer, 0, tempBuffer.Length);
				if (numRead <= 0)
				{
					break;
				}
				outputStream.Write(tempBuffer, 0, numRead);
			}
		}

		private void SkipToNextEntry()
		{
			long numToSkip = entrySize - entryOffset;

			if (numToSkip > 0)
			{
				Skip(numToSkip);
			}

			readBuffer = null;
		}

		public interface IEntryFactory
		{
			TarEntry CreateEntry(string name);

			TarEntry CreateEntryFromFile(string fileName);

			TarEntry CreateEntry(byte[] headerBuffer);
		}

		public class EntryFactoryAdapter : IEntryFactory
		{
			Encoding nameEncoding;
			[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
			public EntryFactoryAdapter()
			{
			}
			public EntryFactoryAdapter(Encoding nameEncoding)
			{
				this.nameEncoding = nameEncoding;
			}
			public TarEntry CreateEntry(string name)
			{
				return TarEntry.CreateTarEntry(name);
			}

			public TarEntry CreateEntryFromFile(string fileName)
			{
				return TarEntry.CreateEntryFromFile(fileName);
			}

			public TarEntry CreateEntry(byte[] headerBuffer)
			{
				return new TarEntry(headerBuffer, nameEncoding);
			}
		}

		#region Instance Fields

		protected bool hasHitEOF;

		protected long entrySize;

		protected long entryOffset;

		protected byte[] readBuffer;

		protected TarBuffer tarBuffer;

		private TarEntry currentEntry;

		protected IEntryFactory entryFactory;

		private readonly Stream inputStream;

		private readonly Encoding encoding;

		#endregion Instance Fields
	}
}
