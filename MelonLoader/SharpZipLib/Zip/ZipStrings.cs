using System;
using System.Text;
using MelonLoader.ICSharpCode.SharpZipLib.Core;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public static class ZipStrings
	{
		static ZipStrings()
		{
			try
			{
				var platformCodepage = Encoding.GetEncoding(0).CodePage;
				SystemDefaultCodePage = (platformCodepage == 1 || platformCodepage == 2 || platformCodepage == 3 || platformCodepage == 42) ? FallbackCodePage : platformCodepage;
			}
			catch
			{
				SystemDefaultCodePage = FallbackCodePage;
			}
		}

		private static int codePage = AutomaticCodePage;

		private const int AutomaticCodePage = -1;

		public static int CodePage
		{
			get
			{
				return codePage == AutomaticCodePage? Encoding.UTF8.CodePage:codePage;
			}
			set
			{
				if ((value < 0) || (value > 65535) ||
					(value == 1) || (value == 2) || (value == 3) || (value == 42))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				codePage = value;
			}
		}

		private const int FallbackCodePage = 437;

		public static int SystemDefaultCodePage { get; }

		public static bool UseUnicode
		{
			get
			{
				return codePage == Encoding.UTF8.CodePage;
			}
			set
			{
				if (value)
				{
					codePage = Encoding.UTF8.CodePage;
				}
				else
				{
					codePage = SystemDefaultCodePage;
				}
			}
		}

		public static string ConvertToString(byte[] data, int count)
			=> data == null
			? string.Empty
			: Encoding.GetEncoding(CodePage).GetString(data, 0, count);

		public static string ConvertToString(byte[] data)
			=> ConvertToString(data, data.Length);

		private static Encoding EncodingFromFlag(int flags)
			=> ((flags & (int)GeneralBitFlags.UnicodeText) != 0)
				? Encoding.UTF8
				: Encoding.GetEncoding(
					codePage == AutomaticCodePage? 
						SystemDefaultCodePage:
						codePage);

		public static string ConvertToStringExt(int flags, byte[] data, int count)
			=> (data == null)
				? string.Empty
				: EncodingFromFlag(flags).GetString(data, 0, count);

		public static string ConvertToStringExt(int flags, byte[] data)
			=> ConvertToStringExt(flags, data, data.Length);

		public static byte[] ConvertToArray(string str)
			=> str == null
			? Empty.Array<byte>()
			: Encoding.GetEncoding(CodePage).GetBytes(str);

		public static byte[] ConvertToArray(int flags, string str)
			=> (string.IsNullOrEmpty(str))
				? Empty.Array<byte>()
				: EncodingFromFlag(flags).GetBytes(str);
	}
}
