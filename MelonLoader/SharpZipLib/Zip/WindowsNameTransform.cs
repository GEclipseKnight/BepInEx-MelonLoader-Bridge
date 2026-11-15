using MelonLoader.ICSharpCode.SharpZipLib.Core;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public class WindowsNameTransform : INameTransform
	{
		private const int MaxPath = 260;

		private string _baseDirectory;
		private bool _trimIncomingPaths;
		private char _replacementChar = '_';
		private bool _allowParentTraversal;

		private static readonly char[] InvalidEntryChars = new char[] {
			'"', '<', '>', '|', '\0', '\u0001', '\u0002', '\u0003', '\u0004', '\u0005',
			'\u0006', '\a', '\b', '\t', '\n', '\v', '\f', '\r', '\u000e', '\u000f',
			'\u0010', '\u0011', '\u0012', '\u0013', '\u0014', '\u0015', '\u0016',
			'\u0017', '\u0018', '\u0019', '\u001a', '\u001b', '\u001c', '\u001d',
			'\u001e', '\u001f',
			'*', '?', ':'
		};

		public WindowsNameTransform(string baseDirectory, bool allowParentTraversal = false)
		{
			BaseDirectory = baseDirectory ?? throw new ArgumentNullException(nameof(baseDirectory), "Directory name is invalid");
			AllowParentTraversal = allowParentTraversal;
		}

		public WindowsNameTransform()
		{
		}

		public string BaseDirectory
		{
			get { return _baseDirectory; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}

				_baseDirectory = Path.GetFullPath(value);
			}
		}

		public bool AllowParentTraversal
		{
			get => _allowParentTraversal;
			set => _allowParentTraversal = value;
		}

		public bool TrimIncomingPaths
		{
			get { return _trimIncomingPaths; }
			set { _trimIncomingPaths = value; }
		}

		public string TransformDirectory(string name)
		{
			name = TransformFile(name);
			if (name.Length > 0)
			{
				while (name.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal))
				{
					name = name.Remove(name.Length - 1, 1);
				}
			}
			else
			{
				throw new InvalidNameException("Cannot have an empty directory name");
			}
			return name;
		}

		public string TransformFile(string name)
		{
			if (name != null)
			{
				name = MakeValidName(name, _replacementChar);

				if (_trimIncomingPaths)
				{
					name = Path.GetFileName(name);
				}

				if (_baseDirectory != null)
				{
					name = Path.Combine(_baseDirectory, name);

					var pathBase = Path.GetFullPath(_baseDirectory);
					if (pathBase[pathBase.Length - 1] != Path.DirectorySeparatorChar)
					{
						pathBase += Path.DirectorySeparatorChar;
					}

					if (!_allowParentTraversal && !Path.GetFullPath(name).StartsWith(pathBase, StringComparison.InvariantCultureIgnoreCase))
					{
						throw new InvalidNameException("Parent traversal in paths is not allowed");
					}
				}
			}
			else
			{
				name = string.Empty;
			}
			return name;
		}

		public static bool IsValidName(string name)
		{
			bool result =
				(name != null) &&
				(name.Length <= MaxPath) &&
				(string.Compare(name, MakeValidName(name, '_'), StringComparison.Ordinal) == 0)
				;

			return result;
		}

		public static string MakeValidName(string name, char replacement)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			name = PathUtils.DropPathRoot(name.Replace("/", Path.DirectorySeparatorChar.ToString()));

			while ((name.Length > 0) && (name[0] == Path.DirectorySeparatorChar))
			{
				name = name.Remove(0, 1);
			}

			while ((name.Length > 0) && (name[name.Length - 1] == Path.DirectorySeparatorChar))
			{
				name = name.Remove(name.Length - 1, 1);
			}

			int index = name.IndexOf(string.Format("{0}{0}", Path.DirectorySeparatorChar), StringComparison.Ordinal);
			while (index >= 0)
			{
				name = name.Remove(index, 1);
				index = name.IndexOf(string.Format("{0}{0}", Path.DirectorySeparatorChar), StringComparison.Ordinal);
			}

			index = name.IndexOfAny(InvalidEntryChars);
			if (index >= 0)
			{
				var builder = new StringBuilder(name);

				while (index >= 0)
				{
					builder[index] = replacement;

					if (index >= name.Length)
					{
						index = -1;
					}
					else
					{
						index = name.IndexOfAny(InvalidEntryChars, index + 1);
					}
				}
				name = builder.ToString();
			}

			if (name.Length > MaxPath)
			{
				throw new PathTooLongException();
			}

			return name;
		}

		public char Replacement
		{
			get { return _replacementChar; }
			set
			{
				for (int i = 0; i < InvalidEntryChars.Length; ++i)
				{
					if (InvalidEntryChars[i] == value)
					{
						throw new ArgumentException("invalid path character");
					}
				}

				if ((value == Path.DirectorySeparatorChar) || (value == Path.AltDirectorySeparatorChar))
				{
					throw new ArgumentException("invalid replacement character");
				}

				_replacementChar = value;
			}
		}
	}
}
