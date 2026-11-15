using System.Runtime.CompilerServices;

namespace MelonLoader.ICSharpCode.SharpZipLib.Checksum
{
	internal static class CrcUtilities
	{
		internal const int SlicingDegree = 16;

		internal static uint[] GenerateSlicingLookupTable(uint polynomial, bool isReversed)
		{
			var table = new uint[256 * SlicingDegree];
			uint one = isReversed ? 1 : (1U << 31);

			for (int i = 0; i < 256; i++)
			{
				uint res = (uint)(isReversed ? i : i << 24);
				for (int j = 0; j < SlicingDegree; j++)
				{
					for (int k = 0; k < 8; k++)
					{
						if (isReversed)
						{
							res = (res & one) == 1 ? polynomial ^ (res >> 1) : res >> 1;
						}
						else
						{
							res = (res & one) != 0 ? polynomial ^ (res << 1) : res << 1;
						}
					}

					table[(256 * j) + i] = res;
				}
			}

			return table;
		}

		internal static uint UpdateDataForNormalPoly(byte[] input, int offset, uint[] crcTable, uint checkValue)
		{
			byte x1 = (byte)((byte)(checkValue >> 24) ^ input[offset]);
			byte x2 = (byte)((byte)(checkValue >> 16) ^ input[offset + 1]);
			byte x3 = (byte)((byte)(checkValue >> 8) ^ input[offset + 2]);
			byte x4 = (byte)((byte)checkValue ^ input[offset + 3]);

			return UpdateDataCommon(input, offset, crcTable, x1, x2, x3, x4);
		}

		internal static uint UpdateDataForReversedPoly(byte[] input, int offset, uint[] crcTable, uint checkValue)
		{
			byte x1 = (byte)((byte)checkValue ^ input[offset]);
			byte x2 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 1]);
			byte x3 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 2]);
			byte x4 = (byte)((byte)(checkValue >>= 8) ^ input[offset + 3]);

			return UpdateDataCommon(input, offset, crcTable, x1, x2, x3, x4);
		}

		private static uint UpdateDataCommon(byte[] input, int offset, uint[] crcTable, byte x1, byte x2, byte x3, byte x4)
		{
			uint result;
			uint a1 = crcTable[x1 + 3840] ^ crcTable[x2 + 3584];
			uint a2 = crcTable[x3 + 3328] ^ crcTable[x4 + 3072];

			result = crcTable[input[offset + 4] + 2816];
			result ^= crcTable[input[offset + 5] + 2560];
			a1 ^= crcTable[input[offset + 9] + 1536];
			result ^= crcTable[input[offset + 6] + 2304];
			result ^= crcTable[input[offset + 7] + 2048];
			result ^= crcTable[input[offset + 8] + 1792];
			a2 ^= crcTable[input[offset + 13] + 512];
			result ^= crcTable[input[offset + 10] + 1280];
			result ^= crcTable[input[offset + 11] + 1024];
			result ^= crcTable[input[offset + 12] + 768];
			result ^= a1;
			result ^= crcTable[input[offset + 14] + 256];
			result ^= crcTable[input[offset + 15]];
			result ^= a2;

			return result;
		}
	}
}
