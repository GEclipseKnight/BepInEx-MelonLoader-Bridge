using System;
using System.IO;
using System.Text;
using MelonLoader.ICSharpCode.SharpZipLib.Core;

namespace MelonLoader.ICSharpCode.SharpZipLib.Tar
{
	public delegate void ProgressMessageHandler(TarArchive archive, TarEntry entry, string message);

	public class TarArchive : IDisposable
	{
		public event ProgressMessageHandler ProgressMessageEvent;

		protected virtual void OnProgressMessageEvent(TarEntry entry, string message)
		{
			ProgressMessageHandler handler = ProgressMessageEvent;
			if (handler != null)
			{
				handler(this, entry, message);
			}
		}

		#region Constructors

		protected TarArchive()
		{
		}

		protected TarArchive(TarInputStream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			tarIn = stream;
		}

		protected TarArchive(TarOutputStream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			tarOut = stream;
		}

		#endregion Constructors

		#region Static factory methods

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public static TarArchive CreateInputTarArchive(Stream inputStream)
		{
			return CreateInputTarArchive(inputStream, null);
		}

		public static TarArchive CreateInputTarArchive(Stream inputStream, Encoding nameEncoding)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			var tarStream = inputStream as TarInputStream;

			TarArchive result;
			if (tarStream != null)
			{
				result = new TarArchive(tarStream);
			}
			else
			{
				result = CreateInputTarArchive(inputStream, TarBuffer.DefaultBlockFactor, nameEncoding);
			}
			return result;
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public static TarArchive CreateInputTarArchive(Stream inputStream, int blockFactor)
		{
			return CreateInputTarArchive(inputStream, blockFactor, null);
		}

		public static TarArchive CreateInputTarArchive(Stream inputStream, int blockFactor, Encoding nameEncoding)
		{
			if (inputStream == null)
			{
				throw new ArgumentNullException(nameof(inputStream));
			}

			if (inputStream is TarInputStream)
			{
				throw new ArgumentException("TarInputStream not valid");
			}

			return new TarArchive(new TarInputStream(inputStream, blockFactor, nameEncoding));
		}
		public static TarArchive CreateOutputTarArchive(Stream outputStream, Encoding nameEncoding)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException(nameof(outputStream));
			}

			var tarStream = outputStream as TarOutputStream;

