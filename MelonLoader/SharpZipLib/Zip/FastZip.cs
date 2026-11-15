using MelonLoader.ICSharpCode.SharpZipLib.Core;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression;
using System;
using System.IO;
using static MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Deflater;
using static MelonLoader.ICSharpCode.SharpZipLib.Zip.ZipEntryFactory;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public class FastZipEvents
	{
		public event EventHandler<DirectoryEventArgs> ProcessDirectory;

		public ProcessFileHandler ProcessFile;

		public ProgressHandler Progress;

		public CompletedFileHandler CompletedFile;

		public DirectoryFailureHandler DirectoryFailure;

		public FileFailureHandler FileFailure;

		public bool OnDirectoryFailure(string directory, Exception e)
		{
			bool result = false;
			DirectoryFailureHandler handler = DirectoryFailure;

			if (handler != null)
			{
				var args = new ScanFailureEventArgs(directory, e);
				handler(this, args);
				result = args.ContinueRunning;
			}
			return result;
		}

		public bool OnFileFailure(string file, Exception e)
		{
			FileFailureHandler handler = FileFailure;
			bool result = (handler != null);

			if (result)
			{
				var args = new ScanFailureEventArgs(file, e);
				handler(this, args);
				result = args.ContinueRunning;
			}
			return result;
		}

		public bool OnProcessFile(string file)
		{
			bool result = true;
			ProcessFileHandler handler = ProcessFile;

			if (handler != null)
			{
				var args = new ScanEventArgs(file);
				handler(this, args);
				result = args.ContinueRunning;
			}
			return result;
		}

		public bool OnCompletedFile(string file)
		{
			bool result = true;
			CompletedFileHandler handler = CompletedFile;
			if (handler != null)
			{
				var args = new ScanEventArgs(file);
				handler(this, args);
				result = args.ContinueRunning;
			}
			return result;
		}

		public bool OnProcessDirectory(string directory, bool hasMatchingFiles)
		{
			bool result = true;
			EventHandler<DirectoryEventArgs> handler = ProcessDirectory;
			if (handler != null)
			{
				var args = new DirectoryEventArgs(directory, hasMatchingFiles);
				handler(this, args);
				result = args.ContinueRunning;
			}
			return result;
		}

		public TimeSpan ProgressInterval
		{
			get { return progressInterval_; }
			set { progressInterval_ = value; }
		}

		#region Instance Fields

		private TimeSpan progressInterval_ = TimeSpan.FromSeconds(3);

		#endregion Instance Fields
	}

	public class FastZip
	{
		#region Enumerations

		public enum Overwrite
		{
			Prompt,

			Never,

			Always
		}

		#endregion Enumerations

		#region Constructors

		public FastZip()
		{
		}

		public FastZip(TimeSetting timeSetting)
		{
			entryFactory_ = new ZipEntryFactory(timeSetting);
			restoreDateTimeOnExtract_ = true;
		}

		public FastZip(DateTime time)
		{
			entryFactory_ = new ZipEntryFactory(time);
			restoreDateTimeOnExtract_ = true;
		}

		public FastZip(FastZipEvents events)
		{
			events_ = events;
		}

		#endregion Constructors

		#region Properties

		public bool CreateEmptyDirectories
		{
			get { return createEmptyDirectories_; }
			set { createEmptyDirectories_ = value; }
		}

		public string Password
		{
			get { return password_; }
			set { password_ = value; }
		}

		public ZipEncryptionMethod EntryEncryptionMethod { get; set; } = ZipEncryptionMethod.ZipCrypto;

		public INameTransform NameTransform
		{
			get { return entryFactory_.NameTransform; }
			set
			{
				entryFactory_.NameTransform = value;
			}
		}

		public IEntryFactory EntryFactory
		{
			get { return entryFactory_; }
			set
			{
				if (value == null)
				{
					entryFactory_ = new ZipEntryFactory();
				}
				else
				{
					entryFactory_ = value;
				}
			}
		}

		public UseZip64 UseZip64
		{
			get { return useZip64_; }
			set { useZip64_ = value; }
		}

		public bool RestoreDateTimeOnExtract
		{
			get
			{
				return restoreDateTimeOnExtract_;
			}
			set
			{
				restoreDateTimeOnExtract_ = value;
			}
		}

		public bool RestoreAttributesOnExtract
		{
			get { return restoreAttributesOnExtract_; }
			set { restoreAttributesOnExtract_ = value; }
		}

		public Deflater.CompressionLevel CompressionLevel
		{
			get { return compressionLevel_; }
			set { compressionLevel_ = value; }
		}

		#endregion Properties

		#region Delegates

		public delegate bool ConfirmOverwriteDelegate(string fileName);

		#endregion Delegates

		#region CreateZip

		public void CreateZip(string zipFileName, string sourceDirectory,
			bool recurse, string fileFilter, string directoryFilter)
		{
			CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, directoryFilter);
		}

		public void CreateZip(string zipFileName, string sourceDirectory, bool recurse, string fileFilter)
		{
			CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, null);
		}

		public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, string fileFilter, string directoryFilter)
		{
			CreateZip(outputStream, sourceDirectory, recurse, fileFilter, directoryFilter, false);
		}

		public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, string fileFilter, string directoryFilter, bool leaveOpen)
		{
			var scanner = new FileSystemScanner(fileFilter, directoryFilter);
			CreateZip(outputStream, sourceDirectory, recurse, scanner, leaveOpen);
		}

		public void CreateZip(string zipFileName, string sourceDirectory,
			bool recurse, IScanFilter fileFilter, IScanFilter directoryFilter)
		{
			CreateZip(File.Create(zipFileName), sourceDirectory, recurse, fileFilter, directoryFilter, false);
		}

		public void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, IScanFilter fileFilter, IScanFilter directoryFilter, bool leaveOpen = false)
		{
			var scanner = new FileSystemScanner(fileFilter, directoryFilter);
			CreateZip(outputStream, sourceDirectory, recurse, scanner, leaveOpen);
		}

		private void CreateZip(Stream outputStream, string sourceDirectory, bool recurse, FileSystemScanner scanner, bool leaveOpen)
		{
			NameTransform = new ZipNameTransform(sourceDirectory);
			sourceDirectory_ = sourceDirectory;

			using (outputStream_ = new ZipOutputStream(outputStream))
			{
				outputStream_.SetLevel((int)CompressionLevel);
				outputStream_.IsStreamOwner = !leaveOpen;
				outputStream_.NameTransform = null; // all required transforms handled by us

				if (false == string.IsNullOrEmpty(password_) && EntryEncryptionMethod != ZipEncryptionMethod.None)
				{
					outputStream_.Password = password_;
				}

				outputStream_.UseZip64 = UseZip64;
				scanner.ProcessFile += ProcessFile;
				if (this.CreateEmptyDirectories)
				{
					scanner.ProcessDirectory += ProcessDirectory;
				}

				if (events_ != null)
				{
					if (events_.FileFailure != null)
					{
						scanner.FileFailure += events_.FileFailure;
					}

					if (events_.DirectoryFailure != null)
					{
						scanner.DirectoryFailure += events_.DirectoryFailure;
					}
				}

				scanner.Scan(sourceDirectory, recurse);
			}
		}

		#endregion CreateZip

		#region ExtractZip

		public void ExtractZip(string zipFileName, string targetDirectory, string fileFilter)
		{
			ExtractZip(zipFileName, targetDirectory, Overwrite.Always, null, fileFilter, null, restoreDateTimeOnExtract_);
		}

		public void ExtractZip(string zipFileName, string targetDirectory,
							   Overwrite overwrite, ConfirmOverwriteDelegate confirmDelegate,
							   string fileFilter, string directoryFilter, bool restoreDateTime, bool allowParentTraversal = false)
		{
			Stream inputStream = File.Open(zipFileName, FileMode.Open, FileAccess.Read, FileShare.Read);
			ExtractZip(inputStream, targetDirectory, overwrite, confirmDelegate, fileFilter, directoryFilter, restoreDateTime, true, allowParentTraversal);
		}

		public void ExtractZip(Stream inputStream, string targetDirectory,
					   Overwrite overwrite, ConfirmOverwriteDelegate confirmDelegate,
					   string fileFilter, string directoryFilter, bool restoreDateTime,
					   bool isStreamOwner, bool allowParentTraversal = false)
		{
			if ((overwrite == Overwrite.Prompt) && (confirmDelegate == null))
			{
				throw new ArgumentNullException(nameof(confirmDelegate));
			}

			continueRunning_ = true;
			overwrite_ = overwrite;
			confirmDelegate_ = confirmDelegate;
			extractNameTransform_ = new WindowsNameTransform(targetDirectory, allowParentTraversal);

			fileFilter_ = new NameFilter(fileFilter);
			directoryFilter_ = new NameFilter(directoryFilter);
			restoreDateTimeOnExtract_ = restoreDateTime;

			using (zipFile_ = new ZipFile(inputStream, !isStreamOwner))
			{
				if (password_ != null)
				{
					zipFile_.Password = password_;
				}

				System.Collections.IEnumerator enumerator = zipFile_.GetEnumerator();
				while (continueRunning_ && enumerator.MoveNext())
				{
					var entry = (ZipEntry)enumerator.Current;
					if (entry.IsFile)
					{
						if (directoryFilter_.IsMatch(Path.GetDirectoryName(entry.Name)) && fileFilter_.IsMatch(entry.Name))
						{
							ExtractEntry(entry);
						}
					}
					else if (entry.IsDirectory)
					{
						if (directoryFilter_.IsMatch(entry.Name) && CreateEmptyDirectories)
						{
							ExtractEntry(entry);
						}
					}
					else
					{
					}
				}
			}
		}

		#endregion ExtractZip

		#region Internal Processing

		private void ProcessDirectory(object sender, DirectoryEventArgs e)
		{
			if (!e.HasMatchingFiles && CreateEmptyDirectories)
			{
				if (events_ != null)
				{
					events_.OnProcessDirectory(e.Name, e.HasMatchingFiles);
				}

				if (e.ContinueRunning)
				{
					if (e.Name != sourceDirectory_)
					{
						ZipEntry entry = entryFactory_.MakeDirectoryEntry(e.Name);
						outputStream_.PutNextEntry(entry);
					}
				}
			}
		}

		private void ProcessFile(object sender, ScanEventArgs e)
		{
			if ((events_ != null) && (events_.ProcessFile != null))
			{
				events_.ProcessFile(sender, e);
			}

			if (e.ContinueRunning)
			{
				try
				{
					using (FileStream stream = File.Open(e.Name, FileMode.Open, FileAccess.Read, FileShare.Read))
					{
						ZipEntry entry = entryFactory_.MakeFileEntry(e.Name);

						ConfigureEntryEncryption(entry);

						outputStream_.PutNextEntry(entry);
						AddFileContents(e.Name, stream);
					}
				}
				catch (Exception ex)
				{
					if (events_ != null)
					{
						continueRunning_ = events_.OnFileFailure(e.Name, ex);
					}
					else
					{
						continueRunning_ = false;
						throw;
					}
				}
			}
		}

		private void ConfigureEntryEncryption(ZipEntry entry)
		{
			if (!string.IsNullOrEmpty(Password) && entry.AESEncryptionStrength == 0)
			{
				switch (EntryEncryptionMethod)
				{
					case ZipEncryptionMethod.AES128:
						entry.AESKeySize = 128;
						break;

					case ZipEncryptionMethod.AES256:
						entry.AESKeySize = 256;
						break;
				}
			}
		}

		private void AddFileContents(string name, Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			if (buffer_ == null)
			{
				buffer_ = new byte[4096];
			}

			if ((events_ != null) && (events_.Progress != null))
			{
				StreamUtils.Copy(stream, outputStream_, buffer_,
					events_.Progress, events_.ProgressInterval, this, name);
			}
			else
			{
				StreamUtils.Copy(stream, outputStream_, buffer_);
			}

			if (events_ != null)
			{
				continueRunning_ = events_.OnCompletedFile(name);
			}
		}

		private void ExtractFileEntry(ZipEntry entry, string targetName)
		{
			bool proceed = true;
			if (overwrite_ != Overwrite.Always)
			{
				if (File.Exists(targetName))
				{
					if ((overwrite_ == Overwrite.Prompt) && (confirmDelegate_ != null))
					{
						proceed = confirmDelegate_(targetName);
					}
					else
					{
						proceed = false;
					}
				}
			}

			if (proceed)
			{
				if (events_ != null)
				{
					continueRunning_ = events_.OnProcessFile(entry.Name);
				}

				if (continueRunning_)
				{
					try
					{
						using (FileStream outputStream = File.Create(targetName))
						{
							if (buffer_ == null)
							{
								buffer_ = new byte[4096];
							}

							using (var inputStream = zipFile_.GetInputStream(entry))
							{
								if ((events_ != null) && (events_.Progress != null))
								{
									StreamUtils.Copy(inputStream, outputStream, buffer_,
										events_.Progress, events_.ProgressInterval, this, entry.Name, entry.Size);
								}
								else
								{
									StreamUtils.Copy(inputStream, outputStream, buffer_);
								}
							}

							if (events_ != null)
							{
								continueRunning_ = events_.OnCompletedFile(entry.Name);
							}
						}

						if (restoreDateTimeOnExtract_)
						{
							switch (entryFactory_.Setting)
							{
								case TimeSetting.CreateTime:
									File.SetCreationTime(targetName, entry.DateTime);
									break;

								case TimeSetting.CreateTimeUtc:
									File.SetCreationTimeUtc(targetName, entry.DateTime);
									break;

								case TimeSetting.LastAccessTime:
									File.SetLastAccessTime(targetName, entry.DateTime);
									break;

								case TimeSetting.LastAccessTimeUtc:
									File.SetLastAccessTimeUtc(targetName, entry.DateTime);
									break;

								case TimeSetting.LastWriteTime:
									File.SetLastWriteTime(targetName, entry.DateTime);
									break;

								case TimeSetting.LastWriteTimeUtc:
									File.SetLastWriteTimeUtc(targetName, entry.DateTime);
									break;

								case TimeSetting.Fixed:
									File.SetLastWriteTime(targetName, entryFactory_.FixedDateTime);
									break;

								default:
									throw new ZipException("Unhandled time setting in ExtractFileEntry");
							}
						}

						if (RestoreAttributesOnExtract && entry.IsDOSEntry && (entry.ExternalFileAttributes != -1))
						{
							var fileAttributes = (FileAttributes)entry.ExternalFileAttributes;
							fileAttributes &= (FileAttributes.Archive | FileAttributes.Normal | FileAttributes.ReadOnly | FileAttributes.Hidden);
							File.SetAttributes(targetName, fileAttributes);
						}
					}
					catch (Exception ex)
					{
						if (events_ != null)
						{
							continueRunning_ = events_.OnFileFailure(targetName, ex);
						}
						else
						{
							continueRunning_ = false;
							throw;
						}
					}
				}
			}
		}

		private void ExtractEntry(ZipEntry entry)
		{
			bool doExtraction = entry.IsCompressionMethodSupported();
			string targetName = entry.Name;

			if (doExtraction)
			{
				if (entry.IsFile)
				{
					targetName = extractNameTransform_.TransformFile(targetName);
				}
				else if (entry.IsDirectory)
				{
					targetName = extractNameTransform_.TransformDirectory(targetName);
				}

				doExtraction = !(string.IsNullOrEmpty(targetName));
			}


			string dirName = string.Empty;

			if (doExtraction)
			{
				if (entry.IsDirectory)
				{
					dirName = targetName;
				}
				else
				{
					dirName = Path.GetDirectoryName(Path.GetFullPath(targetName));
				}
			}

			if (doExtraction && !Directory.Exists(dirName))
			{
				if (!entry.IsDirectory || CreateEmptyDirectories)
				{
					try
					{
						continueRunning_ = events_?.OnProcessDirectory(dirName, true) ?? true;
						if (continueRunning_)
						{
							Directory.CreateDirectory(dirName);
							if (entry.IsDirectory && restoreDateTimeOnExtract_)
							{
								switch (entryFactory_.Setting)
								{
									case TimeSetting.CreateTime:
										Directory.SetCreationTime(dirName, entry.DateTime);
										break;

									case TimeSetting.CreateTimeUtc:
										Directory.SetCreationTimeUtc(dirName, entry.DateTime);
										break;

									case TimeSetting.LastAccessTime:
										Directory.SetLastAccessTime(dirName, entry.DateTime);
										break;

									case TimeSetting.LastAccessTimeUtc:
										Directory.SetLastAccessTimeUtc(dirName, entry.DateTime);
										break;

									case TimeSetting.LastWriteTime:
										Directory.SetLastWriteTime(dirName, entry.DateTime);
										break;

									case TimeSetting.LastWriteTimeUtc:
										Directory.SetLastWriteTimeUtc(dirName, entry.DateTime);
										break;

									case TimeSetting.Fixed:
										Directory.SetLastWriteTime(dirName, entryFactory_.FixedDateTime);
										break;

									default:
										throw new ZipException("Unhandled time setting in ExtractEntry");
								}
							}
						}
						else
						{
							doExtraction = false;
						}
					}
					catch (Exception ex)
					{
						doExtraction = false;
						if (events_ != null)
						{
							if (entry.IsDirectory)
							{
								continueRunning_ = events_.OnDirectoryFailure(targetName, ex);
							}
							else
							{
								continueRunning_ = events_.OnFileFailure(targetName, ex);
							}
						}
						else
						{
							continueRunning_ = false;
							throw;
						}
					}
				}
			}

			if (doExtraction && entry.IsFile)
			{
				ExtractFileEntry(entry, targetName);
			}
		}

		private static int MakeExternalAttributes(FileInfo info)
		{
			return (int)info.Attributes;
		}

		private static bool NameIsValid(string name)
		{
			return !string.IsNullOrEmpty(name) &&
				(name.IndexOfAny(Path.GetInvalidPathChars()) < 0);
		}

		#endregion Internal Processing

		#region Instance Fields

		private bool continueRunning_;
		private byte[] buffer_;
		private ZipOutputStream outputStream_;
		private ZipFile zipFile_;
		private string sourceDirectory_;
		private NameFilter fileFilter_;
		private NameFilter directoryFilter_;
		private Overwrite overwrite_;
		private ConfirmOverwriteDelegate confirmDelegate_;

		private bool restoreDateTimeOnExtract_;
		private bool restoreAttributesOnExtract_;
		private bool createEmptyDirectories_;
		private FastZipEvents events_;
		private IEntryFactory entryFactory_ = new ZipEntryFactory();
		private INameTransform extractNameTransform_;
		private UseZip64 useZip64_ = UseZip64.Dynamic;
		private CompressionLevel compressionLevel_ = CompressionLevel.DEFAULT_COMPRESSION;

		private string password_;

		#endregion Instance Fields
	}
}
