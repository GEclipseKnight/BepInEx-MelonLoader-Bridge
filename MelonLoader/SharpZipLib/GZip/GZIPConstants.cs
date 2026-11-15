using System;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.GZip
{
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "kept for backwards compatibility")]
	sealed public class GZipConstants
	{
		public const byte ID1 = 0x1F;

		public const byte ID2 = 0x8B;

		public const byte CompressionMethodDeflate = 0x8;

		public static Encoding Encoding
		{
			get
			{
				try
				{
					return Encoding.GetEncoding(1252);
				}
				catch
				{
					return Encoding.ASCII;
				}
			}
		}

	}

	[Flags]
	public enum GZipFlags: byte
	{
		FTEXT = 0x1 << 0,

		FHCRC = 0x1 << 1,

		FEXTRA = 0x1 << 2,

		FNAME = 0x1 << 3,

		FCOMMENT = 0x1 << 4,
	}
}
