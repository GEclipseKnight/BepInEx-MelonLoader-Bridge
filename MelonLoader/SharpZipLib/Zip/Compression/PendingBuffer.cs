namespace MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression
{
	public class PendingBuffer
	{
		#region Instance Fields

		private readonly byte[] buffer;

		private int start;
		private int end;

		private uint bits;
		private int bitCount;

		#endregion Instance Fields

		#region Constructors

		public PendingBuffer() : this(4096)
		{
		}

		public PendingBuffer(int bufferSize)
		{
			buffer = new byte[bufferSize];
		}

		#endregion Constructors

		public void Reset()
		{
			start = end = bitCount = 0;
		}

		public void WriteByte(int value)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			buffer[end++] = unchecked((byte)value);
		}

		public void WriteShort(int value)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			buffer[end++] = unchecked((byte)value);
			buffer[end++] = unchecked((byte)(value >> 8));
		}

		public void WriteInt(int value)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			buffer[end++] = unchecked((byte)value);
			buffer[end++] = unchecked((byte)(value >> 8));
			buffer[end++] = unchecked((byte)(value >> 16));
			buffer[end++] = unchecked((byte)(value >> 24));
		}

		public void WriteBlock(byte[] block, int offset, int length)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			System.Array.Copy(block, offset, buffer, end, length);
			end += length;
		}

		public int BitCount
		{
			get
			{
				return bitCount;
			}
		}

		public void AlignToByte()
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			if (bitCount > 0)
			{
				buffer[end++] = unchecked((byte)bits);
				if (bitCount > 8)
				{
					buffer[end++] = unchecked((byte)(bits >> 8));
				}
			}
			bits = 0;
			bitCount = 0;
		}

		public void WriteBits(int b, int count)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}

#endif
			bits |= (uint)(b << bitCount);
			bitCount += count;
			if (bitCount >= 16)
			{
				buffer[end++] = unchecked((byte)bits);
				buffer[end++] = unchecked((byte)(bits >> 8));
				bits >>= 16;
				bitCount -= 16;
			}
		}

		public void WriteShortMSB(int s)
		{
#if DebugDeflation
			if (DeflaterConstants.DEBUGGING && (start != 0) )
			{
				throw new SharpZipBaseException("Debug check: start != 0");
			}
#endif
			buffer[end++] = unchecked((byte)(s >> 8));
			buffer[end++] = unchecked((byte)s);
		}

		public bool IsFlushed
		{
			get
			{
				return end == 0;
			}
		}

		public int Flush(byte[] output, int offset, int length)
		{
			if (bitCount >= 8)
			{
				buffer[end++] = unchecked((byte)bits);
				bits >>= 8;
				bitCount -= 8;
			}

			if (length > end - start)
			{
				length = end - start;
				System.Array.Copy(buffer, start, output, offset, length);
				start = 0;
				end = 0;
			}
			else
			{
				System.Array.Copy(buffer, start, output, offset, length);
				start += length;
			}
			return length;
		}

		public byte[] ToByteArray()
		{
			AlignToByte();

			byte[] result = new byte[end - start];
			System.Array.Copy(buffer, start, result, 0, result.Length);
			start = 0;
			end = 0;
			return result;
		}
	}
}