			TarArchive result;
			if (tarStream != null)
			{
				result = new TarArchive(tarStream);
			}
			else
			{
				result = CreateOutputTarArchive(outputStream, TarBuffer.DefaultBlockFactor, nameEncoding);
			}
			return result;
		}
		public static TarArchive CreateOutputTarArchive(Stream outputStream)
		{
			return CreateOutputTarArchive(outputStream, null);
		}

		public static TarArchive CreateOutputTarArchive(Stream outputStream, int blockFactor)
		{
			return CreateOutputTarArchive(outputStream, blockFactor, null);
		}
		public static TarArchive CreateOutputTarArchive(Stream outputStream, int blockFactor, Encoding nameEncoding)
		{
			if (outputStream == null)
			{
				throw new ArgumentNullException(nameof(outputStream));
			}

			if (outputStream is TarOutputStream)
			{
				throw new ArgumentException("TarOutputStream is not valid");
			}

			return new TarArchive(new TarOutputStream(outputStream, blockFactor, nameEncoding));
		}

		#endregion Static factory methods

		public void SetKeepOldFiles(bool keepExistingFiles)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			keepOldFiles = keepExistingFiles;
		}

		public bool AsciiTranslate
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return asciiTranslate;
			}

			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				asciiTranslate = value;
			}
		}

		[Obsolete("Use the AsciiTranslate property")]
		public void SetAsciiTranslation(bool translateAsciiFiles)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			asciiTranslate = translateAsciiFiles;
		}

		public string PathPrefix
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return pathPrefix;
			}

			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				pathPrefix = value;
			}
		}

		public string RootPath
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return rootPath;
			}

			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}
				rootPath = value.Replace('\\', '/').TrimEnd('/');
			}
		}

		public void SetUserInfo(int userId, string userName, int groupId, string groupName)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			this.userId = userId;
			this.userName = userName;
			this.groupId = groupId;
			this.groupName = groupName;
			applyUserInfoOverrides = true;
		}

		public bool ApplyUserInfoOverrides
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return applyUserInfoOverrides;
			}

			set
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				applyUserInfoOverrides = value;
			}
		}

		public int UserId
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return userId;
			}
		}

		public string UserName
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return userName;
			}
		}

		public int GroupId
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return groupId;
			}
		}

		public string GroupName
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				return groupName;
			}
		}

		public int RecordSize
		{
			get
			{
				if (isDisposed)
				{
					throw new ObjectDisposedException("TarArchive");
				}

				if (tarIn != null)
				{
					return tarIn.RecordSize;
				}
				else if (tarOut != null)
				{
					return tarOut.RecordSize;
				}
				return TarBuffer.DefaultRecordSize;
			}
		}

		public bool IsStreamOwner
		{
			set
			{
				if (tarIn != null)
				{
					tarIn.IsStreamOwner = value;
				}
				else
				{
					tarOut.IsStreamOwner = value;
				}
			}
		}

		[Obsolete("Use Close instead")]
		public void CloseArchive()
		{
			Close();
		}

		public void ListContents()
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			while (true)
			{
				TarEntry entry = tarIn.GetNextEntry();

				if (entry == null)
				{
					break;
				}
				OnProgressMessageEvent(entry, null);
			}
		}

		public void ExtractContents(string destinationDirectory) 
			=> ExtractContents(destinationDirectory, false);

		public void ExtractContents(string destinationDirectory, bool allowParentTraversal)
		{
			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			var fullDistDir = Path.GetFullPath(destinationDirectory);

			while (true)
			{
				TarEntry entry = tarIn.GetNextEntry();

				if (entry == null)
				{
					break;
				}

				if (entry.TarHeader.TypeFlag == TarHeader.LF_LINK || entry.TarHeader.TypeFlag == TarHeader.LF_SYMLINK)
					continue;

				ExtractEntry(fullDistDir, entry, allowParentTraversal);
			}
		}

		private void ExtractEntry(string destDir, TarEntry entry, bool allowParentTraversal)
		{
			OnProgressMessageEvent(entry, null);

			string name = entry.Name;

			if (Path.IsPathRooted(name))
			{
				name = name.Substring(Path.GetPathRoot(name).Length);
			}

			name = name.Replace('/', Path.DirectorySeparatorChar);

			string destFile = Path.Combine(destDir, name);
			var destFileDir = Path.GetDirectoryName(Path.GetFullPath(destFile)) ?? "";

			if (!allowParentTraversal && !destFileDir.StartsWith(destDir, StringComparison.InvariantCultureIgnoreCase))
			{
				throw new InvalidNameException("Parent traversal in paths is not allowed");
			}

			if (entry.IsDirectory)
			{
				EnsureDirectoryExists(destFile);
			}
			else
			{
				string parentDirectory = Path.GetDirectoryName(destFile);
				EnsureDirectoryExists(parentDirectory);

				bool process = true;
				var fileInfo = new FileInfo(destFile);
				if (fileInfo.Exists)
				{
					if (keepOldFiles)
					{
						OnProgressMessageEvent(entry, "Destination file already exists");
						process = false;
					}
					else if ((fileInfo.Attributes & FileAttributes.ReadOnly) != 0)
					{
						OnProgressMessageEvent(entry, "Destination file already exists, and is read-only");
						process = false;
					}
				}

				if (process)
				{
					using (var outputStream = File.Create(destFile))
					{
						if (this.asciiTranslate)
						{
							ExtractAndTranslateEntry(destFile, outputStream);
						}
						else
						{
							tarIn.CopyEntryContents(outputStream);
						}
					}
				}
			}
		}

		private void ExtractAndTranslateEntry(string destFile, Stream outputStream)
		{
			bool asciiTrans = !IsBinary(destFile);

			if (asciiTrans)
			{
				using (var outw = new StreamWriter(outputStream, new UTF8Encoding(false), 1024))
				{
					byte[] rdbuf = new byte[32 * 1024];

					while (true)
					{
						int numRead = tarIn.Read(rdbuf, 0, rdbuf.Length);

						if (numRead <= 0)
						{
							break;
						}

						for (int off = 0, b = 0; b < numRead; ++b)
						{
							if (rdbuf[b] == 10)
							{
								string s = Encoding.ASCII.GetString(rdbuf, off, (b - off));
								outw.WriteLine(s);
								off = b + 1;
							}
						}
					}
				}
			}
			else
			{
				tarIn.CopyEntryContents(outputStream);
			}
		}

		public void WriteEntry(TarEntry sourceEntry, bool recurse)
		{
			if (sourceEntry == null)
			{
				throw new ArgumentNullException(nameof(sourceEntry));
			}

			if (isDisposed)
			{
				throw new ObjectDisposedException("TarArchive");
			}

			try
			{
				if (recurse)
				{
					TarHeader.SetValueDefaults(sourceEntry.UserId, sourceEntry.UserName,
											   sourceEntry.GroupId, sourceEntry.GroupName);
				}
				WriteEntryCore(sourceEntry, recurse);
			}
			finally
			{
				if (recurse)
				{
					TarHeader.RestoreSetValues();
				}
			}
		}

		private void WriteEntryCore(TarEntry sourceEntry, bool recurse)
		{
			string tempFileName = null;
			string entryFilename = sourceEntry.File;

			var entry = (TarEntry)sourceEntry.Clone();

			if (applyUserInfoOverrides)
			{
				entry.GroupId = groupId;
				entry.GroupName = groupName;
				entry.UserId = userId;
				entry.UserName = userName;
			}

			OnProgressMessageEvent(entry, null);

			if (asciiTranslate && !entry.IsDirectory)
			{
				if (!IsBinary(entryFilename))
				{
					tempFileName = PathUtils.GetTempFileName();

					using (StreamReader inStream = File.OpenText(entryFilename))
					{
						using (Stream outStream = File.Create(tempFileName))
						{
							while (true)
							{
								string line = inStream.ReadLine();
								if (line == null)
								{
									break;
								}
								byte[] data = Encoding.ASCII.GetBytes(line);
								outStream.Write(data, 0, data.Length);
								outStream.WriteByte((byte)'\n');
							}

							outStream.Flush();
						}
					}

					entry.Size = new FileInfo(tempFileName).Length;
					entryFilename = tempFileName;
				}
			}

			string newName = null;

			if (!String.IsNullOrEmpty(rootPath))
			{
				if (entry.Name.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
				{
					newName = entry.Name.Substring(rootPath.Length + 1);
				}
			}

			if (pathPrefix != null)
			{
				newName = (newName == null) ? pathPrefix + "/" + entry.Name : pathPrefix + "/" + newName;
			}

			if (newName != null)
			{
				entry.Name = newName;
			}

			tarOut.PutNextEntry(entry);

			if (entry.IsDirectory)
			{
				if (recurse)
				{
					TarEntry[] list = entry.GetDirectoryEntries();
					for (int i = 0; i < list.Length; ++i)
					{
						WriteEntryCore(list[i], recurse);
					}
				}
			}
			else
			{
				using (Stream inputStream = File.OpenRead(entryFilename))
				{
					byte[] localBuffer = new byte[32 * 1024];
					while (true)
					{
						int numRead = inputStream.Read(localBuffer, 0, localBuffer.Length);

						if (numRead <= 0)
						{
							break;
						}

						tarOut.Write(localBuffer, 0, numRead);
					}
				}

				if (!string.IsNullOrEmpty(tempFileName))
				{
					File.Delete(tempFileName);
				}

				tarOut.CloseEntry();
			}
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		protected virtual void Dispose(bool disposing)
		{
			if (!isDisposed)
			{
				isDisposed = true;
				if (disposing)
				{
					if (tarOut != null)
					{
						tarOut.Flush();
						tarOut.Dispose();
					}

					if (tarIn != null)
					{
						tarIn.Dispose();
					}
				}
			}
		}

		public virtual void Close()
		{
			Dispose(true);
		}

		~TarArchive()
		{
			Dispose(false);
		}

		private static void EnsureDirectoryExists(string directoryName)
		{
			if (!Directory.Exists(directoryName))
			{
				try
				{
					Directory.CreateDirectory(directoryName);
				}
				catch (Exception e)
				{
					throw new TarException("Exception creating directory '" + directoryName + "', " + e.Message, e);
				}
			}
		}

		private static bool IsBinary(string filename)
		{
			using (FileStream fs = File.OpenRead(filename))
			{
				int sampleSize = Math.Min(4096, (int)fs.Length);
				byte[] content = new byte[sampleSize];

				int bytesRead = fs.Read(content, 0, sampleSize);

				for (int i = 0; i < bytesRead; ++i)
				{
					byte b = content[i];
					if ((b < 8) || ((b > 13) && (b < 32)) || (b == 255))
					{
						return true;
					}
				}
			}
			return false;
		}

		#region Instance Fields

		private bool keepOldFiles;
		private bool asciiTranslate;

		private int userId;
		private string userName = string.Empty;
		private int groupId;
		private string groupName = string.Empty;

		private string rootPath;
		private string pathPrefix;

		private bool applyUserInfoOverrides;

		private TarInputStream tarIn;
		private TarOutputStream tarOut;
		private bool isDisposed;

		#endregion Instance Fields
	}
}
