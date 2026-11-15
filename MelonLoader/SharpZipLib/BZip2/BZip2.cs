using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.BZip2
{
	public static class BZip2
	{
		public static void Decompress(Stream inStream, Stream outStream, bool isStreamOwner)
		{
			if (inStream == null)
				throw new ArgumentNullException(nameof(inStream));

			if (outStream == null)
				throw new ArgumentNullException(nameof(outStream));

			try
			{
				using (BZip2InputStream bzipInput = new BZip2InputStream(inStream))
				{
					bzipInput.IsStreamOwner = isStreamOwner;
					Core.StreamUtils.Copy(bzipInput, outStream, new byte[4096]);
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

		public static void Compress(Stream inStream, Stream outStream, bool isStreamOwner, int level)
		{
			if (inStream == null)
				throw new ArgumentNullException(nameof(inStream));

			if (outStream == null)
				throw new ArgumentNullException(nameof(outStream));

			try
			{
				using (BZip2OutputStream bzipOutput = new BZip2OutputStream(outStream, level))
				{
					bzipOutput.IsStreamOwner = isStreamOwner;
					Core.StreamUtils.Copy(inStream, bzipOutput, new byte[4096]);
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
