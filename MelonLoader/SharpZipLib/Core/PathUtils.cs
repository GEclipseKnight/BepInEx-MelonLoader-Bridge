using System;
using System.IO;
using System.Linq;

namespace MelonLoader.ICSharpCode.SharpZipLib.Core
{
	public static class PathUtils
	{
		public static string DropPathRoot(string path)
		{
			var invalidChars = Path.GetInvalidPathChars();
			var cleanRootSep = path.Length >= 3 && path[1] == ':' && path[2] == ':';
			
			var cleanPath = new string(path.Take(258)
				.Select( (c, i) => invalidChars.Contains(c) || (i == 2 && cleanRootSep) ? '_' : c).ToArray());

			var stripLength = Path.GetPathRoot(cleanPath).Length;
			while (path.Length > stripLength && (path[stripLength] == '/' || path[stripLength] == '\\')) stripLength++;
			return path.Substring(stripLength);
		}

		public static string GetTempFileName(string original = null)
		{
			string fileName;
			var tempPath = Path.GetTempPath();

			do
			{
				fileName = original == null
					? Path.Combine(tempPath, Path.GetRandomFileName())
					: $"{original}.{Path.GetRandomFileName()}";
			} while (File.Exists(fileName));

			return fileName;
		}
	}
}
