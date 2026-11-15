using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	public enum HostSystemID
	{
		Msdos = 0,

		Amiga = 1,

		OpenVms = 2,

		Unix = 3,

		VMCms = 4,

		AtariST = 5,

		OS2 = 6,

		Macintosh = 7,

		ZSystem = 8,

		Cpm = 9,

		WindowsNT = 10,

		MVS = 11,

		Vse = 12,

		AcornRisc = 13,

		Vfat = 14,

		AlternateMvs = 15,

		BeOS = 16,

		Tandem = 17,

		OS400 = 18,

		OSX = 19,

		WinZipAES = 99,
	}

	public class ZipEntry
	{
		[Flags]
		private enum Known : byte
		{
			None = 0,
			Size = 0x01,
			CompressedSize = 0x02,
			Crc = 0x04,
			Time = 0x08,
			ExternalAttributes = 0x10,
		}

		#region Constructors

		public ZipEntry(string name)
			: this(name, 0, ZipConstants.VersionMadeBy, CompressionMethod.Deflated)
		{
		}

		internal ZipEntry(string name, int versionRequiredToExtract)
			: this(name, versionRequiredToExtract, ZipConstants.VersionMadeBy,
			CompressionMethod.Deflated)
		{
		}

		internal ZipEntry(string name, int versionRequiredToExtract, int madeByInfo,
			CompressionMethod method)
		{
			if (name == null)
			{
				throw new ArgumentNullException(nameof(name));
			}

			if (name.Length > 0xffff)
			{
				throw new ArgumentException("Name is too long", nameof(name));
			}

			if ((versionRequiredToExtract != 0) && (versionRequiredToExtract < 10))
			{
				throw new ArgumentOutOfRangeException(nameof(versionRequiredToExtract));
			}

			this.DateTime = DateTime.Now;
			this.name = name;
			this.versionMadeBy = (ushort)madeByInfo;
			this.versionToExtract = (ushort)versionRequiredToExtract;
			this.method = method;

			IsUnicodeText = ZipStrings.UseUnicode;
		}

		[Obsolete("Use Clone instead")]
		public ZipEntry(ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			known = entry.known;
			name = entry.name;
			size = entry.size;
			compressedSize = entry.compressedSize;
			crc = entry.crc;
			dateTime = entry.DateTime;
			method = entry.method;
			comment = entry.comment;
			versionToExtract = entry.versionToExtract;
			versionMadeBy = entry.versionMadeBy;
			externalFileAttributes = entry.externalFileAttributes;
			flags = entry.flags;

			zipFileIndex = entry.zipFileIndex;
			offset = entry.offset;

			forceZip64_ = entry.forceZip64_;

			if (entry.extra != null)
			{
				extra = new byte[entry.extra.Length];
				Array.Copy(entry.extra, 0, extra, 0, entry.extra.Length);
			}
		}

		#endregion Constructors

		public bool HasCrc => (known & Known.Crc) != 0;

		public bool IsCrypted
		{
			get => this.HasFlag(GeneralBitFlags.Encrypted);
			set => this.SetFlag(GeneralBitFlags.Encrypted, value);
		}

		public bool IsUnicodeText
		{
			get => this.HasFlag(GeneralBitFlags.UnicodeText);
			set => this.SetFlag(GeneralBitFlags.UnicodeText, value);
		}

		internal byte CryptoCheckValue
		{
			get => cryptoCheckValue_;
			set => cryptoCheckValue_ = value;
		}

		public int Flags
		{
			get => flags;
			set => flags = value;
		}

		public long ZipFileIndex
		{
			get => zipFileIndex;
			set => zipFileIndex = value;
		}

		public long Offset
		{
			get => offset;
			set => offset = value;
		}

		public int ExternalFileAttributes
		{
			get => (known & Known.ExternalAttributes) == 0 ? -1 : externalFileAttributes;

			set
			{
				externalFileAttributes = value;
				known |= Known.ExternalAttributes;
			}
		}

		public int VersionMadeBy => versionMadeBy & 0xff;

		public bool IsDOSEntry
			=> (HostSystem == (int)HostSystemID.Msdos) 
			|| (HostSystem == (int)HostSystemID.WindowsNT);

		private bool HasDosAttributes(int attributes)
		{
			bool result = false;
			if ((known & Known.ExternalAttributes) != 0)
			{
				result |= (((HostSystem == (int)HostSystemID.Msdos) ||
					(HostSystem == (int)HostSystemID.WindowsNT)) &&
					(ExternalFileAttributes & attributes) == attributes);
			}
			return result;
		}

		public int HostSystem
		{
			get => (versionMadeBy >> 8) & 0xff;

			set
			{
				versionMadeBy &= 0x00ff;
				versionMadeBy |= (ushort)((value & 0xff) << 8);
			}
		}

		public int Version
		{
			get
			{
				if (versionToExtract != 0)
					return versionToExtract & 0x00ff;

				if (AESKeySize > 0)
					return ZipConstants.VERSION_AES;

				if (CompressionMethod.BZip2 == method)
					return ZipConstants.VersionBZip2;

				if (CentralHeaderRequiresZip64)
					return ZipConstants.VersionZip64;

				if (CompressionMethod.Deflated == method || IsDirectory || IsCrypted)
					return 20;
				
				if (HasDosAttributes(0x08))
					return 11;
				
				return 10;
			}
		}

		public bool CanDecompress 
			=> Version <= ZipConstants.VersionMadeBy 
			&& (Version == 10 || Version == 11 || Version == 20 || Version == 45 || Version == 46 || Version == 51) 
			&& IsCompressionMethodSupported();

		public void ForceZip64() => forceZip64_ = true;

		public bool IsZip64Forced() => forceZip64_;

		public bool LocalHeaderRequiresZip64
		{
			get
			{
				bool result = forceZip64_;

				if (!result)
				{
					ulong trueCompressedSize = compressedSize;

					if ((versionToExtract == 0) && IsCrypted)
					{
						trueCompressedSize += (ulong)this.EncryptionOverheadSize;
					}

					result =
						((this.size >= uint.MaxValue) || (trueCompressedSize >= uint.MaxValue)) &&
						((versionToExtract == 0) || (versionToExtract >= ZipConstants.VersionZip64));
				}

				return result;
			}
		}

		public bool CentralHeaderRequiresZip64 
			=> LocalHeaderRequiresZip64 || (offset >= uint.MaxValue);

		public long DosTime
		{
			get
			{
				if ((known & Known.Time) == 0)
				{
					return 0;
				}

				var year = (uint)DateTime.Year;
				var month = (uint)DateTime.Month;
				var day = (uint)DateTime.Day;
				var hour = (uint)DateTime.Hour;
				var minute = (uint)DateTime.Minute;
				var second = (uint)DateTime.Second;

				if (year < 1980)
				{
					year = 1980;
					month = 1;
					day = 1;
					hour = 0;
					minute = 0;
					second = 0;
				}
				else if (year > 2107)
				{
					year = 2107;
					month = 12;
					day = 31;
					hour = 23;
					minute = 59;
					second = 59;
				}

				return ((year - 1980) & 0x7f) << 25 |
				       (month << 21) |
				       (day << 16) |
				       (hour << 11) |
				       (minute << 5) |
				       (second >> 1);
			}

			set
			{
				unchecked
				{
					var dosTime = (uint)value;
					uint sec = Math.Min(59, 2 * (dosTime & 0x1f));
					uint min = Math.Min(59, (dosTime >> 5) & 0x3f);
					uint hrs = Math.Min(23, (dosTime >> 11) & 0x1f);
					uint mon = Math.Max(1, Math.Min(12, ((uint)(value >> 21) & 0xf)));
					uint year = ((dosTime >> 25) & 0x7f) + 1980;
					int day = Math.Max(1, Math.Min(DateTime.DaysInMonth((int)year, (int)mon), (int)((value >> 16) & 0x1f)));
					DateTime = new DateTime((int)year, (int)mon, day, (int)hrs, (int)min, (int)sec, DateTimeKind.Unspecified);
				}
			}
		}

		public DateTime DateTime
		{
			get => dateTime;

			set
			{
				dateTime = value;
				known |= Known.Time;
			}
		}

		public string Name
		{
			get => name;
			internal set => name = value;
		}

		public long Size
		{
			get => (known & Known.Size) != 0 ? (long)size : -1L;
			set
			{
				size = (ulong)value;
				known |= Known.Size;
			}
		}

		public long CompressedSize
		{
			get => (known & Known.CompressedSize) != 0 ? (long)compressedSize : -1L;
			set
			{
				compressedSize = (ulong)value;
				known |= Known.CompressedSize;
			}
		}

		public long Crc
		{
			get => (known & Known.Crc) != 0 ? crc & 0xffffffffL : -1L;
			set
			{
				if ((crc & 0xffffffff00000000L) != 0)
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}
				this.crc = (uint)value;
				this.known |= Known.Crc;
			}
		}

		public CompressionMethod CompressionMethod
		{
			get => method;
			set => method = value;
		}

		internal CompressionMethod CompressionMethodForHeader 
			=> (AESKeySize > 0) ? CompressionMethod.WinZipAES : method;

		public byte[] ExtraData
		{
			get => extra;

			set
			{
				if (value == null)
				{
					extra = null;
				}
				else
				{
					if (value.Length > 0xffff)
					{
						throw new System.ArgumentOutOfRangeException(nameof(value));
					}

					extra = new byte[value.Length];
					Array.Copy(value, 0, extra, 0, value.Length);
				}
			}
		}

		public int AESKeySize
		{
			get
			{
				switch (_aesEncryptionStrength)
				{
					case 0:
						return 0;   // Not AES
					case 1:
						return 128;

					case 2:
						return 192; // Not used by WinZip
					case 3:
						return 256;

					default:
						throw new ZipException("Invalid AESEncryptionStrength " + _aesEncryptionStrength);
				}
			}
			set
			{
				switch (value)
				{
					case 0:
						_aesEncryptionStrength = 0;
						break;

					case 128:
						_aesEncryptionStrength = 1;
						break;

					case 256:
						_aesEncryptionStrength = 3;
						break;

					default:
						throw new ZipException("AESKeySize must be 0, 128 or 256: " + value);
				}
			}
		}

		internal byte AESEncryptionStrength => (byte)_aesEncryptionStrength;

		internal int AESSaltLen => AESKeySize / 16;

		internal int AESOverheadSize => 12 + AESSaltLen;

		internal int EncryptionOverheadSize =>
			!IsCrypted
				? 0
				: _aesEncryptionStrength == 0
					? ZipConstants.CryptoHeaderSize
					: AESOverheadSize;

		internal void ProcessExtraData(bool localHeader)
		{
			var extraData = new ZipExtraData(this.extra);

			if (extraData.Find(0x0001))
			{

				forceZip64_ = true;

				if (extraData.ValueLength < 4)
				{
					throw new ZipException("Extra data extended Zip64 information length is invalid");
				}

				if (size == uint.MaxValue)
				{
					size = (ulong)extraData.ReadLong();
				}

				if (compressedSize == uint.MaxValue)
				{
					compressedSize = (ulong)extraData.ReadLong();
				}

				if (!localHeader && (offset == uint.MaxValue))
				{
					offset = extraData.ReadLong();
				}

			}
			else
			{
				if (
					((versionToExtract & 0xff) >= ZipConstants.VersionZip64) &&
					((size == uint.MaxValue) || (compressedSize == uint.MaxValue))
				)
				{
					throw new ZipException("Zip64 Extended information required but is missing.");
				}
			}

			DateTime = GetDateTime(extraData) ?? DateTime;
			if (method == CompressionMethod.WinZipAES)
			{
				ProcessAESExtraData(extraData);
			}
		}

		private static DateTime? GetDateTime(ZipExtraData extraData)
		{
#if RESPECT_NT_TIMESTAMP
			NTTaggedData ntData = extraData.GetData<NTTaggedData>();
			if (ntData != null)
				return ntData.LastModificationTime;
#endif

			ExtendedUnixData unixData = extraData.GetData<ExtendedUnixData>();
			if (unixData != null && unixData.Include.HasFlag(ExtendedUnixData.Flags.ModificationTime))
				return unixData.ModificationTime;

			return null;
		}

		private void ProcessAESExtraData(ZipExtraData extraData)
		{
			if (extraData.Find(0x9901))
			{
				versionToExtract = ZipConstants.VERSION_AES;            // Ver 5.1 = AES see "Version" getter

				int length = extraData.ValueLength;         // Data size currently 7
				if (length < 7)
					throw new ZipException("AES Extra Data Length " + length + " invalid.");
				int ver = extraData.ReadShort();            // Version number (1=AE-1 2=AE-2)
				int vendorId = extraData.ReadShort();       // 2-character vendor ID 0x4541 = "AE"
				int encrStrength = extraData.ReadByte();    // encryption strength 1 = 128 2 = 192 3 = 256
				int actualCompress = extraData.ReadShort(); // The actual compression method used to compress the file
				_aesVer = ver;
				_aesEncryptionStrength = encrStrength;
				method = (CompressionMethod)actualCompress;
			}
			else
				throw new ZipException("AES Extra Data missing");
		}

		public string Comment
		{
			get => comment;
			set
			{
				if ((value != null) && (value.Length > 0xffff))
				{
					throw new ArgumentOutOfRangeException(nameof(value), "cannot exceed 65535");
				}

				comment = value;
			}
		}

		public bool IsDirectory 
			=> name.Length > 0 
			&& (name[name.Length - 1] == '/' || name[name.Length - 1] == '\\') || HasDosAttributes(16);

		public bool IsFile => !IsDirectory && !HasDosAttributes(8);

		public bool IsCompressionMethodSupported() => IsCompressionMethodSupported(CompressionMethod);

		#region ICloneable Members

		public object Clone()
		{
			var result = (ZipEntry)this.MemberwiseClone();

			if (extra != null)
			{
				result.extra = new byte[extra.Length];
				Array.Copy(extra, 0, result.extra, 0, extra.Length);
			}

			return result;
		}

		#endregion ICloneable Members

		public override string ToString() => name;

		public static bool IsCompressionMethodSupported(CompressionMethod method) 
			=> method == CompressionMethod.Deflated
			|| method == CompressionMethod.Stored
			|| method == CompressionMethod.BZip2;

		public static string CleanName(string name)
		{
			if (name == null)
			{
				return string.Empty;
			}

			if (Path.IsPathRooted(name))
			{
				name = name.Substring(Path.GetPathRoot(name).Length);
			}

			name = name.Replace(@"\", "/");

			while ((name.Length > 0) && (name[0] == '/'))
			{
				name = name.Remove(0, 1);
			}
			return name;
		}

		#region Instance Fields

		private Known known;
		private int externalFileAttributes = -1;     // contains external attributes (O/S dependant)

		private ushort versionMadeBy;                   // Contains host system and version information

		private string name;
		private ulong size;
		private ulong compressedSize;
		private ushort versionToExtract;                // Version required to extract (library handles <= 2.0)
		private uint crc;
		private DateTime dateTime;

		private CompressionMethod method = CompressionMethod.Deflated;
		private byte[] extra;
		private string comment;

		private int flags;                             // general purpose bit flags

		private long zipFileIndex = -1;                // used by ZipFile
		private long offset;                           // used by ZipFile and ZipOutputStream

		private bool forceZip64_;
		private byte cryptoCheckValue_;
		private int _aesVer;                            // Version number (2 = AE-2 ?). Assigned but not used.
		private int _aesEncryptionStrength;             // Encryption strength 1 = 128 2 = 192 3 = 256

		#endregion Instance Fields
	}
}
