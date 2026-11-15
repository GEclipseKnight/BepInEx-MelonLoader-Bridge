using System;
using System.Runtime.CompilerServices;

namespace MelonLoader.ICSharpCode.SharpZipLib.Checksum
{
	public sealed class Crc32 : IChecksum
	{
		#region Instance Fields

		private static readonly uint crcInit = 0xFFFFFFFF;
		private static readonly uint crcXor = 0xFFFFFFFF;

		private static readonly uint[] crcTable = CrcUtilities.GenerateSlicingLookupTable(0xEDB88320, isReversed: true);

		private uint checkValue;

		#endregion Instance Fields

		internal static uint ComputeCrc32(uint oldCrc, byte bval)
		{
			return (uint)(Crc32.crcTable[(oldCrc ^ bval) & 0xFF] ^ (oldCrc >> 8));
		}

		public Crc32()
		{
			Reset();
		}

		public void Reset()
		{
			checkValue = crcInit;
		}

		public long Value
		{
			get
			{
				return (long)(checkValue ^ crcXor);
			}
		}

		public void Update(int bval)
		{
			checkValue = unchecked(crcTable[(checkValue ^ bval) & 0xFF] ^ (checkValue >> 8));
		}

		public void Update(byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			Update(buffer, 0, buffer.Length);
		}

		public void Update(ArraySegment<byte> segment)
		{
			Update(segment.Array, segment.Offset, segment.Count);
		}

		private void Update(byte[] data, int offset, int count)
		{
			int remainder = count % CrcUtilities.SlicingDegree;
			int end = offset + count - remainder;

			while (offset != end)
			{
				checkValue = CrcUtilities.UpdateDataForReversedPoly(data, offset, crcTable, checkValue);
				offset += CrcUtilities.SlicingDegree;
			}

			if (remainder != 0)
			{
				SlowUpdateLoop(data, offset, end + remainder);
			}
		}

		[MethodImpl(MethodImplOptions.NoInlining)]
		private void SlowUpdateLoop(byte[] data, int offset, int end)
		{
			while (offset != end)
			{
				Update(data[offset++]);
			}
		}
	}
}
