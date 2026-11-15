using System;

namespace MelonLoader.ICSharpCode.SharpZipLib.Core
{
	#region EventArgs

	public class ScanEventArgs : EventArgs
	{
		#region Constructors

		public ScanEventArgs(string name)
		{
			name_ = name;
		}

		#endregion Constructors

		public string Name
		{
			get { return name_; }
		}

		public bool ContinueRunning
		{
			get { return continueRunning_; }
			set { continueRunning_ = value; }
		}

		#region Instance Fields

		private string name_;
		private bool continueRunning_ = true;

		#endregion Instance Fields
	}

	public class ProgressEventArgs : EventArgs
	{
		#region Constructors

		public ProgressEventArgs(string name, long processed, long target)
		{
			name_ = name;
			processed_ = processed;
			target_ = target;
		}

		#endregion Constructors

		public string Name
		{
			get { return name_; }
		}

		public bool ContinueRunning
		{
			get { return continueRunning_; }
			set { continueRunning_ = value; }
		}

		public float PercentComplete
		{
			get
			{
				float result;
				if (target_ <= 0)
				{
					result = 0;
				}
				else
				{
					result = ((float)processed_ / (float)target_) * 100.0f;
				}
				return result;
			}
		}

		public long Processed
		{
			get { return processed_; }
		}

		public long Target
		{
			get { return target_; }
		}

		#region Instance Fields

		private string name_;
		private long processed_;
		private long target_;
		private bool continueRunning_ = true;

		#endregion Instance Fields
	}

	public class DirectoryEventArgs : ScanEventArgs
	{
		#region Constructors

		public DirectoryEventArgs(string name, bool hasMatchingFiles)
			: base(name)
		{
			hasMatchingFiles_ = hasMatchingFiles;
		}

		#endregion Constructors

		public bool HasMatchingFiles
		{
			get { return hasMatchingFiles_; }
		}

		private readonly

		#region Instance Fields

		bool hasMatchingFiles_;

		#endregion Instance Fields
	}

	public class ScanFailureEventArgs : EventArgs
	{
		#region Constructors

		public ScanFailureEventArgs(string name, Exception e)
		{
			name_ = name;
			exception_ = e;
			continueRunning_ = true;
		}

		#endregion Constructors

		public string Name
		{
			get { return name_; }
		}

		public Exception Exception
		{
			get { return exception_; }
		}

		public bool ContinueRunning
		{
			get { return continueRunning_; }
			set { continueRunning_ = value; }
		}

		#region Instance Fields

		private string name_;
		private Exception exception_;
		private bool continueRunning_;

		#endregion Instance Fields
	}

	#endregion EventArgs

	#region Delegates

	public delegate void ProcessFileHandler(object sender, ScanEventArgs e);

	public delegate void ProgressHandler(object sender, ProgressEventArgs e);

	public delegate void CompletedFileHandler(object sender, ScanEventArgs e);

	public delegate void DirectoryFailureHandler(object sender, ScanFailureEventArgs e);

	public delegate void FileFailureHandler(object sender, ScanFailureEventArgs e);

	#endregion Delegates

	public class FileSystemScanner
	{
		#region Constructors

		public FileSystemScanner(string filter)
		{
			fileFilter_ = new PathFilter(filter);
		}

		public FileSystemScanner(string fileFilter, string directoryFilter)
		{
			fileFilter_ = new PathFilter(fileFilter);
			directoryFilter_ = new PathFilter(directoryFilter);
		}

		public FileSystemScanner(IScanFilter fileFilter)
		{
			fileFilter_ = fileFilter;
		}

		public FileSystemScanner(IScanFilter fileFilter, IScanFilter directoryFilter)
		{
			fileFilter_ = fileFilter;
			directoryFilter_ = directoryFilter;
		}

		#endregion Constructors

		#region Delegates

		public event EventHandler<DirectoryEventArgs> ProcessDirectory;

		public ProcessFileHandler ProcessFile;

		public CompletedFileHandler CompletedFile;

		public DirectoryFailureHandler DirectoryFailure;

		public FileFailureHandler FileFailure;

		#endregion Delegates

		private bool OnDirectoryFailure(string directory, Exception e)
		{
			DirectoryFailureHandler handler = DirectoryFailure;
			bool result = (handler != null);
			if (result)
			{
				var args = new ScanFailureEventArgs(directory, e);
				handler(this, args);
				alive_ = args.ContinueRunning;
			}
			return result;
		}

		private bool OnFileFailure(string file, Exception e)
		{
			FileFailureHandler handler = FileFailure;

			bool result = (handler != null);

			if (result)
			{
				var args = new ScanFailureEventArgs(file, e);
				FileFailure(this, args);
				alive_ = args.ContinueRunning;
			}
			return result;
		}

		private void OnProcessFile(string file)
		{
			ProcessFileHandler handler = ProcessFile;

			if (handler != null)
			{
				var args = new ScanEventArgs(file);
				handler(this, args);
				alive_ = args.ContinueRunning;
			}
		}

		private void OnCompleteFile(string file)
		{
			CompletedFileHandler handler = CompletedFile;

			if (handler != null)
			{
				var args = new ScanEventArgs(file);
				handler(this, args);
				alive_ = args.ContinueRunning;
			}
		}

		private void OnProcessDirectory(string directory, bool hasMatchingFiles)
		{
			EventHandler<DirectoryEventArgs> handler = ProcessDirectory;

			if (handler != null)
			{
				var args = new DirectoryEventArgs(directory, hasMatchingFiles);
				handler(this, args);
				alive_ = args.ContinueRunning;
			}
		}

		public void Scan(string directory, bool recurse)
		{
			alive_ = true;
			ScanDir(directory, recurse);
		}

		private void ScanDir(string directory, bool recurse)
		{
			try
			{
				string[] names = System.IO.Directory.GetFiles(directory);
				bool hasMatch = false;
				for (int fileIndex = 0; fileIndex < names.Length; ++fileIndex)
				{
					if (!fileFilter_.IsMatch(names[fileIndex]))
					{
						names[fileIndex] = null;
					}
					else
					{
						hasMatch = true;
					}
				}

				OnProcessDirectory(directory, hasMatch);

				if (alive_ && hasMatch)
				{
					foreach (string fileName in names)
					{
						try
						{
							if (fileName != null)
							{
								OnProcessFile(fileName);
								if (!alive_)
								{
									break;
								}
							}
						}
						catch (Exception e)
						{
							if (!OnFileFailure(fileName, e))
							{
								throw;
							}
						}
					}
				}
			}
			catch (Exception e)
			{
				if (!OnDirectoryFailure(directory, e))
				{
					throw;
				}
			}

			if (alive_ && recurse)
			{
				try
				{
					string[] names = System.IO.Directory.GetDirectories(directory);
					foreach (string fulldir in names)
					{
						if ((directoryFilter_ == null) || (directoryFilter_.IsMatch(fulldir)))
						{
							ScanDir(fulldir, true);
							if (!alive_)
							{
								break;
							}
						}
					}
				}
				catch (Exception e)
				{
					if (!OnDirectoryFailure(directory, e))
					{
						throw;
					}
				}
			}
		}

		#region Instance Fields

		private IScanFilter fileFilter_;

		private IScanFilter directoryFilter_;

		private bool alive_;

		#endregion Instance Fields
	}
}
