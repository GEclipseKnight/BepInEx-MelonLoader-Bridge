using System;
using System.Runtime.CompilerServices;

namespace MelonLoader.ICSharpCode.SharpZipLib.Checksum
{
	public sealed class BZip2Crc : IChecksum
	{
		#region Instance Fields

		private const uint crcInit = 0xFFFFFFFF;

		private static readonly uint[] crcTable = CrcUtilities.GenerateSlicingLookupTable(0x04C11DB7, isReversed: false);

		private uint checkValue;

		#endregion Instance Fields

		public BZip2Crc()
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
				return (long)(~checkValue);
			}
		}

		public void Update(int bval)
		{
			checkValue = unchecked(crcTable[(byte)(((checkValue >> 24) & 0xFF) ^ bval)] ^ (checkValue << 8));
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
				checkValue = CrcUtilities.UpdateDataForNormalPoly(data, offset, crcTable, checkValue);
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
