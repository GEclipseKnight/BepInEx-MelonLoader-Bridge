using System;
using System.IO;
using System.Text;
using MelonLoader.ICSharpCode.SharpZipLib.Core;

namespace MelonLoader.ICSharpCode.SharpZipLib.Tar
{
	public class TarEntry
	{
		#region Constructors

		private TarEntry()
		{
			header = new TarHeader();
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public TarEntry(byte[] headerBuffer) : this(headerBuffer, null)
		{
		}

		public TarEntry(byte[] headerBuffer, Encoding nameEncoding)
		{
			header = new TarHeader();
			header.ParseBuffer(headerBuffer, nameEncoding);
		}

		public TarEntry(TarHeader header)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			this.header = (TarHeader)header.Clone();
		}

		#endregion Constructors

		#region ICloneable Members

		public object Clone()
		{
			var entry = new TarEntry();
			entry.file = file;
			entry.header = (TarHeader)header.Clone();
			entry.Name = Name;
			return entry;
		}

		#endregion ICloneable Members

		public static TarEntry CreateTarEntry(string name)
		{
			var entry = new TarEntry();
			TarEntry.NameTarHeader(entry.header, name);
			return entry;
		}

		public static TarEntry CreateEntryFromFile(string fileName)
		{
			var entry = new TarEntry();
			entry.GetFileTarHeader(entry.header, fileName);
			return entry;
		}

		public override bool Equals(object obj)
		{
			var localEntry = obj as TarEntry;

			if (localEntry != null)
			{
				return Name.Equals(localEntry.Name);
			}
			return false;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public bool IsDescendent(TarEntry toTest)
		{
			if (toTest == null)
			{
				throw new ArgumentNullException(nameof(toTest));
			}

			return toTest.Name.StartsWith(Name, StringComparison.Ordinal);
		}

		public TarHeader TarHeader
		{
			get
			{
				return header;
			}
		}

		public string Name
		{
			get
			{
				return header.Name;
			}
			set
			{
				header.Name = value;
			}
		}

		public int UserId
		{
			get
			{
				return header.UserId;
			}
			set
			{
				header.UserId = value;
			}
		}

		public int GroupId
		{
			get
			{
				return header.GroupId;
			}
			set
			{
				header.GroupId = value;
			}
		}

		public string UserName
		{
			get
			{
				return header.UserName;
			}
			set
			{
				header.UserName = value;
			}
		}

		public string GroupName
		{
			get
			{
				return header.GroupName;
			}
			set
			{
				header.GroupName = value;
			}
		}

		public void SetIds(int userId, int groupId)
		{
			UserId = userId;
			GroupId = groupId;
		}

		public void SetNames(string userName, string groupName)
		{
			UserName = userName;
			GroupName = groupName;
		}

		public DateTime ModTime
		{
			get
			{
				return header.ModTime;
			}
			set
			{
				header.ModTime = value;
			}
		}

		public string File
		{
			get
			{
				return file;
			}
		}

		public long Size
		{
			get
			{
				return header.Size;
			}
			set
			{
				header.Size = value;
			}
		}

		public bool IsDirectory
		{
			get
			{
				if (file != null)
				{
					return Directory.Exists(file);
				}

				if (header != null)
				{
					if ((header.TypeFlag == TarHeader.LF_DIR) || Name.EndsWith("/", StringComparison.Ordinal))
					{
						return true;
					}
				}
				return false;
			}
		}

		public void GetFileTarHeader(TarHeader header, string file)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			if (file == null)
			{
				throw new ArgumentNullException(nameof(file));
			}

			this.file = file;

			string name = file;

			if (name.IndexOf(Directory.GetCurrentDirectory(), StringComparison.Ordinal) == 0)
			{
				name = name.Substring(Directory.GetCurrentDirectory().Length);
			}

			/*
						if (Path.DirectorySeparatorChar == '\\')
						{
							if (name.Length > 2)
							{
								char ch1 = name[0];
								char ch2 = name[1];

								if (ch2 == ':' && Char.IsLetter(ch1))
								{
									name = name.Substring(2);
								}
							}
						}
			*/

			name = name.Replace(Path.DirectorySeparatorChar, '/');

			while (name.StartsWith("/", StringComparison.Ordinal))
			{
				name = name.Substring(1);
			}

			header.LinkName = String.Empty;
			header.Name = name;

			if (Directory.Exists(file))
			{
				header.Mode = 1003; // Magic number for security access for a UNIX filesystem
				header.TypeFlag = TarHeader.LF_DIR;
				if ((header.Name.Length == 0) || header.Name[header.Name.Length - 1] != '/')
				{
					header.Name = header.Name + "/";
				}

				header.Size = 0;
			}
			else
			{
				header.Mode = 33216; // Magic number for security access for a UNIX filesystem
				header.TypeFlag = TarHeader.LF_NORMAL;
				header.Size = new FileInfo(file.Replace('/', Path.DirectorySeparatorChar)).Length;
			}

			header.ModTime = System.IO.File.GetLastWriteTime(file.Replace('/', Path.DirectorySeparatorChar)).ToUniversalTime();
			header.DevMajor = 0;
			header.DevMinor = 0;
		}

		public TarEntry[] GetDirectoryEntries()
		{
			if ((file == null) || !Directory.Exists(file))
			{
				return Empty.Array<TarEntry>();
			}

			string[] list = Directory.GetFileSystemEntries(file);
			TarEntry[] result = new TarEntry[list.Length];

			for (int i = 0; i < list.Length; ++i)
			{
				result[i] = TarEntry.CreateEntryFromFile(list[i]);
			}

			return result;
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public void WriteEntryHeader(byte[] outBuffer)
		{
			WriteEntryHeader(outBuffer, null);
		}

		public void WriteEntryHeader(byte[] outBuffer, Encoding nameEncoding)
		{
			header.WriteHeader(outBuffer, nameEncoding);
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		static public void AdjustEntryName(byte[] buffer, string newName)
		{
			AdjustEntryName(buffer, newName, null);
		}

		static public void AdjustEntryName(byte[] buffer, string newName, Encoding nameEncoding)
		{
			TarHeader.GetNameBytes(newName, buffer, 0, TarHeader.NAMELEN, nameEncoding);
		}

		static public void NameTarHeader(TarHeader header, string name)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			bool isDir = name.EndsWith("/", StringComparison.Ordinal);

			header.Name = name;
			header.Mode = isDir ? 1003 : 33216;
			header.UserId = 0;
			header.GroupId = 0;
			header.Size = 0;

			header.ModTime = DateTime.UtcNow;

			header.TypeFlag = isDir ? TarHeader.LF_DIR : TarHeader.LF_NORMAL;

			header.LinkName = String.Empty;
			header.UserName = String.Empty;
			header.GroupName = String.Empty;

			header.DevMajor = 0;
			header.DevMinor = 0;
		}

		#region Instance Fields

		private string file;

		private TarHeader header;

		#endregion Instance Fields
	}
}
