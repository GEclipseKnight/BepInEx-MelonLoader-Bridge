using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.GZip
{
	using static Zip.Compression.Deflater;

	public static class GZip
	{
		public static void Decompress(Stream inStream, Stream outStream, bool isStreamOwner)
		{
			if (inStream == null)
				throw new ArgumentNullException(nameof(inStream), "Input stream is null");

			if (outStream == null)
				throw new ArgumentNullException(nameof(outStream), "Output stream is null");

			try
			{
				using (GZipInputStream gzipInput = new GZipInputStream(inStream))
				{
					gzipInput.IsStreamOwner = isStreamOwner;
					Core.StreamUtils.Copy(gzipInput, outStream, new byte[4096]);
				}
			}
			finally
			{
				if (isStreamOwner)
				{
					outStream.Dispose();
				}
			}
		}

		public static void Compress(Stream inStream, Stream outStream, bool isStreamOwner, int bufferSize = 512, int level = 6)
		{
			if (inStream == null)
				throw new ArgumentNullException(nameof(inStream), "Input stream is null");

			if (outStream == null)
				throw new ArgumentNullException(nameof(outStream), "Output stream is null");

			if (bufferSize < 512)
				throw new ArgumentOutOfRangeException(nameof(bufferSize), "Deflate buffer size must be >= 512");

			if (level < NO_COMPRESSION || level > BEST_COMPRESSION)
				throw new ArgumentOutOfRangeException(nameof(level), "Compression level must be 0-9");

			try
			{
				using (GZipOutputStream gzipOutput = new GZipOutputStream(outStream, bufferSize))
				{
					gzipOutput.SetLevel(level);
					gzipOutput.IsStreamOwner = isStreamOwner;
					Core.StreamUtils.Copy(inStream, gzipOutput, new byte[bufferSize]);
				}
			}
			finally
			{
				if (isStreamOwner)
				{
					inStream.Dispose();
				}
			}
		}
	}
}
