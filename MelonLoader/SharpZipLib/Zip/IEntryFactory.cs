using System;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using static MelonLoader.ICSharpCode.SharpZipLib.Zip.ZipEntryFactory;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public interface IEntryFactory
	{
		ZipEntry MakeFileEntry(string fileName);

		ZipEntry MakeFileEntry(string fileName, bool useFileSystem);

		ZipEntry MakeFileEntry(string fileName, string entryName, bool useFileSystem);

		ZipEntry MakeDirectoryEntry(string directoryName);

		ZipEntry MakeDirectoryEntry(string directoryName, bool useFileSystem);

		INameTransform NameTransform { get; set; }

		TimeSetting Setting { get; }

		DateTime FixedDateTime { get; }
	}
}
