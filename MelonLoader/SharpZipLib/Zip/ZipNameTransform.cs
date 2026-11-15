using MelonLoader.ICSharpCode.SharpZipLib.Core;
using System;
using System.IO;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public class ZipNameTransform : INameTransform
	{
		#region Constructors

		public ZipNameTransform()
		{
		}

		public ZipNameTransform(string trimPrefix)
		{
			TrimPrefix = trimPrefix;
		}

		#endregion Constructors

		static ZipNameTransform()
		{
			char[] invalidPathChars;
			invalidPathChars = Path.GetInvalidPathChars();
			int howMany = invalidPathChars.Length + 2;

			InvalidEntryCharsRelaxed = new char[howMany];
			Array.Copy(invalidPathChars, 0, InvalidEntryCharsRelaxed, 0, invalidPathChars.Length);
			InvalidEntryCharsRelaxed[howMany - 1] = '*';
			InvalidEntryCharsRelaxed[howMany - 2] = '?';

			howMany = invalidPathChars.Length + 4;
			InvalidEntryChars = new char[howMany];
			Array.Copy(invalidPathChars, 0, InvalidEntryChars, 0, invalidPathChars.Length);
			InvalidEntryChars[howMany - 1] = ':';
			InvalidEntryChars[howMany - 2] = '\\';
			InvalidEntryChars[howMany - 3] = '*';
			InvalidEntryChars[howMany - 4] = '?';
		}

		public string TransformDirectory(string name)
		{
			name = TransformFile(name);
			if (name.Length > 0)
			{
				if (!name.EndsWith("/", StringComparison.Ordinal))
				{
					name += "/";
				}
			}
			else
			{
				throw new ZipException("Cannot have an empty directory name");
			}
			return name;
		}

		public string TransformFile(string name)
		{
			if (name != null)
			{
				string lowerName = name.ToLower();
				if ((trimPrefix_ != null) && (lowerName.IndexOf(trimPrefix_, StringComparison.Ordinal) == 0))
				{
					name = name.Substring(trimPrefix_.Length);
				}

				name = name.Replace(@"\", "/");
				name = PathUtils.DropPathRoot(name);

				name = name.Trim('/');

				int index = name.IndexOf("//", StringComparison.Ordinal);
				while (index >= 0)
				{
					name = name.Remove(index, 1);
					index = name.IndexOf("//", StringComparison.Ordinal);
				}

				name = MakeValidName(name, '_');
			}
			else
			{
				name = string.Empty;
			}
			return name;
		}

		public string TrimPrefix
		{
			get { return trimPrefix_; }
			set
			{
				trimPrefix_ = value;
				if (trimPrefix_ != null)
				{
					trimPrefix_ = trimPrefix_.ToLower();
				}
			}
		}

		private static string MakeValidName(string name, char replacement)
		{
			int index = name.IndexOfAny(InvalidEntryChars);
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

			if (name.Length > 0xffff)
			{
				throw new PathTooLongException();
			}

			return name;
		}

		public static bool IsValidName(string name, bool relaxed)
		{
			bool result = (name != null);

			if (result)
			{
				if (relaxed)
				{
					result = name.IndexOfAny(InvalidEntryCharsRelaxed) < 0;
				}
				else
				{
					result =
						(name.IndexOfAny(InvalidEntryChars) < 0) &&
						(name.IndexOf('/') != 0);
				}
			}

			return result;
		}

		public static bool IsValidName(string name)
		{
			bool result =
				(name != null) &&
				(name.IndexOfAny(InvalidEntryChars) < 0) &&
				(name.IndexOf('/') != 0)
				;
			return result;
		}

		#region Instance Fields

		private string trimPrefix_;

		#endregion Instance Fields

		#region Class Fields

		private static readonly char[] InvalidEntryChars;
		private static readonly char[] InvalidEntryCharsRelaxed;

		#endregion Class Fields
	}

	public class PathTransformer : INameTransform
	{
		public PathTransformer()
		{
		}

		public string TransformDirectory(string name)
		{
			name = TransformFile(name);
			
			if (name.Length > 0)
			{
				if (!name.EndsWith("/", StringComparison.Ordinal))
				{
					name += "/";
				}
			}
			else
			{
				throw new ZipException("Cannot have an empty directory name");
			}

			return name;
		}

		public string TransformFile(string name)
		{
			if (name != null)
			{
				name = name.Replace(@"\", "/");

				name = PathUtils.DropPathRoot(name);

				name = name.Trim('/');

				int index = name.IndexOf("//", StringComparison.Ordinal);
				while (index >= 0)
				{
					name = name.Remove(index, 1);
					index = name.IndexOf("//", StringComparison.Ordinal);
				}
			}
			else
			{
				name = string.Empty;
			}

			return name;
		}
	}
}
