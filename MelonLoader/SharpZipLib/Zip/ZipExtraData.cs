using System;
using System.IO;
using MelonLoader.ICSharpCode.SharpZipLib.Core;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{

	public interface ITaggedData
	{
		short TagID { get; }

		void SetData(byte[] data, int offset, int count);

		byte[] GetData();
	}

	public class RawTaggedData : ITaggedData
	{
		public RawTaggedData(short tag)
		{
			_tag = tag;
		}

		#region ITaggedData Members

		public short TagID
		{
			get { return _tag; }
			set { _tag = value; }
		}

		public void SetData(byte[] data, int offset, int count)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			_data = new byte[count];
			Array.Copy(data, offset, _data, 0, count);
		}

		public byte[] GetData()
		{
			return _data;
		}

		#endregion ITaggedData Members

		public byte[] Data
		{
			get { return _data; }
			set { _data = value; }
		}

		#region Instance Fields

		private short _tag;

		private byte[] _data;

		#endregion Instance Fields
	}

	public class ExtendedUnixData : ITaggedData
	{
		[Flags]
		public enum Flags : byte
		{
			ModificationTime = 0x01,

			AccessTime = 0x02,

			CreateTime = 0x04,
		}

		#region ITaggedData Members

		public short TagID
		{
			get { return 0x5455; }
		}

		public void SetData(byte[] data, int index, int count)
		{
			using (MemoryStream ms = new MemoryStream(data, index, count, false))
			using (ZipHelperStream helperStream = new ZipHelperStream(ms))
			{

				_flags = (Flags)helperStream.ReadByte();
				if (((_flags & Flags.ModificationTime) != 0))
				{
					int iTime = helperStream.ReadLEInt();

					_modificationTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) +
						new TimeSpan(0, 0, 0, iTime, 0);

					if (count <= 5) return;
				}

				if ((_flags & Flags.AccessTime) != 0)
				{
					int iTime = helperStream.ReadLEInt();

					_lastAccessTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) +
						new TimeSpan(0, 0, 0, iTime, 0);
				}

				if ((_flags & Flags.CreateTime) != 0)
				{
					int iTime = helperStream.ReadLEInt();

					_createTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) +
						new TimeSpan(0, 0, 0, iTime, 0);
				}
			}
		}

		public byte[] GetData()
		{
			using (MemoryStream ms = new MemoryStream())
			using (ZipHelperStream helperStream = new ZipHelperStream(ms))
			{
				helperStream.IsStreamOwner = false;
				helperStream.WriteByte((byte)_flags);     // Flags
				if ((_flags & Flags.ModificationTime) != 0)
				{
					TimeSpan span = _modificationTime - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					var seconds = (int)span.TotalSeconds;
					helperStream.WriteLEInt(seconds);
				}
				if ((_flags & Flags.AccessTime) != 0)
				{
					TimeSpan span = _lastAccessTime - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					var seconds = (int)span.TotalSeconds;
					helperStream.WriteLEInt(seconds);
				}
				if ((_flags & Flags.CreateTime) != 0)
				{
					TimeSpan span = _createTime - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
					var seconds = (int)span.TotalSeconds;
					helperStream.WriteLEInt(seconds);
				}
				return ms.ToArray();
			}
		}

		#endregion ITaggedData Members

		public static bool IsValidValue(DateTime value)
		{
			return ((value >= new DateTime(1901, 12, 13, 20, 45, 52)) ||
					(value <= new DateTime(2038, 1, 19, 03, 14, 07)));
		}

		public DateTime ModificationTime
		{
			get { return _modificationTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				_flags |= Flags.ModificationTime;
				_modificationTime = value;
			}
		}

		public DateTime AccessTime
		{
			get { return _lastAccessTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				_flags |= Flags.AccessTime;
				_lastAccessTime = value;
			}
		}

		public DateTime CreateTime
		{
			get { return _createTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				_flags |= Flags.CreateTime;
				_createTime = value;
			}
		}

		public Flags Include
		{
			get { return _flags; }
			set { _flags = value; }
		}

		#region Instance Fields

		private Flags _flags;
		private DateTime _modificationTime = new DateTime(1970, 1, 1);
		private DateTime _lastAccessTime = new DateTime(1970, 1, 1);
		private DateTime _createTime = new DateTime(1970, 1, 1);

		#endregion Instance Fields
	}

	public class NTTaggedData : ITaggedData
	{
		public short TagID
		{
			get { return 10; }
		}

		public void SetData(byte[] data, int index, int count)
		{
			using (MemoryStream ms = new MemoryStream(data, index, count, false))
			using (ZipHelperStream helperStream = new ZipHelperStream(ms))
			{
				helperStream.ReadLEInt(); // Reserved
				while (helperStream.Position < helperStream.Length)
				{
					int ntfsTag = helperStream.ReadLEShort();
					int ntfsLength = helperStream.ReadLEShort();
					if (ntfsTag == 1)
					{
						if (ntfsLength >= 24)
						{
							long lastModificationTicks = helperStream.ReadLELong();
							_lastModificationTime = DateTime.FromFileTimeUtc(lastModificationTicks);

							long lastAccessTicks = helperStream.ReadLELong();
							_lastAccessTime = DateTime.FromFileTimeUtc(lastAccessTicks);

							long createTimeTicks = helperStream.ReadLELong();
							_createTime = DateTime.FromFileTimeUtc(createTimeTicks);
						}
						break;
					}
					else
					{
						helperStream.Seek(ntfsLength, SeekOrigin.Current);
					}
				}
			}
		}

		public byte[] GetData()
		{
			using (MemoryStream ms = new MemoryStream())
			using (ZipHelperStream helperStream = new ZipHelperStream(ms))
			{
				helperStream.IsStreamOwner = false;
				helperStream.WriteLEInt(0);       // Reserved
				helperStream.WriteLEShort(1);     // Tag
				helperStream.WriteLEShort(24);    // Length = 3 x 8.
				helperStream.WriteLELong(_lastModificationTime.ToFileTimeUtc());
				helperStream.WriteLELong(_lastAccessTime.ToFileTimeUtc());
				helperStream.WriteLELong(_createTime.ToFileTimeUtc());
				return ms.ToArray();
			}
		}

		public static bool IsValidValue(DateTime value)
		{
			bool result = true;
			try
			{
				value.ToFileTimeUtc();
			}
			catch
			{
				result = false;
			}
			return result;
		}

		public DateTime LastModificationTime
		{
			get { return _lastModificationTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}
				_lastModificationTime = value;
			}
		}

		public DateTime CreateTime
		{
			get { return _createTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}
				_createTime = value;
			}
		}

		public DateTime LastAccessTime
		{
			get { return _lastAccessTime; }
			set
			{
				if (!IsValidValue(value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}
				_lastAccessTime = value;
			}
		}

		#region Instance Fields

		private DateTime _lastAccessTime = DateTime.FromFileTimeUtc(0);
		private DateTime _lastModificationTime = DateTime.FromFileTimeUtc(0);
		private DateTime _createTime = DateTime.FromFileTimeUtc(0);

		#endregion Instance Fields
	}

	internal interface ITaggedDataFactory
	{
		ITaggedData Create(short tag, byte[] data, int offset, int count);
	}

	sealed public class ZipExtraData : IDisposable
	{
		#region Constructors

		public ZipExtraData()
		{
			Clear();
		}

		public ZipExtraData(byte[] data)
		{
			if (data == null)
			{
				_data = Empty.Array<byte>();
			}
			else
			{
				_data = data;
			}
		}

		#endregion Constructors

		public byte[] GetEntryData()
		{
			if (Length > ushort.MaxValue)
			{
				throw new ZipException("Data exceeds maximum length");
			}

			return (byte[])_data.Clone();
		}

		public void Clear()
		{
			if ((_data == null) || (_data.Length != 0))
			{
				_data = Empty.Array<byte>();
			}
		}

		public int Length
		{
			get { return _data.Length; }
		}

		public Stream GetStreamForTag(int tag)
		{
			Stream result = null;
			if (Find(tag))
			{
				result = new MemoryStream(_data, _index, _readValueLength, false);
			}
			return result;
		}

		public T GetData<T>()
			where T : class, ITaggedData, new()
		{
			T result = new T();
			if (Find(result.TagID))
			{
				result.SetData(_data, _readValueStart, _readValueLength);
				return result;
			}
			else return null;
		}

		public int ValueLength
		{
			get { return _readValueLength; }
		}

		public int CurrentReadIndex
		{
			get { return _index; }
		}

		public int UnreadCount
		{
			get
			{
				if ((_readValueStart > _data.Length) ||
					(_readValueStart < 4))
				{
					throw new ZipException("Find must be called before calling a Read method");
				}

				return _readValueStart + _readValueLength - _index;
			}
		}

		public bool Find(int headerID)
		{
			_readValueStart = _data.Length;
			_readValueLength = 0;
			_index = 0;

			int localLength = _readValueStart;
			int localTag = headerID - 1;

			while ((localTag != headerID) && (_index < _data.Length - 3))
			{
				localTag = ReadShortInternal();
				localLength = ReadShortInternal();
				if (localTag != headerID)
				{
					_index += localLength;
				}
			}

			bool result = (localTag == headerID) && ((_index + localLength) <= _data.Length);

			if (result)
			{
				_readValueStart = _index;
				_readValueLength = localLength;
			}

			return result;
		}

		public void AddEntry(ITaggedData taggedData)
		{
			if (taggedData == null)
			{
				throw new ArgumentNullException(nameof(taggedData));
			}
			AddEntry(taggedData.TagID, taggedData.GetData());
		}

		public void AddEntry(int headerID, byte[] fieldData)
		{
			if ((headerID > ushort.MaxValue) || (headerID < 0))
			{
				throw new ArgumentOutOfRangeException(nameof(headerID));
			}

			int addLength = (fieldData == null) ? 0 : fieldData.Length;

			if (addLength > ushort.MaxValue)
			{
				throw new ArgumentOutOfRangeException(nameof(fieldData), "exceeds maximum length");
			}

			int newLength = _data.Length + addLength + 4;

			if (Find(headerID))
			{
				newLength -= (ValueLength + 4);
			}

			if (newLength > ushort.MaxValue)
			{
				throw new ZipException("Data exceeds maximum length");
			}

			Delete(headerID);

			byte[] newData = new byte[newLength];
			_data.CopyTo(newData, 0);
			int index = _data.Length;
			_data = newData;
			SetShort(ref index, headerID);
			SetShort(ref index, addLength);
			if (fieldData != null)
			{
				fieldData.CopyTo(newData, index);
			}
		}

		public void StartNewEntry()
		{
			_newEntry = new MemoryStream();
		}

		public void AddNewEntry(int headerID)
		{
			byte[] newData = _newEntry.ToArray();
			_newEntry = null;
			AddEntry(headerID, newData);
		}

		public void AddData(byte data)
		{
			_newEntry.WriteByte(data);
		}

		public void AddData(byte[] data)
		{
			if (data == null)
			{
				throw new ArgumentNullException(nameof(data));
			}

			_newEntry.Write(data, 0, data.Length);
		}

		public void AddLeShort(int toAdd)
		{
			unchecked
			{
				_newEntry.WriteByte((byte)toAdd);
				_newEntry.WriteByte((byte)(toAdd >> 8));
			}
		}

		public void AddLeInt(int toAdd)
		{
			unchecked
			{
				AddLeShort((short)toAdd);
				AddLeShort((short)(toAdd >> 16));
			}
		}

		public void AddLeLong(long toAdd)
		{
			unchecked
			{
				AddLeInt((int)(toAdd & 0xffffffff));
				AddLeInt((int)(toAdd >> 32));
			}
		}

		public bool Delete(int headerID)
		{
			bool result = false;

			if (Find(headerID))
			{
				result = true;
				int trueStart = _readValueStart - 4;

				byte[] newData = new byte[_data.Length - (ValueLength + 4)];
				Array.Copy(_data, 0, newData, 0, trueStart);

				int trueEnd = trueStart + ValueLength + 4;
				Array.Copy(_data, trueEnd, newData, trueStart, _data.Length - trueEnd);
				_data = newData;
			}
			return result;
		}

		#region Reading Support

		public long ReadLong()
		{
			ReadCheck(8);
			return (ReadInt() & 0xffffffff) | (((long)ReadInt()) << 32);
		}

		public int ReadInt()
		{
			ReadCheck(4);

			int result = _data[_index] + (_data[_index + 1] << 8) +
				(_data[_index + 2] << 16) + (_data[_index + 3] << 24);
			_index += 4;
			return result;
		}

		public int ReadShort()
		{
			ReadCheck(2);
			int result = _data[_index] + (_data[_index + 1] << 8);
			_index += 2;
			return result;
		}

		public int ReadByte()
		{
			int result = -1;
			if ((_index < _data.Length) && (_readValueStart + _readValueLength > _index))
			{
				result = _data[_index];
				_index += 1;
			}
			return result;
		}

		public void Skip(int amount)
		{
			ReadCheck(amount);
			_index += amount;
		}

		private void ReadCheck(int length)
		{
			if ((_readValueStart > _data.Length) ||
				(_readValueStart < 4))
			{
				throw new ZipException("Find must be called before calling a Read method");
			}

			if (_index > _readValueStart + _readValueLength - length)
			{
				throw new ZipException("End of extra data");
			}

			if (_index + length < 4)
			{
				throw new ZipException("Cannot read before start of tag");
			}
		}

		private int ReadShortInternal()
		{
			if (_index > _data.Length - 2)
			{
				throw new ZipException("End of extra data");
			}

			int result = _data[_index] + (_data[_index + 1] << 8);
			_index += 2;
			return result;
		}

		private void SetShort(ref int index, int source)
		{
			_data[index] = (byte)source;
			_data[index + 1] = (byte)(source >> 8);
			index += 2;
		}

		#endregion Reading Support

		#region IDisposable Members

		public void Dispose()
		{
			if (_newEntry != null)
			{
				_newEntry.Dispose();
			}
		}

		#endregion IDisposable Members

		#region Instance Fields

		private int _index;
		private int _readValueStart;
		private int _readValueLength;

		private MemoryStream _newEntry;
		private byte[] _data;

		#endregion Instance Fields
	}
}
