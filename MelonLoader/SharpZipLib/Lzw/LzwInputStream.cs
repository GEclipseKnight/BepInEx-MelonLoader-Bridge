using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.Lzw
{
	public class LzwInputStream : Stream
	{
		public bool IsStreamOwner { get; set; } = true;

		public LzwInputStream(Stream baseInputStream)
		{
			this.baseInputStream = baseInputStream;
		}

		public override int ReadByte()
		{
			int b = Read(one, 0, 1);
			if (b == 1)
				return (one[0] & 0xff);
			return -1;
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (!headerParsed)
				ParseHeader();

			if (eof)
				return 0;

			int start = offset;

			/* Using local copies of various variables speeds things up by as
	         * much as 30% in Java! Performance not tested in C#.
	         */
			int[] lTabPrefix = tabPrefix;
			byte[] lTabSuffix = tabSuffix;
			byte[] lStack = stack;
			int lNBits = nBits;
			int lMaxCode = maxCode;
			int lMaxMaxCode = maxMaxCode;
			int lBitMask = bitMask;
			int lOldCode = oldCode;
			byte lFinChar = finChar;
			int lStackP = stackP;
			int lFreeEnt = freeEnt;
			byte[] lData = data;
			int lBitPos = bitPos;

			int sSize = lStack.Length - lStackP;
			if (sSize > 0)
			{
				int num = (sSize >= count) ? count : sSize;
				Array.Copy(lStack, lStackP, buffer, offset, num);
				offset += num;
				count -= num;
				lStackP += num;
			}

			if (count == 0)
			{
				stackP = lStackP;
				return offset - start;
			}

		MainLoop:
			do
			{
				if (end < EXTRA)
				{
					Fill();
				}

				int bitIn = (got > 0) ? (end - end % lNBits) << 3 :
										(end << 3) - (lNBits - 1);

				while (lBitPos < bitIn)
				{
					#region A

					if (count == 0)
					{
						nBits = lNBits;
						maxCode = lMaxCode;
						maxMaxCode = lMaxMaxCode;
						bitMask = lBitMask;
						oldCode = lOldCode;
						finChar = lFinChar;
						stackP = lStackP;
						freeEnt = lFreeEnt;
						bitPos = lBitPos;

						return offset - start;
					}

					if (lFreeEnt > lMaxCode)
					{
						int nBytes = lNBits << 3;
						lBitPos = (lBitPos - 1) +
						nBytes - (lBitPos - 1 + nBytes) % nBytes;

						lNBits++;
						lMaxCode = (lNBits == maxBits) ? lMaxMaxCode :
														(1 << lNBits) - 1;

						lBitMask = (1 << lNBits) - 1;
						lBitPos = ResetBuf(lBitPos);
						goto MainLoop;
					}

					#endregion A

					#region B

					int pos = lBitPos >> 3;
					int code = (((lData[pos] & 0xFF) |
						((lData[pos + 1] & 0xFF) << 8) |
						((lData[pos + 2] & 0xFF) << 16)) >>
						(lBitPos & 0x7)) & lBitMask;

					lBitPos += lNBits;

					if (lOldCode == -1)
					{
						if (code >= 256)
							throw new LzwException("corrupt input: " + code + " > 255");

						lFinChar = (byte)(lOldCode = code);
						buffer[offset++] = lFinChar;
						count--;
						continue;
					}

					if (code == TBL_CLEAR && blockMode)
					{
						Array.Copy(zeros, 0, lTabPrefix, 0, zeros.Length);
						lFreeEnt = TBL_FIRST - 1;

						int nBytes = lNBits << 3;
						lBitPos = (lBitPos - 1) + nBytes - (lBitPos - 1 + nBytes) % nBytes;
						lNBits = LzwConstants.INIT_BITS;
						lMaxCode = (1 << lNBits) - 1;
						lBitMask = lMaxCode;


						lBitPos = ResetBuf(lBitPos);
						goto MainLoop;
					}

					#endregion B

					#region C

					int inCode = code;
					lStackP = lStack.Length;

					if (code >= lFreeEnt)
					{
						if (code > lFreeEnt)
						{
							throw new LzwException("corrupt input: code=" + code +
								", freeEnt=" + lFreeEnt);
						}

						lStack[--lStackP] = lFinChar;
						code = lOldCode;
					}

					while (code >= 256)
					{
						lStack[--lStackP] = lTabSuffix[code];
						code = lTabPrefix[code];
					}

					lFinChar = lTabSuffix[code];
					buffer[offset++] = lFinChar;
					count--;

					sSize = lStack.Length - lStackP;
					int num = (sSize >= count) ? count : sSize;
					Array.Copy(lStack, lStackP, buffer, offset, num);
					offset += num;
					count -= num;
					lStackP += num;

					#endregion C

					#region D

					if (lFreeEnt < lMaxMaxCode)
					{
						lTabPrefix[lFreeEnt] = lOldCode;
						lTabSuffix[lFreeEnt] = lFinChar;
						lFreeEnt++;
					}

					lOldCode = inCode;

					if (count == 0)
					{
						nBits = lNBits;
						maxCode = lMaxCode;
						bitMask = lBitMask;
						oldCode = lOldCode;
						finChar = lFinChar;
						stackP = lStackP;
						freeEnt = lFreeEnt;
						bitPos = lBitPos;

						return offset - start;
					}

					#endregion D
				}   // while

				lBitPos = ResetBuf(lBitPos);
			} while (got > 0);  // do..while

			nBits = lNBits;
			maxCode = lMaxCode;
			bitMask = lBitMask;
			oldCode = lOldCode;
			finChar = lFinChar;
			stackP = lStackP;
			freeEnt = lFreeEnt;
			bitPos = lBitPos;

			eof = true;
			return offset - start;
		}

		private int ResetBuf(int bitPosition)
		{
			int pos = bitPosition >> 3;
			Array.Copy(data, pos, data, 0, end - pos);
			end -= pos;
			return 0;
		}

		private void Fill()
		{
			got = baseInputStream.Read(data, end, data.Length - 1 - end);
			if (got > 0)
			{
				end += got;
			}
		}

		private void ParseHeader()
		{
			headerParsed = true;

			byte[] hdr = new byte[LzwConstants.HDR_SIZE];

			int result = baseInputStream.Read(hdr, 0, hdr.Length);

			if (result < 0)
				throw new LzwException("Failed to read LZW header");

			if (hdr[0] != (LzwConstants.MAGIC >> 8) || hdr[1] != (LzwConstants.MAGIC & 0xff))
			{
				throw new LzwException(String.Format(
					"Wrong LZW header. Magic bytes don't match. 0x{0:x2} 0x{1:x2}",
					hdr[0], hdr[1]));
			}

			blockMode = (hdr[2] & LzwConstants.BLOCK_MODE_MASK) > 0;
			maxBits = hdr[2] & LzwConstants.BIT_MASK;

			if (maxBits > LzwConstants.MAX_BITS)
			{
				throw new LzwException("Stream compressed with " + maxBits +
					" bits, but decompression can only handle " +
					LzwConstants.MAX_BITS + " bits.");
			}

			if ((hdr[2] & LzwConstants.RESERVED_MASK) > 0)
			{
				throw new LzwException("Unsupported bits set in the header.");
			}

			maxMaxCode = 1 << maxBits;
			nBits = LzwConstants.INIT_BITS;
			maxCode = (1 << nBits) - 1;
			bitMask = maxCode;
			oldCode = -1;
			finChar = 0;
			freeEnt = blockMode ? TBL_FIRST : 256;

			tabPrefix = new int[1 << maxBits];
			tabSuffix = new byte[1 << maxBits];
			stack = new byte[1 << maxBits];
			stackP = stack.Length;

			for (int idx = 255; idx >= 0; idx--)
				tabSuffix[idx] = (byte)idx;
		}

		#region Stream Overrides

		public override bool CanRead
		{
			get
			{
				return baseInputStream.CanRead;
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
				return got;
			}
		}

		public override long Position
		{
			get
			{
				return baseInputStream.Position;
			}
			set
			{
				throw new NotSupportedException("InflaterInputStream Position not supported");
			}
		}

		public override void Flush()
		{
			baseInputStream.Flush();
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException("Seek not supported");
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException("InflaterInputStream SetLength not supported");
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotSupportedException("InflaterInputStream Write not supported");
		}

		public override void WriteByte(byte value)
		{
			throw new NotSupportedException("InflaterInputStream WriteByte not supported");
		}

		protected override void Dispose(bool disposing)
		{
			if (!isClosed)
			{
				isClosed = true;
				if (IsStreamOwner)
				{
					baseInputStream.Dispose();
				}
			}
		}

		#endregion Stream Overrides

		#region Instance Fields

		private Stream baseInputStream;

		private bool isClosed;

		private readonly byte[] one = new byte[1];
		private bool headerParsed;

		private const int TBL_CLEAR = 0x100;

		private const int TBL_FIRST = TBL_CLEAR + 1;

		private int[] tabPrefix;
		private byte[] tabSuffix;
		private readonly int[] zeros = new int[256];
		private byte[] stack;

		private bool blockMode;

		private int nBits;
		private int maxBits;
		private int maxMaxCode;
		private int maxCode;
		private int bitMask;
		private int oldCode;
		private byte finChar;
		private int stackP;
		private int freeEnt;

		private readonly byte[] data = new byte[1024 * 8];

		private int bitPos;
		private int end;
		private int got;
		private bool eof;
		private const int EXTRA = 64;

		#endregion Instance Fields
	}
}
