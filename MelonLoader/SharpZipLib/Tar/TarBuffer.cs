using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.Tar
{
	public class TarBuffer
	{
		/* A quote from GNU tar man file on blocking and records
		   A `tar' archive file contains a series of blocks.  Each block
		contains `BLOCKSIZE' bytes.  Although this format may be thought of as
		being on magnetic tape, other media are often used.

		   Each file archived is represented by a header block which describes
		the file, followed by zero or more blocks which give the contents of
		the file.  At the end of the archive file there may be a block filled
		with binary zeros as an end-of-file marker.  A reasonable system should
		write a block of zeros at the end, but must not assume that such a
		block exists when reading an archive.

		   The blocks may be "blocked" for physical I/O operations.  Each
		record of N blocks is written with a single 'write ()'
		operation.  On magnetic tapes, the result of such a write is a single
		record.  When writing an archive, the last record of blocks should be
		written at the full size, with blocks after the zero block containing
		all zeros.  When reading an archive, a reasonable system should
		properly handle an archive whose last record is shorter than the rest,
		or which contains garbage records after a zero block.
		*/

		#region Constants

		public const int BlockSize = 512;

		public const int DefaultBlockFactor = 20;

		public const int DefaultRecordSize = BlockSize * DefaultBlockFactor;

		#endregion Constants

		public int RecordSize
		{
			get
			{
				return recordSize;
			}
		}

		[Obsolete("Use RecordSize property instead")]
		public int GetRecordSize()
		{
			return recordSize;
		}

		public int BlockFactor
		{
			get
			{
				return blockFactor;
			}
		}

		[Obsolete("Use BlockFactor property instead")]
		public int GetBlockFactor()
		{
			return blockFactor;
		}

		protected TarBuffer()
		{
		}

		public static TarBuffer CreateInputTarBuffer(Stream inputStream)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			return CreateInputTarBuffer(inputStream, DefaultBlockFactor);
		}

		public static TarBuffer CreateInputTarBuffer(Stream inputStream, int blockFactor)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			if (blockFactor <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(blockFactor), "Factor cannot be negative");
			}

			var tarBuffer = new TarBuffer();
			tarBuffer.inputStream = inputStream;
			tarBuffer.outputStream = null;
			tarBuffer.Initialize(blockFactor);

			return tarBuffer;
		}

		public static TarBuffer CreateOutputTarBuffer(Stream outputStream)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException(nameof(outputStream));
			}

			return CreateOutputTarBuffer(outputStream, DefaultBlockFactor);
		}

		public static TarBuffer CreateOutputTarBuffer(Stream outputStream, int blockFactor)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException(nameof(outputStream));
			}

			if (blockFactor <= 0)
			{
				throw new ArgumentOutOfRangeException(nameof(blockFactor), "Factor cannot be negative");
			}

			var tarBuffer = new TarBuffer();
			tarBuffer.inputStream = null;
			tarBuffer.outputStream = outputStream;
			tarBuffer.Initialize(blockFactor);

			return tarBuffer;
		}

		private void Initialize(int archiveBlockFactor)
		{
			blockFactor = archiveBlockFactor;
			recordSize = archiveBlockFactor * BlockSize;
			recordBuffer = new byte[RecordSize];

			if (inputStream != null)
			{
				currentRecordIndex = -1;
				currentBlockIndex = BlockFactor;
			}
			else
			{
				currentRecordIndex = 0;
				currentBlockIndex = 0;
			}
		}

		[Obsolete("Use IsEndOfArchiveBlock instead")]
		public bool IsEOFBlock(byte[] block)
		{
			if (block == null)
			{
				throw new ArgumentNullException(nameof(block));
			}

			if (block.Length != BlockSize)
			{
				throw new ArgumentException("block length is invalid");
			}

			for (int i = 0; i < BlockSize; ++i)
			{
				if (block[i] != 0)
				{
					return false;
				}
			}

			return true;
		}

		public static bool IsEndOfArchiveBlock(byte[] block)
		{
			if (block == null)
			{
				throw new ArgumentNullException(nameof(block));
			}

			if (block.Length != BlockSize)
			{
				throw new ArgumentException("block length is invalid");
			}

			for (int i = 0; i < BlockSize; ++i)
			{
				if (block[i] != 0)
				{
					return false;
				}
			}

			return true;
		}

		public void SkipBlock()
		{
			if (inputStream == null)
			{
				throw new TarException("no input stream defined");
			}

			if (currentBlockIndex >= BlockFactor)
			{
				if (!ReadRecord())
				{
					throw new TarException("Failed to read a record");
				}
			}

			currentBlockIndex++;
		}

		public byte[] ReadBlock()
		{
			if (inputStream == null)
			{
				throw new TarException("TarBuffer.ReadBlock - no input stream defined");
			}

			if (currentBlockIndex >= BlockFactor)
			{
				if (!ReadRecord())
				{
					throw new TarException("Failed to read a record");
				}
			}

			byte[] result = new byte[BlockSize];

			Array.Copy(recordBuffer, (currentBlockIndex * BlockSize), result, 0, BlockSize);
			currentBlockIndex++;
			return result;
		}

		private bool ReadRecord()
		{
			if (inputStream == null)
			{
				throw new TarException("no input stream defined");
			}

			currentBlockIndex = 0;

			int offset = 0;
			int bytesNeeded = RecordSize;

			while (bytesNeeded > 0)
			{
				long numBytes = inputStream.Read(recordBuffer, offset, bytesNeeded);

				if (numBytes <= 0)
				{
					break;
				}

				offset += (int)numBytes;
				bytesNeeded -= (int)numBytes;
			}

			currentRecordIndex++;
			return true;
		}

		public int CurrentBlock
		{
			get { return currentBlockIndex; }
		}

		public bool IsStreamOwner { get; set; } = true;

		[Obsolete("Use CurrentBlock property instead")]
		public int GetCurrentBlockNum()
		{
			return currentBlockIndex;
		}

		public int CurrentRecord
		{
			get { return currentRecordIndex; }
		}

		[Obsolete("Use CurrentRecord property instead")]
		public int GetCurrentRecordNum()
		{
			return currentRecordIndex;
		}

		public void WriteBlock(byte[] block)
		{
			if (block == null)
			{
				throw new ArgumentNullException(nameof(block));
			}

			if (outputStream == null)
			{
				throw new TarException("TarBuffer.WriteBlock - no output stream defined");
			}

			if (block.Length != BlockSize)
			{
				string errorText = string.Format("TarBuffer.WriteBlock - block to write has length '{0}' which is not the block size of '{1}'",
					block.Length, BlockSize);
				throw new TarException(errorText);
			}

			if (currentBlockIndex >= BlockFactor)
			{
				WriteRecord();
			}

			Array.Copy(block, 0, recordBuffer, (currentBlockIndex * BlockSize), BlockSize);
			currentBlockIndex++;
		}

		public void WriteBlock(byte[] buffer, int offset)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (outputStream == null)
			{
				throw new TarException("TarBuffer.WriteBlock - no output stream defined");
			}

			if ((offset < 0) || (offset >= buffer.Length))
			{
				throw new ArgumentOutOfRangeException(nameof(offset));
			}

			if ((offset + BlockSize) > buffer.Length)
			{
				string errorText = string.Format("TarBuffer.WriteBlock - record has length '{0}' with offset '{1}' which is less than the record size of '{2}'",
					buffer.Length, offset, recordSize);
				throw new TarException(errorText);
			}

			if (currentBlockIndex >= BlockFactor)
			{
				WriteRecord();
			}

			Array.Copy(buffer, offset, recordBuffer, (currentBlockIndex * BlockSize), BlockSize);

			currentBlockIndex++;
		}

		private void WriteRecord()
		{
			if (outputStream == null)
			{
				throw new TarException("TarBuffer.WriteRecord no output stream defined");
			}

			outputStream.Write(recordBuffer, 0, RecordSize);
			outputStream.Flush();

			currentBlockIndex = 0;
			currentRecordIndex++;
		}

		private void WriteFinalRecord()
		{
			if (outputStream == null)
			{
				throw new TarException("TarBuffer.WriteFinalRecord no output stream defined");
			}

			if (currentBlockIndex > 0)
			{
				int dataBytes = currentBlockIndex * BlockSize;
				Array.Clear(recordBuffer, dataBytes, RecordSize - dataBytes);
				WriteRecord();
			}

			outputStream.Flush();
		}

		public void Close()
		{
			if (outputStream != null)
			{
				WriteFinalRecord();

				if (IsStreamOwner)
				{
					outputStream.Dispose();
				}
				outputStream = null;
			}
			else if (inputStream != null)
			{
				if (IsStreamOwner)
				{
					inputStream.Dispose();
				}
				inputStream = null;
			}
		}

		#region Instance Fields

		private Stream inputStream;
		private Stream outputStream;

		private byte[] recordBuffer;
		private int currentBlockIndex;
		private int currentRecordIndex;

		private int recordSize = DefaultRecordSize;
		private int blockFactor = DefaultBlockFactor;

		#endregion Instance Fields
	}
}
