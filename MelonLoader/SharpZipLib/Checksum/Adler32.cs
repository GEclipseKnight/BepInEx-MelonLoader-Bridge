using System;

namespace MelonLoader.ICSharpCode.SharpZipLib.Checksum
{
	public sealed class Adler32 : IChecksum
	{
		#region Instance Fields

		private static readonly uint BASE = 65521;

		private uint checkValue;

		#endregion Instance Fields

		public Adler32()
		{
			Reset();
		}

		public void Reset()
		{
			checkValue = 1;
		}

		public long Value
		{
			get
			{
				return checkValue;
			}
		}

		public void Update(int bval)
		{
			uint s1 = checkValue & 0xFFFF;
			uint s2 = checkValue >> 16;

			s1 = (s1 + ((uint)bval & 0xFF)) % BASE;
			s2 = (s1 + s2) % BASE;

			checkValue = (s2 << 16) + s1;
		}

		public void Update(byte[] buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			Update(new ArraySegment<byte>(buffer, 0, buffer.Length));
		}

		public void Update(ArraySegment<byte> segment)
		{
			uint s1 = checkValue & 0xFFFF;
			uint s2 = checkValue >> 16;
			var count = segment.Count;
			var offset = segment.Offset;
			while (count > 0)
			{
				int n = 3800;
				if (n > count)
				{
					n = count;
				}
				count -= n;
				while (--n >= 0)
				{
					s1 = s1 + (uint)(segment.Array[offset++] & 0xff);
					s2 = s2 + s1;
				}
				s1 %= BASE;
				s2 %= BASE;
			}
			checkValue = (s2 << 16) | s1;
		}
	}
}
