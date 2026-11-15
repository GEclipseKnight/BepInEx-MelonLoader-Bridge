using System;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.Tar
{
	public class TarHeader
	{
		#region Constants

		public const int NAMELEN = 100;

		public const int MODELEN = 8;

		public const int UIDLEN = 8;

		public const int GIDLEN = 8;

		public const int CHKSUMLEN = 8;

		public const int CHKSUMOFS = 148;

		public const int SIZELEN = 12;

		public const int MAGICLEN = 6;

		public const int VERSIONLEN = 2;

		public const int MODTIMELEN = 12;

		public const int UNAMELEN = 32;

		public const int GNAMELEN = 32;

		public const int DEVLEN = 8;

		public const int PREFIXLEN = 155;


		public const byte LF_OLDNORM = 0;

		public const byte LF_NORMAL = (byte)'0';

		public const byte LF_LINK = (byte)'1';

		public const byte LF_SYMLINK = (byte)'2';

		public const byte LF_CHR = (byte)'3';

		public const byte LF_BLK = (byte)'4';

		public const byte LF_DIR = (byte)'5';

		public const byte LF_FIFO = (byte)'6';

		public const byte LF_CONTIG = (byte)'7';

		public const byte LF_GHDR = (byte)'g';

		public const byte LF_XHDR = (byte)'x';


		public const byte LF_ACL = (byte)'A';

		public const byte LF_GNU_DUMPDIR = (byte)'D';

		public const byte LF_EXTATTR = (byte)'E';

		public const byte LF_META = (byte)'I';

		public const byte LF_GNU_LONGLINK = (byte)'K';

		public const byte LF_GNU_LONGNAME = (byte)'L';

		public const byte LF_GNU_MULTIVOL = (byte)'M';

		public const byte LF_GNU_NAMES = (byte)'N';

		public const byte LF_GNU_SPARSE = (byte)'S';

		public const byte LF_GNU_VOLHDR = (byte)'V';

		public const string TMAGIC = "ustar";

		public const string GNU_TMAGIC = "ustar  ";

		private const long timeConversionFactor = 10000000L;           // 1 tick == 100 nanoseconds
		private static readonly DateTime dateTime1970 = new DateTime(1970, 1, 1, 0, 0, 0, 0);

		#endregion Constants

		#region Constructors

		public TarHeader()
		{
			Magic = TMAGIC;
			Version = " ";

			Name = "";
			LinkName = "";

			UserId = defaultUserId;
			GroupId = defaultGroupId;
			UserName = defaultUser;
			GroupName = defaultGroupName;
			Size = 0;
		}

		#endregion Constructors

		#region Properties

		public string Name
		{
			get { return name; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				name = value;
			}
		}

		[Obsolete("Use the Name property instead", true)]
		public string GetName()
		{
			return name;
		}

		public int Mode
		{
			get { return mode; }
			set { mode = value; }
		}

		public int UserId
		{
			get { return userId; }
			set { userId = value; }
		}

		public int GroupId
		{
			get { return groupId; }
			set { groupId = value; }
		}

		public long Size
		{
			get { return size; }
			set
			{
				if (value < 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "Cannot be less than zero");
				}
				size = value;
			}
		}

		public DateTime ModTime
		{
			get { return modTime; }
			set
			{
				if (value < dateTime1970)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "ModTime cannot be before Jan 1st 1970");
				}
				modTime = new DateTime(value.Year, value.Month, value.Day, value.Hour, value.Minute, value.Second);
			}
		}

		public int Checksum
		{
			get { return checksum; }
		}

		public bool IsChecksumValid
		{
			get { return isChecksumValid; }
		}

		public byte TypeFlag
		{
			get { return typeFlag; }
			set { typeFlag = value; }
		}

		public string LinkName
		{
			get { return linkName; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				linkName = value;
			}
		}

		public string Magic
		{
			get { return magic; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				magic = value;
			}
		}

		public string Version
		{
			get
			{
				return version;
			}

			set
			{
				if (value == null)
				{
					throw new ArgumentNullException(nameof(value));
				}
				version = value;
			}
		}

		public string UserName
		{
			get { return userName; }
			set
			{
				if (value != null)
				{
					userName = value.Substring(0, Math.Min(UNAMELEN, value.Length));
				}
				else
				{
					string currentUser = "user";
					if (currentUser.Length > UNAMELEN)
					{
						currentUser = currentUser.Substring(0, UNAMELEN);
					}
					userName = currentUser;
				}
			}
		}

		public string GroupName
		{
			get { return groupName; }
			set
			{
				if (value == null)
				{
					groupName = "None";
				}
				else
				{
					groupName = value;
				}
			}
		}

		public int DevMajor
		{
			get { return devMajor; }
			set { devMajor = value; }
		}

		public int DevMinor
		{
			get { return devMinor; }
			set { devMinor = value; }
		}

		#endregion Properties

		#region ICloneable Members

		public object Clone()
		{
			return this.MemberwiseClone();
		}

		#endregion ICloneable Members

		public void ParseBuffer(byte[] header, Encoding nameEncoding)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			int offset = 0;

			name = ParseName(header, offset, NAMELEN, nameEncoding).ToString();
			offset += NAMELEN;

			mode = (int)ParseOctal(header, offset, MODELEN);
			offset += MODELEN;

			UserId = (int)ParseOctal(header, offset, UIDLEN);
			offset += UIDLEN;

			GroupId = (int)ParseOctal(header, offset, GIDLEN);
			offset += GIDLEN;

			Size = ParseBinaryOrOctal(header, offset, SIZELEN);
			offset += SIZELEN;

			ModTime = GetDateTimeFromCTime(ParseOctal(header, offset, MODTIMELEN));
			offset += MODTIMELEN;

			checksum = (int)ParseOctal(header, offset, CHKSUMLEN);
			offset += CHKSUMLEN;

			TypeFlag = header[offset++];

			LinkName = ParseName(header, offset, NAMELEN, nameEncoding).ToString();
			offset += NAMELEN;

			Magic = ParseName(header, offset, MAGICLEN, nameEncoding).ToString();
			offset += MAGICLEN;

			if (Magic == "ustar")
			{
				Version = ParseName(header, offset, VERSIONLEN, nameEncoding).ToString();
				offset += VERSIONLEN;

				UserName = ParseName(header, offset, UNAMELEN, nameEncoding).ToString();
				offset += UNAMELEN;

				GroupName = ParseName(header, offset, GNAMELEN, nameEncoding).ToString();
				offset += GNAMELEN;

				DevMajor = (int)ParseOctal(header, offset, DEVLEN);
				offset += DEVLEN;

				DevMinor = (int)ParseOctal(header, offset, DEVLEN);
				offset += DEVLEN;

				string prefix = ParseName(header, offset, PREFIXLEN, nameEncoding).ToString();
				if (!string.IsNullOrEmpty(prefix)) Name = prefix + '/' + Name;
			}

			isChecksumValid = Checksum == TarHeader.MakeCheckSum(header);
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public void ParseBuffer(byte[] header)
		{
			ParseBuffer(header, null);
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public void WriteHeader(byte[] outBuffer)
		{
			WriteHeader(outBuffer, null);
		}

		public void WriteHeader(byte[] outBuffer, Encoding nameEncoding)
		{
			if (outBuffer == null)
			{
				throw new ArgumentNullException(nameof(outBuffer));
			}

			int offset = 0;

			offset = GetNameBytes(Name, outBuffer, offset, NAMELEN, nameEncoding);
			offset = GetOctalBytes(mode, outBuffer, offset, MODELEN);
			offset = GetOctalBytes(UserId, outBuffer, offset, UIDLEN);
			offset = GetOctalBytes(GroupId, outBuffer, offset, GIDLEN);

			offset = GetBinaryOrOctalBytes(Size, outBuffer, offset, SIZELEN);
			offset = GetOctalBytes(GetCTime(ModTime), outBuffer, offset, MODTIMELEN);

			int csOffset = offset;
			for (int c = 0; c < CHKSUMLEN; ++c)
			{
				outBuffer[offset++] = (byte)' ';
			}

			outBuffer[offset++] = TypeFlag;

			offset = GetNameBytes(LinkName, outBuffer, offset, NAMELEN, nameEncoding);
			offset = GetAsciiBytes(Magic, 0, outBuffer, offset, MAGICLEN, nameEncoding);
			offset = GetNameBytes(Version, outBuffer, offset, VERSIONLEN, nameEncoding);
			offset = GetNameBytes(UserName, outBuffer, offset, UNAMELEN, nameEncoding);
			offset = GetNameBytes(GroupName, outBuffer, offset, GNAMELEN, nameEncoding);

			if ((TypeFlag == LF_CHR) || (TypeFlag == LF_BLK))
			{
				offset = GetOctalBytes(DevMajor, outBuffer, offset, DEVLEN);
				offset = GetOctalBytes(DevMinor, outBuffer, offset, DEVLEN);
			}

			for (; offset < outBuffer.Length;)
			{
				outBuffer[offset++] = 0;
			}

			checksum = ComputeCheckSum(outBuffer);

			GetCheckSumOctalBytes(checksum, outBuffer, csOffset, CHKSUMLEN);
			isChecksumValid = true;
		}

		public override int GetHashCode()
		{
			return Name.GetHashCode();
		}

		public override bool Equals(object obj)
		{
			var localHeader = obj as TarHeader;

			bool result;
			if (localHeader != null)
			{
				result = (name == localHeader.name)
					&& (mode == localHeader.mode)
					&& (UserId == localHeader.UserId)
					&& (GroupId == localHeader.GroupId)
					&& (Size == localHeader.Size)
					&& (ModTime == localHeader.ModTime)
					&& (Checksum == localHeader.Checksum)
					&& (TypeFlag == localHeader.TypeFlag)
					&& (LinkName == localHeader.LinkName)
					&& (Magic == localHeader.Magic)
					&& (Version == localHeader.Version)
					&& (UserName == localHeader.UserName)
					&& (GroupName == localHeader.GroupName)
					&& (DevMajor == localHeader.DevMajor)
					&& (DevMinor == localHeader.DevMinor);
			}
			else
			{
				result = false;
			}
			return result;
		}

		static internal void SetValueDefaults(int userId, string userName, int groupId, string groupName)
		{
			defaultUserId = userIdAsSet = userId;
			defaultUser = userNameAsSet = userName;
			defaultGroupId = groupIdAsSet = groupId;
			defaultGroupName = groupNameAsSet = groupName;
		}

		static internal void RestoreSetValues()
		{
			defaultUserId = userIdAsSet;
			defaultUser = userNameAsSet;
			defaultGroupId = groupIdAsSet;
			defaultGroupName = groupNameAsSet;
		}

		static private long ParseBinaryOrOctal(byte[] header, int offset, int length)
		{
			if (header[offset] >= 0x80)
			{
				long result = 0;
				for (int pos = length - 8; pos < length; pos++)
				{
					result = result << 8 | header[offset + pos];
				}
				return result;
			}
			return ParseOctal(header, offset, length);
		}

		static public long ParseOctal(byte[] header, int offset, int length)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			long result = 0;
			bool stillPadding = true;

			int end = offset + length;
			for (int i = offset; i < end; ++i)
			{
				if (header[i] == 0)
				{
					break;
				}

				if (header[i] == (byte)' ' || header[i] == '0')
				{
					if (stillPadding)
					{
						continue;
					}

					if (header[i] == (byte)' ')
					{
						break;
					}
				}

				stillPadding = false;

				result = (result << 3) + (header[i] - '0');
			}

			return result;
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		static public StringBuilder ParseName(byte[] header, int offset, int length)
		{
			return ParseName(header, offset, length, null);
		}

		static public StringBuilder ParseName(byte[] header, int offset, int length, Encoding encoding)
		{
			if (header == null)
			{
				throw new ArgumentNullException(nameof(header));
			}

			if (offset < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(offset), "Cannot be less than zero");
			}

			if (length < 0)
			{
				throw new ArgumentOutOfRangeException(nameof(length), "Cannot be less than zero");
			}

			if (offset + length > header.Length)
			{
				throw new ArgumentException("Exceeds header size", nameof(length));
			}

			var result = new StringBuilder(length);

			int count = 0;
			if(encoding == null)
			{
				for (int i = offset; i < offset + length; ++i)
				{
					if (header[i] == 0)
					{
						break;
					}
					result.Append((char)header[i]);
				}
			}
			else
			{
				for(int i = offset; i < offset + length; ++i, ++count)
				{
					if(header[i] == 0)
					{
						break;
					}
				}
				result.Append(encoding.GetString(header, offset, count));
			}

			return result;
		}

		public static int GetNameBytes(StringBuilder name, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			return GetNameBytes(name.ToString(), nameOffset, buffer, bufferOffset, length, null);
		}

		public static int GetNameBytes(string name, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			return GetNameBytes(name, nameOffset, buffer, bufferOffset, length, null);
		}

		public static int GetNameBytes(string name, int nameOffset, byte[] buffer, int bufferOffset, int length, Encoding encoding)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int i;
			if(encoding != null)
			{
				var nameArray = name.ToCharArray(nameOffset, Math.Min(name.Length - nameOffset, length));
				var bytes = encoding.GetBytes(nameArray, 0, nameArray.Length);
				i = Math.Min(bytes.Length, length);
				Array.Copy(bytes, 0, buffer, bufferOffset, i);
			}
			else
			{
				for (i = 0; i < length && nameOffset + i < name.Length; ++i)
				{
					buffer[bufferOffset + i] = (byte)name[nameOffset + i];
				}
			}

			for (; i < length; ++i)
			{
				buffer[bufferOffset + i] = 0;
			}
			return bufferOffset + length;
		}
		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public static int GetNameBytes(StringBuilder name, byte[] buffer, int offset, int length)
		{
			return GetNameBytes(name, buffer, offset, length, null);
		}

		public static int GetNameBytes(StringBuilder name, byte[] buffer, int offset, int length, Encoding encoding)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			return GetNameBytes(name.ToString(), 0, buffer, offset, length, encoding);
		}

		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public static int GetNameBytes(string name, byte[] buffer, int offset, int length)
		{
			return GetNameBytes(name, buffer, offset, length, null);
		}

		public static int GetNameBytes(string name, byte[] buffer, int offset, int length, Encoding encoding)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			return GetNameBytes(name, 0, buffer, offset, length, encoding);
		}
		[Obsolete("No Encoding for Name field is specified, any non-ASCII bytes will be discarded")]
		public static int GetAsciiBytes(string toAdd, int nameOffset, byte[] buffer, int bufferOffset, int length)
		{
			return GetAsciiBytes(toAdd, nameOffset, buffer, bufferOffset, length, null);
		}

		public static int GetAsciiBytes(string toAdd, int nameOffset, byte[] buffer, int bufferOffset, int length, Encoding encoding)
		{
			if (toAdd == null)
			{
				throw new ArgumentNullException(nameof(toAdd));
			}

			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int i;
			if(encoding == null)
			{
				for (i = 0; i < length && nameOffset + i < toAdd.Length; ++i)
				{
					buffer[bufferOffset + i] = (byte)toAdd[nameOffset + i];
				}
			}
			else
			{
				var chars = toAdd.ToCharArray();
				var bytes = encoding.GetBytes(chars, nameOffset, Math.Min(toAdd.Length - nameOffset, length));
				i = Math.Min(bytes.Length, length);
				Array.Copy(bytes, 0, buffer, bufferOffset, i);
			}
			for (; i < length; ++i)
				buffer[bufferOffset + i] = 0;
			return bufferOffset + length;
		}

		public static int GetOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			int localIndex = length - 1;

			buffer[offset + localIndex] = 0;
			--localIndex;

			if (value > 0)
			{
				for (long v = value; (localIndex >= 0) && (v > 0); --localIndex)
				{
					buffer[offset + localIndex] = (byte)((byte)'0' + (byte)(v & 7));
					v >>= 3;
				}
			}

			for (; localIndex >= 0; --localIndex)
			{
				buffer[offset + localIndex] = (byte)'0';
			}

			return offset + length;
		}

		private static int GetBinaryOrOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			if (value > 0x1FFFFFFFF)
			{  // Octal 77777777777 (11 digits)
				for (int pos = length - 1; pos > 0; pos--)
				{
					buffer[offset + pos] = (byte)value;
					value = value >> 8;
				}
				buffer[offset] = 0x80;
				return offset + length;
			}
			return GetOctalBytes(value, buffer, offset, length);
		}

		private static void GetCheckSumOctalBytes(long value, byte[] buffer, int offset, int length)
		{
			GetOctalBytes(value, buffer, offset, length - 1);
		}

		private static int ComputeCheckSum(byte[] buffer)
		{
			int sum = 0;
			for (int i = 0; i < buffer.Length; ++i)
			{
				sum += buffer[i];
			}
			return sum;
		}

		private static int MakeCheckSum(byte[] buffer)
		{
			int sum = 0;
			for (int i = 0; i < CHKSUMOFS; ++i)
			{
				sum += buffer[i];
			}

			for (int i = 0; i < CHKSUMLEN; ++i)
			{
				sum += (byte)' ';
			}

			for (int i = CHKSUMOFS + CHKSUMLEN; i < buffer.Length; ++i)
			{
				sum += buffer[i];
			}
			return sum;
		}

		private static int GetCTime(DateTime dateTime)
		{
			return unchecked((int)((dateTime.Ticks - dateTime1970.Ticks) / timeConversionFactor));
		}

		private static DateTime GetDateTimeFromCTime(long ticks)
		{
			DateTime result;

			try
			{
				result = new DateTime(dateTime1970.Ticks + ticks * timeConversionFactor);
			}
			catch (ArgumentOutOfRangeException)
			{
				result = dateTime1970;
			}
			return result;
		}

		#region Instance Fields

		private string name;
		private int mode;
		private int userId;
		private int groupId;
		private long size;
		private DateTime modTime;
		private int checksum;
		private bool isChecksumValid;
		private byte typeFlag;
		private string linkName;
		private string magic;
		private string version;
		private string userName;
		private string groupName;
		private int devMajor;
		private int devMinor;

		#endregion Instance Fields

		#region Class Fields

		static internal int userIdAsSet;

		static internal int groupIdAsSet;
		static internal string userNameAsSet;
		static internal string groupNameAsSet = "None";

		static internal int defaultUserId;
		static internal int defaultGroupId;
		static internal string defaultGroupName = "None";
		static internal string defaultUser;

		#endregion Class Fields
	}
}
