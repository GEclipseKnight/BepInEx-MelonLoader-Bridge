using MelonLoader.ICSharpCode.SharpZipLib.Checksum;
using MelonLoader.ICSharpCode.SharpZipLib.Core;
using MelonLoader.ICSharpCode.SharpZipLib.Encryption;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression;
using MelonLoader.ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace MelonLoader.ICSharpCode.SharpZipLib.Zip
{
	#region Keys Required Event Args

	public class KeysRequiredEventArgs : EventArgs
	{
		#region Constructors

		public KeysRequiredEventArgs(string name)
		{
			fileName = name;
		}

		public KeysRequiredEventArgs(string name, byte[] keyValue)
		{
			fileName = name;
			key = keyValue;
		}

		#endregion Constructors

		#region Properties

		public string FileName
		{
			get { return fileName; }
		}

		public byte[] Key
		{
			get { return key; }
			set { key = value; }
		}

		#endregion Properties

		#region Instance Fields

		private readonly string fileName;
		private byte[] key;

		#endregion Instance Fields
	}

	#endregion Keys Required Event Args

	#region Test Definitions

	public enum TestStrategy
	{
		FindFirstError,

		FindAllErrors,
	}

	public enum TestOperation
	{
		Initialising,

		EntryHeader,

		EntryData,

		EntryComplete,

		MiscellaneousTests,

		Complete,
	}

	public class TestStatus
	{
		#region Constructors

		public TestStatus(ZipFile file)
		{
			file_ = file;
		}

		#endregion Constructors

		#region Properties

		public TestOperation Operation
		{
			get { return operation_; }
		}

		public ZipFile File
		{
			get { return file_; }
		}

		public ZipEntry Entry
		{
			get { return entry_; }
		}

		public int ErrorCount
		{
			get { return errorCount_; }
		}

		public long BytesTested
		{
			get { return bytesTested_; }
		}

		public bool EntryValid
		{
			get { return entryValid_; }
		}

		#endregion Properties

		#region Internal API

		internal void AddError()
		{
			errorCount_++;
			entryValid_ = false;
		}

		internal void SetOperation(TestOperation operation)
		{
			operation_ = operation;
		}

		internal void SetEntry(ZipEntry entry)
		{
			entry_ = entry;
			entryValid_ = true;
			bytesTested_ = 0;
		}

		internal void SetBytesTested(long value)
		{
			bytesTested_ = value;
		}

		#endregion Internal API

		#region Instance Fields

		private readonly ZipFile file_;
		private ZipEntry entry_;
		private bool entryValid_;
		private int errorCount_;
		private long bytesTested_;
		private TestOperation operation_;

		#endregion Instance Fields
	}

	public delegate void ZipTestResultHandler(TestStatus status, string message);

	#endregion Test Definitions

	#region Update Definitions

	public enum FileUpdateMode
	{
		Safe,

		Direct,
	}

	#endregion Update Definitions

	#region ZipFile Class

	public class ZipFile : IEnumerable, IDisposable
	{
		#region KeyHandling

		public delegate void KeysRequiredEventHandler(
			object sender,
			KeysRequiredEventArgs e
		);

		public KeysRequiredEventHandler KeysRequired;

		private void OnKeysRequired(string fileName)
		{
			if (KeysRequired != null)
			{
				var krea = new KeysRequiredEventArgs(fileName, key);
				KeysRequired(this, krea);
				key = krea.Key;
			}
		}

		private byte[] Key
		{
			get { return key; }
			set { key = value; }
		}

		public string Password
		{
			set
			{
				if (string.IsNullOrEmpty(value))
				{
					key = null;
				}
				else
				{
					key = PkzipClassic.GenerateKeys(ZipStrings.ConvertToArray(value));
				}

				rawPassword_ = value;
			}
		}

		private bool HaveKeys
		{
			get { return key != null; }
		}

		#endregion KeyHandling

		#region Constructors

		public ZipFile(string name)
		{
			name_ = name ?? throw new ArgumentNullException(nameof(name));

			baseStream_ = File.Open(name, FileMode.Open, FileAccess.Read, FileShare.Read);
			isStreamOwner = true;

			try
			{
				ReadEntries();
			}
			catch
			{
				DisposeInternal(true);
				throw;
			}
		}

		public ZipFile(FileStream file) :
			this(file, false)
		{

		}

		public ZipFile(FileStream file, bool leaveOpen)
		{
			if (file == null)
			{
				throw new ArgumentNullException(nameof(file));
			}

			if (!file.CanSeek)
			{
				throw new ArgumentException("Stream is not seekable", nameof(file));
			}

			baseStream_ = file;
			name_ = file.Name;
			isStreamOwner = !leaveOpen;

			try
			{
				ReadEntries();
			}
			catch
			{
				DisposeInternal(true);
				throw;
			}
		}

		public ZipFile(Stream stream) :
			this(stream, false)
		{

		}

		public ZipFile(Stream stream, bool leaveOpen)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			if (!stream.CanSeek)
			{
				throw new ArgumentException("Stream is not seekable", nameof(stream));
			}

			baseStream_ = stream;
			isStreamOwner = !leaveOpen;

			if (baseStream_.Length > 0)
			{
				try
				{
					ReadEntries();
				}
				catch
				{
					DisposeInternal(true);
					throw;
				}
			}
			else
			{
				entries_ = Empty.Array<ZipEntry>();
				isNewArchive_ = true;
			}
		}

		internal ZipFile()
		{
			entries_ = Empty.Array<ZipEntry>();
			isNewArchive_ = true;
		}

		#endregion Constructors

		#region Destructors and Closing

		~ZipFile()
		{
			Dispose(false);
		}

		public void Close()
		{
			DisposeInternal(true);
			GC.SuppressFinalize(this);
		}

		#endregion Destructors and Closing

		#region Creators

		public static ZipFile Create(string fileName)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			FileStream fs = File.Create(fileName);

			return new ZipFile
			{
				name_ = fileName,
				baseStream_ = fs,
				isStreamOwner = true
			};
		}

		public static ZipFile Create(Stream outStream)
		{
			if (outStream == null)
			{
				throw new ArgumentNullException(nameof(outStream));
			}

			if (!outStream.CanWrite)
			{
				throw new ArgumentException("Stream is not writeable", nameof(outStream));
			}

			if (!outStream.CanSeek)
			{
				throw new ArgumentException("Stream is not seekable", nameof(outStream));
			}

			var result = new ZipFile
			{
				baseStream_ = outStream
			};
			return result;
		}

		#endregion Creators

		#region Properties

		public bool IsStreamOwner
		{
			get { return isStreamOwner; }
			set { isStreamOwner = value; }
		}

		public bool IsEmbeddedArchive
		{
			get { return offsetOfFirstEntry > 0; }
		}

		public bool IsNewArchive
		{
			get { return isNewArchive_; }
		}

		public string ZipFileComment
		{
			get { return comment_; }
		}

		public string Name
		{
			get { return name_; }
		}

		[Obsolete("Use the Count property instead")]
		public int Size
		{
			get
			{
				return entries_.Length;
			}
		}

		public long Count
		{
			get
			{
				return entries_.Length;
			}
		}

		[System.Runtime.CompilerServices.IndexerNameAttribute("EntryByIndex")]
		public ZipEntry this[int index]
		{
			get
			{
				return (ZipEntry)entries_[index].Clone();
			}
		}

		#endregion Properties

		#region Input Handling

		public IEnumerator GetEnumerator()
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			return new ZipEntryEnumerator(entries_);
		}

		public int FindEntry(string name, bool ignoreCase)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			for (int i = 0; i < entries_.Length; i++)
			{
				if (string.Compare(name, entries_[i].Name, ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) == 0)
				{
					return i;
				}
			}
			return -1;
		}

		public ZipEntry GetEntry(string name)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			int index = FindEntry(name, true);
			return (index >= 0) ? (ZipEntry)entries_[index].Clone() : null;
		}

		public Stream GetInputStream(ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			long index = entry.ZipFileIndex;
			if ((index < 0) || (index >= entries_.Length) || (entries_[index].Name != entry.Name))
			{
				index = FindEntry(entry.Name, true);
				if (index < 0)
				{
					throw new ZipException("Entry cannot be found");
				}
			}
			return GetInputStream(index);
		}

		public Stream GetInputStream(long entryIndex)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			long start = LocateEntry(entries_[entryIndex]);
			CompressionMethod method = entries_[entryIndex].CompressionMethod;
			Stream result = new PartialInputStream(this, start, entries_[entryIndex].CompressedSize);

			if (entries_[entryIndex].IsCrypted == true)
			{
				result = CreateAndInitDecryptionStream(result, entries_[entryIndex]);
				if (result == null)
				{
					throw new ZipException("Unable to decrypt this entry");
				}
			}

			switch (method)
			{
				case CompressionMethod.Stored:
					break;

				case CompressionMethod.Deflated:
					result = new InflaterInputStream(result, new Inflater(true));
					break;

				case CompressionMethod.BZip2:
					result = new BZip2.BZip2InputStream(result);
					break;

				default:
					throw new ZipException("Unsupported compression method " + method);
			}

			return result;
		}

		#endregion Input Handling

		#region Archive Testing

		public bool TestArchive(bool testData)
		{
			return TestArchive(testData, TestStrategy.FindFirstError, null);
		}

		public bool TestArchive(bool testData, TestStrategy strategy, ZipTestResultHandler resultHandler)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			var status = new TestStatus(this);

			resultHandler?.Invoke(status, null);

			HeaderTest test = testData ? (HeaderTest.Header | HeaderTest.Extract) : HeaderTest.Header;

			bool testing = true;

			try
			{
				int entryIndex = 0;

				while (testing && (entryIndex < Count))
				{
					if (resultHandler != null)
					{
						status.SetEntry(this[entryIndex]);
						status.SetOperation(TestOperation.EntryHeader);
						resultHandler(status, null);
					}

					try
					{
						TestLocalHeader(this[entryIndex], test);
					}
					catch (ZipException ex)
					{
						status.AddError();

						resultHandler?.Invoke(status, $"Exception during test - '{ex.Message}'");

						testing &= strategy != TestStrategy.FindFirstError;
					}

					if (testing && testData && this[entryIndex].IsFile)
					{
						var checkCRC = this[entryIndex].AESKeySize == 0;

						if (resultHandler != null)
						{
							status.SetOperation(TestOperation.EntryData);
							resultHandler(status, null);
						}

						var crc = new Crc32();

						using (Stream entryStream = this.GetInputStream(this[entryIndex]))
						{
							byte[] buffer = new byte[4096];
							long totalBytes = 0;
							int bytesRead;
							while ((bytesRead = entryStream.Read(buffer, 0, buffer.Length)) > 0)
							{
								if (checkCRC)
								{
									crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
								}

								if (resultHandler != null)
								{
									totalBytes += bytesRead;
									status.SetBytesTested(totalBytes);
									resultHandler(status, null);
								}
							}
						}

						if (checkCRC && this[entryIndex].Crc != crc.Value)
						{
							status.AddError();

							resultHandler?.Invoke(status, "CRC mismatch");

							testing &= strategy != TestStrategy.FindFirstError;
						}

						if ((this[entryIndex].Flags & (int)GeneralBitFlags.Descriptor) != 0)
						{
							var helper = new ZipHelperStream(baseStream_);
							var data = new DescriptorData();
							helper.ReadDataDescriptor(this[entryIndex].LocalHeaderRequiresZip64, data);

							if (checkCRC && this[entryIndex].Crc != data.Crc)
							{
								status.AddError();
								resultHandler?.Invoke(status, "Descriptor CRC mismatch");
							}

							if (this[entryIndex].CompressedSize != data.CompressedSize)
							{
								status.AddError();
								resultHandler?.Invoke(status, "Descriptor compressed size mismatch");
							}

							if (this[entryIndex].Size != data.Size)
							{
								status.AddError();
								resultHandler?.Invoke(status, "Descriptor size mismatch");
							}
						}
					}

					if (resultHandler != null)
					{
						status.SetOperation(TestOperation.EntryComplete);
						resultHandler(status, null);
					}

					entryIndex += 1;
				}

				if (resultHandler != null)
				{
					status.SetOperation(TestOperation.MiscellaneousTests);
					resultHandler(status, null);
				}

			}
			catch (Exception ex)
			{
				status.AddError();

				resultHandler?.Invoke(status, $"Exception during test - '{ex.Message}'");
			}

			if (resultHandler != null)
			{
				status.SetOperation(TestOperation.Complete);
				status.SetEntry(null);
				resultHandler(status, null);
			}

			return (status.ErrorCount == 0);
		}

		[Flags]
		private enum HeaderTest
		{
			Extract = 0x01,     // Check that this header represents an entry whose data can be extracted
			Header = 0x02,     // Check that this header contents are valid
		}

		private long TestLocalHeader(ZipEntry entry, HeaderTest tests)
		{
			lock (baseStream_)
			{
				bool testHeader = (tests & HeaderTest.Header) != 0;
				bool testData = (tests & HeaderTest.Extract) != 0;

				var entryAbsOffset = offsetOfFirstEntry + entry.Offset;
				
				baseStream_.Seek(entryAbsOffset, SeekOrigin.Begin);
				var signature = (int)ReadLEUint();

				if (signature != ZipConstants.LocalHeaderSignature)
				{
					throw new ZipException(string.Format("Wrong local header signature at 0x{0:x}, expected 0x{1:x8}, actual 0x{2:x8}",
						entryAbsOffset, ZipConstants.LocalHeaderSignature, signature));
				}

				var extractVersion = (short)(ReadLEUshort() & 0x00ff);
				var localFlags = (short)ReadLEUshort();
				var compressionMethod = (short)ReadLEUshort();
				var fileTime = (short)ReadLEUshort();
				var fileDate = (short)ReadLEUshort();
				uint crcValue = ReadLEUint();
				long compressedSize = ReadLEUint();
				long size = ReadLEUint();
				int storedNameLength = ReadLEUshort();
				int extraDataLength = ReadLEUshort();

				byte[] nameData = new byte[storedNameLength];
				StreamUtils.ReadFully(baseStream_, nameData);

				byte[] extraData = new byte[extraDataLength];
				StreamUtils.ReadFully(baseStream_, extraData);

				var localExtraData = new ZipExtraData(extraData);

				if (localExtraData.Find(1))
				{

					size = localExtraData.ReadLong();
					compressedSize = localExtraData.ReadLong();

					if ((localFlags & (int)GeneralBitFlags.Descriptor) != 0)
					{
						if ((size != -1) && (size != entry.Size))
						{
							throw new ZipException("Size invalid for descriptor");
						}

						if ((compressedSize != -1) && (compressedSize != entry.CompressedSize))
						{
							throw new ZipException("Compressed size invalid for descriptor");
						}
					}
				}
				else
				{
					if ((extractVersion >= ZipConstants.VersionZip64) &&
						(((uint)size == uint.MaxValue) || ((uint)compressedSize == uint.MaxValue)))
					{
						throw new ZipException("Required Zip64 extended information missing");
					}
				}

				if (testData)
				{
					if (entry.IsFile)
					{
						if (!entry.IsCompressionMethodSupported())
						{
							throw new ZipException("Compression method not supported");
						}

						if ((extractVersion > ZipConstants.VersionMadeBy)
							|| ((extractVersion > 20) && (extractVersion < ZipConstants.VersionZip64)))
						{
							throw new ZipException(string.Format("Version required to extract this entry not supported ({0})", extractVersion));
						}

						if ((localFlags & (int)(GeneralBitFlags.Patched | GeneralBitFlags.StrongEncryption | GeneralBitFlags.EnhancedCompress | GeneralBitFlags.HeaderMasked)) != 0)
						{
							throw new ZipException("The library does not support the zip version required to extract this entry");
						}
					}
				}

				if (testHeader)
				{
					if ((extractVersion <= 63) &&   // Ignore later versions as we dont know about them..
						(extractVersion != 10) &&
						(extractVersion != 11) &&
						(extractVersion != 20) &&
						(extractVersion != 21) &&
						(extractVersion != 25) &&
						(extractVersion != 27) &&
						(extractVersion != 45) &&
						(extractVersion != 46) &&
						(extractVersion != 50) &&
						(extractVersion != 51) &&
						(extractVersion != 52) &&
						(extractVersion != 61) &&
						(extractVersion != 62) &&
						(extractVersion != 63)
						)
					{
						throw new ZipException(string.Format("Version required to extract this entry is invalid ({0})", extractVersion));
					}

					if ((localFlags & (int)(GeneralBitFlags.ReservedPKware4 | GeneralBitFlags.ReservedPkware14 | GeneralBitFlags.ReservedPkware15)) != 0)
					{
						throw new ZipException("Reserved bit flags cannot be set.");
					}

					if (((localFlags & (int)GeneralBitFlags.Encrypted) != 0) && (extractVersion < 20))
					{
						throw new ZipException(string.Format("Version required to extract this entry is too low for encryption ({0})", extractVersion));
					}

					if ((localFlags & (int)GeneralBitFlags.StrongEncryption) != 0)
					{
						if ((localFlags & (int)GeneralBitFlags.Encrypted) == 0)
						{
							throw new ZipException("Strong encryption flag set but encryption flag is not set");
						}

						if (extractVersion < 50)
						{
							throw new ZipException(string.Format("Version required to extract this entry is too low for encryption ({0})", extractVersion));
						}
					}

					if (((localFlags & (int)GeneralBitFlags.Patched) != 0) && (extractVersion < 27))
					{
						throw new ZipException(string.Format("Patched data requires higher version than ({0})", extractVersion));
					}

					if (localFlags != entry.Flags)
					{
						throw new ZipException("Central header/local header flags mismatch");
					}

					if (entry.CompressionMethodForHeader != (CompressionMethod)compressionMethod)
					{
						throw new ZipException("Central header/local header compression method mismatch");
					}

					if (entry.Version != extractVersion)
					{
						throw new ZipException("Extract version mismatch");
					}

					if ((localFlags & (int)GeneralBitFlags.StrongEncryption) != 0)
					{
						if (extractVersion < 62)
						{
							throw new ZipException("Strong encryption flag set but version not high enough");
						}
					}

					if ((localFlags & (int)GeneralBitFlags.HeaderMasked) != 0)
					{
						if ((fileTime != 0) || (fileDate != 0))
						{
							throw new ZipException("Header masked set but date/time values non-zero");
						}
					}

					if ((localFlags & (int)GeneralBitFlags.Descriptor) == 0)
					{
						if (crcValue != (uint)entry.Crc)
						{
							throw new ZipException("Central header/local header crc mismatch");
						}
					}

					if ((size == 0) && (compressedSize == 0))
					{
						if (crcValue != 0)
						{
							throw new ZipException("Invalid CRC for empty entry");
						}
					}

					if (entry.Name.Length > storedNameLength)
					{
						throw new ZipException("File name length mismatch");
					}

					string localName = ZipStrings.ConvertToStringExt(localFlags, nameData);

					if (localName != entry.Name)
					{
						throw new ZipException("Central header and local header file name mismatch");
					}

					if (entry.IsDirectory)
					{
						if (size > 0)
						{
							throw new ZipException("Directory cannot have size");
						}

						if (entry.IsCrypted)
						{
							if (compressedSize > entry.EncryptionOverheadSize + 2)
							{
								throw new ZipException("Directory compressed size invalid");
							}
						}
						else if (compressedSize > 2)
						{
							throw new ZipException("Directory compressed size invalid");
						}
					}

					if (!ZipNameTransform.IsValidName(localName, true))
					{
						throw new ZipException("Name is invalid");
					}
				}


				if (((localFlags & (int)GeneralBitFlags.Descriptor) == 0) ||
					((size > 0 || compressedSize > 0) && entry.Size > 0))
				{
					if ((size != 0)
						&& (size != entry.Size))
					{
						throw new ZipException(
							string.Format("Size mismatch between central header({0}) and local header({1})",
								entry.Size, size));
					}

					if ((compressedSize != 0)
						&& (compressedSize != entry.CompressedSize && compressedSize != 0xFFFFFFFF && compressedSize != -1))
					{
						throw new ZipException(
							string.Format("Compressed size mismatch between central header({0}) and local header({1})",
							entry.CompressedSize, compressedSize));
					}
				}

				int extraLength = storedNameLength + extraDataLength;
				return offsetOfFirstEntry + entry.Offset + ZipConstants.LocalHeaderBaseSize + extraLength;
			}
		}

		#endregion Archive Testing

		#region Updating

		private const int DefaultBufferSize = 4096;

		private enum UpdateCommand
		{
			Copy,       // Copy original file contents.
			Modify,     // Change encryption, compression, attributes, name, time etc, of an existing file.
			Add,        // Add a new file to the archive.
		}

		#region Properties

		public INameTransform NameTransform
		{
			get
			{
				return updateEntryFactory_.NameTransform;
			}

			set
			{
				updateEntryFactory_.NameTransform = value;
			}
		}

		public IEntryFactory EntryFactory
		{
			get
			{
				return updateEntryFactory_;
			}

			set
			{
				if (value == null)
				{
					updateEntryFactory_ = new ZipEntryFactory();
				}
				else
				{
					updateEntryFactory_ = value;
				}
			}
		}

		public int BufferSize
		{
			get { return bufferSize_; }
			set
			{
				if (value < 1024)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "cannot be below 1024");
				}

				if (bufferSize_ != value)
				{
					bufferSize_ = value;
					copyBuffer_ = null;
				}
			}
		}

		public bool IsUpdating
		{
			get { return updates_ != null; }
		}

		public UseZip64 UseZip64
		{
			get { return useZip64_; }
			set { useZip64_ = value; }
		}

		#endregion Properties

		#region Immediate updating


		#endregion Immediate updating

		#region Deferred Updating

		public void BeginUpdate(IArchiveStorage archiveStorage, IDynamicDataSource dataSource)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			if (IsEmbeddedArchive)
			{
				throw new ZipException("Cannot update embedded/SFX archives");
			}

			archiveStorage_ = archiveStorage ?? throw new ArgumentNullException(nameof(archiveStorage));
			updateDataSource_ = dataSource ?? throw new ArgumentNullException(nameof(dataSource));


			updateIndex_ = new Dictionary<string, int>();

			updates_ = new List<ZipUpdate>(entries_.Length);
			foreach (ZipEntry entry in entries_)
			{
				int index = updates_.Count;
				updates_.Add(new ZipUpdate(entry));
				updateIndex_.Add(entry.Name, index);
			}

			updates_.Sort(new UpdateComparer());

			int idx = 0;
			foreach (ZipUpdate update in updates_)
			{
				if (idx == updates_.Count - 1)
					break;

				update.OffsetBasedSize = ((ZipUpdate)updates_[idx + 1]).Entry.Offset - update.Entry.Offset;
				idx++;
			}
			updateCount_ = updates_.Count;

			contentsEdited_ = false;
			commentEdited_ = false;
			newComment_ = null;
		}

		public void BeginUpdate(IArchiveStorage archiveStorage)
		{
			BeginUpdate(archiveStorage, new DynamicDiskDataSource());
		}

		public void BeginUpdate()
		{
			if (Name == null)
			{
				BeginUpdate(new MemoryArchiveStorage(), new DynamicDiskDataSource());
			}
			else
			{
				BeginUpdate(new DiskArchiveStorage(this), new DynamicDiskDataSource());
			}
		}

		public void CommitUpdate()
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			CheckUpdating();

			try
			{
				updateIndex_.Clear();
				updateIndex_ = null;

				if (contentsEdited_)
				{
					RunUpdates();
				}
				else if (commentEdited_)
				{
					UpdateCommentOnly();
				}
				else
				{
					if (entries_.Length == 0)
					{
						byte[] theComment = (newComment_ != null) ? newComment_.RawComment : ZipStrings.ConvertToArray(comment_);
						using (ZipHelperStream zhs = new ZipHelperStream(baseStream_))
						{
							zhs.WriteEndOfCentralDirectory(0, 0, 0, theComment);
						}
					}
				}
			}
			finally
			{
				PostUpdateCleanup();
			}
		}

		public void AbortUpdate()
		{
			PostUpdateCleanup();
		}

		public void SetComment(string comment)
		{
			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			CheckUpdating();

			newComment_ = new ZipString(comment);

			if (newComment_.RawLength > 0xffff)
			{
				newComment_ = null;
				throw new ZipException("Comment length exceeds maximum - 65535");
			}

			commentEdited_ = true;
		}

		#endregion Deferred Updating

		#region Adding Entries

		private void AddUpdate(ZipUpdate update)
		{
			contentsEdited_ = true;

			int index = FindExistingUpdate(update.Entry.Name, isEntryName: true);

			if (index >= 0)
			{
				if (updates_[index] == null)
				{
					updateCount_ += 1;
				}

				updates_[index] = update;
			}
			else
			{
				index = updates_.Count;
				updates_.Add(update);
				updateCount_ += 1;
				updateIndex_.Add(update.Entry.Name, index);
			}
		}

		public void Add(string fileName, CompressionMethod compressionMethod, bool useUnicodeText)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			if (isDisposed_)
			{
				throw new ObjectDisposedException("ZipFile");
			}

			CheckSupportedCompressionMethod(compressionMethod);
			CheckUpdating();
			contentsEdited_ = true;

			ZipEntry entry = EntryFactory.MakeFileEntry(fileName);
			entry.IsUnicodeText = useUnicodeText;
			entry.CompressionMethod = compressionMethod;

			AddUpdate(new ZipUpdate(fileName, entry));
		}

		public void Add(string fileName, CompressionMethod compressionMethod)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			CheckSupportedCompressionMethod(compressionMethod);
			CheckUpdating();
			contentsEdited_ = true;

			ZipEntry entry = EntryFactory.MakeFileEntry(fileName);
			entry.CompressionMethod = compressionMethod;
			AddUpdate(new ZipUpdate(fileName, entry));
		}

		public void Add(string fileName)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			CheckUpdating();
			AddUpdate(new ZipUpdate(fileName, EntryFactory.MakeFileEntry(fileName)));
		}

		public void Add(string fileName, string entryName)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			if (entryName == null)
			{
				throw new ArgumentNullException(nameof(entryName));
			}

			CheckUpdating();
			AddUpdate(new ZipUpdate(fileName, EntryFactory.MakeFileEntry(fileName, entryName, true)));
		}

		public void Add(IStaticDataSource dataSource, string entryName)
		{
			if (dataSource == null)
			{
				throw new ArgumentNullException(nameof(dataSource));
			}

			if (entryName == null)
			{
				throw new ArgumentNullException(nameof(entryName));
			}

			CheckUpdating();
			AddUpdate(new ZipUpdate(dataSource, EntryFactory.MakeFileEntry(entryName, false)));
		}

		public void Add(IStaticDataSource dataSource, string entryName, CompressionMethod compressionMethod)
		{
			if (dataSource == null)
			{
				throw new ArgumentNullException(nameof(dataSource));
			}

			if (entryName == null)
			{
				throw new ArgumentNullException(nameof(entryName));
			}

			CheckSupportedCompressionMethod(compressionMethod);
			CheckUpdating();

			ZipEntry entry = EntryFactory.MakeFileEntry(entryName, false);
			entry.CompressionMethod = compressionMethod;

			AddUpdate(new ZipUpdate(dataSource, entry));
		}

		public void Add(IStaticDataSource dataSource, string entryName, CompressionMethod compressionMethod, bool useUnicodeText)
		{
			if (dataSource == null)
			{
				throw new ArgumentNullException(nameof(dataSource));
			}

			if (entryName == null)
			{
				throw new ArgumentNullException(nameof(entryName));
			}

			CheckSupportedCompressionMethod(compressionMethod);
			CheckUpdating();

			ZipEntry entry = EntryFactory.MakeFileEntry(entryName, false);
			entry.IsUnicodeText = useUnicodeText;
			entry.CompressionMethod = compressionMethod;

			AddUpdate(new ZipUpdate(dataSource, entry));
		}

		public void Add(ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			CheckUpdating();

			if ((entry.Size != 0) || (entry.CompressedSize != 0))
			{
				throw new ZipException("Entry cannot have any data");
			}

			AddUpdate(new ZipUpdate(UpdateCommand.Add, entry));
		}

		public void Add(IStaticDataSource dataSource, ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			if (dataSource == null)
			{
				throw new ArgumentNullException(nameof(dataSource));
			}

			if (entry.AESKeySize > 0)
			{
				throw new NotSupportedException("Creation of AES encrypted entries is not supported");
			}

			CheckSupportedCompressionMethod(entry.CompressionMethod);
			CheckUpdating();

			AddUpdate(new ZipUpdate(dataSource, entry));
		}

		public void AddDirectory(string directoryName)
		{
			if (directoryName == null)
			{
				throw new ArgumentNullException(nameof(directoryName));
			}

			CheckUpdating();

			ZipEntry dirEntry = EntryFactory.MakeDirectoryEntry(directoryName);
			AddUpdate(new ZipUpdate(UpdateCommand.Add, dirEntry));
		}

		private static void CheckSupportedCompressionMethod(CompressionMethod compressionMethod)
		{
			if (compressionMethod != CompressionMethod.Deflated && compressionMethod != CompressionMethod.Stored && compressionMethod != CompressionMethod.BZip2)
			{
				throw new NotImplementedException("Compression method not supported");
			}
		}

		#endregion Adding Entries

		#region Modifying Entries

		/* Modify not yet ready for public consumption.
		   Direct modification of an entry should not overwrite original data before its read.
		   Safe mode is trivial in this sense.
				public void Modify(ZipEntry original, ZipEntry updated)
				{
					if ( original == null ) {
						throw new ArgumentNullException("original");
					}
					if ( updated == null ) {
						throw new ArgumentNullException("updated");
					}
					CheckUpdating();
					contentsEdited_ = true;
					updates_.Add(new ZipUpdate(original, updated));
				}
		*/

		#endregion Modifying Entries

		#region Deleting Entries

		public bool Delete(string fileName)
		{
			if (fileName == null)
			{
				throw new ArgumentNullException(nameof(fileName));
			}

			CheckUpdating();

			bool result = false;
			int index = FindExistingUpdate(fileName);
			if ((index >= 0) && (updates_[index] != null))
			{
				result = true;
				contentsEdited_ = true;
				updates_[index] = null;
				updateCount_ -= 1;
			}
			else
			{
				throw new ZipException("Cannot find entry to delete");
			}
			return result;
		}

		public void Delete(ZipEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			CheckUpdating();

			int index = FindExistingUpdate(entry);
			if (index >= 0)
			{
				contentsEdited_ = true;
				updates_[index] = null;
				updateCount_ -= 1;
			}
			else
			{
				throw new ZipException("Cannot find entry to delete");
			}
		}

		#endregion Deleting Entries

		#region Update Support

		#region Writing Values/Headers

		private void WriteLEShort(int value)
		{
			baseStream_.WriteByte((byte)(value & 0xff));
			baseStream_.WriteByte((byte)((value >> 8) & 0xff));
		}

		private void WriteLEUshort(ushort value)
		{
			baseStream_.WriteByte((byte)(value & 0xff));
			baseStream_.WriteByte((byte)(value >> 8));
		}

		private void WriteLEInt(int value)
		{
			WriteLEShort(value & 0xffff);
			WriteLEShort(value >> 16);
		}

		private void WriteLEUint(uint value)
		{
			WriteLEUshort((ushort)(value & 0xffff));
			WriteLEUshort((ushort)(value >> 16));
		}

		private void WriteLeLong(long value)
		{
			WriteLEInt((int)(value & 0xffffffff));
			WriteLEInt((int)(value >> 32));
		}

		private void WriteLEUlong(ulong value)
		{
			WriteLEUint((uint)(value & 0xffffffff));
			WriteLEUint((uint)(value >> 32));
		}

		private void WriteLocalEntryHeader(ZipUpdate update)
		{
			ZipEntry entry = update.OutEntry;

			entry.Offset = baseStream_.Position;

			if (update.Command != UpdateCommand.Copy)
			{
				if (entry.CompressionMethod == CompressionMethod.Deflated)
				{
					if (entry.Size == 0)
					{
						entry.CompressedSize = entry.Size;
						entry.Crc = 0;
						entry.CompressionMethod = CompressionMethod.Stored;
					}
				}
				else if (entry.CompressionMethod == CompressionMethod.Stored)
				{
					entry.Flags &= ~(int)GeneralBitFlags.Descriptor;
				}

				if (HaveKeys)
				{
					entry.IsCrypted = true;
					if (entry.Crc < 0)
					{
						entry.Flags |= (int)GeneralBitFlags.Descriptor;
					}
				}
				else
				{
					entry.IsCrypted = false;
				}

				switch (useZip64_)
				{
					case UseZip64.Dynamic:
						if (entry.Size < 0)
						{
							entry.ForceZip64();
						}
						break;

					case UseZip64.On:
						entry.ForceZip64();
						break;

					case UseZip64.Off:
						break;
				}
			}

			WriteLEInt(ZipConstants.LocalHeaderSignature);

			WriteLEShort(entry.Version);
			WriteLEShort(entry.Flags);

			WriteLEShort((byte)entry.CompressionMethodForHeader);
			WriteLEInt((int)entry.DosTime);

			if (!entry.HasCrc)
			{
				update.CrcPatchOffset = baseStream_.Position;
				WriteLEInt((int)0);
			}
			else
			{
				WriteLEInt(unchecked((int)entry.Crc));
			}

			if (entry.LocalHeaderRequiresZip64)
			{
				WriteLEInt(-1);
				WriteLEInt(-1);
			}
			else
			{
				if ((entry.CompressedSize < 0) || (entry.Size < 0))
				{
					update.SizePatchOffset = baseStream_.Position;
				}

				WriteLEInt((int)entry.CompressedSize);
				WriteLEInt((int)entry.Size);
			}

			byte[] name = ZipStrings.ConvertToArray(entry.Flags, entry.Name);

			if (name.Length > 0xFFFF)
			{
				throw new ZipException("Entry name too long.");
			}

			var ed = new ZipExtraData(entry.ExtraData);

			if (entry.LocalHeaderRequiresZip64)
			{
				ed.StartNewEntry();

				ed.AddLeLong(entry.Size);
				ed.AddLeLong(entry.CompressedSize);
				ed.AddNewEntry(1);
			}
			else
			{
				ed.Delete(1);
			}

			entry.ExtraData = ed.GetEntryData();

			WriteLEShort(name.Length);
			WriteLEShort(entry.ExtraData.Length);

			if (name.Length > 0)
			{
				baseStream_.Write(name, 0, name.Length);
			}

			if (entry.LocalHeaderRequiresZip64)
			{
				if (!ed.Find(1))
				{
					throw new ZipException("Internal error cannot find extra data");
				}

				update.SizePatchOffset = baseStream_.Position + ed.CurrentReadIndex;
			}

			if (entry.ExtraData.Length > 0)
			{
				baseStream_.Write(entry.ExtraData, 0, entry.ExtraData.Length);
			}
		}

		private int WriteCentralDirectoryHeader(ZipEntry entry)
		{
			if (entry.CompressedSize < 0)
			{
				throw new ZipException("Attempt to write central directory entry with unknown csize");
			}

			if (entry.Size < 0)
			{
				throw new ZipException("Attempt to write central directory entry with unknown size");
			}

			if (entry.Crc < 0)
			{
				throw new ZipException("Attempt to write central directory entry with unknown crc");
			}

			WriteLEInt(ZipConstants.CentralHeaderSignature);

			WriteLEShort((entry.HostSystem << 8) | entry.VersionMadeBy);

			WriteLEShort(entry.Version);

			WriteLEShort(entry.Flags);

			unchecked
			{
				WriteLEShort((byte)entry.CompressionMethodForHeader);
				WriteLEInt((int)entry.DosTime);
				WriteLEInt((int)entry.Crc);
			}

			bool useExtraCompressedSize = false; //Do we want to store the compressed size in the extra data?
			if ((entry.IsZip64Forced()) || (entry.CompressedSize >= 0xffffffff))
			{
				useExtraCompressedSize = true;
				WriteLEInt(-1);
			}
			else
			{
				WriteLEInt((int)(entry.CompressedSize & 0xffffffff));
			}

			bool useExtraUncompressedSize = false; //Do we want to store the uncompressed size in the extra data?
			if ((entry.IsZip64Forced()) || (entry.Size >= 0xffffffff))
			{
				useExtraUncompressedSize = true;
				WriteLEInt(-1);
			}
			else
			{
				WriteLEInt((int)entry.Size);
			}

			byte[] name = ZipStrings.ConvertToArray(entry.Flags, entry.Name);

			if (name.Length > 0xFFFF)
			{
				throw new ZipException("Entry name is too long.");
			}

			WriteLEShort(name.Length);

			var ed = new ZipExtraData(entry.ExtraData);

			if (entry.CentralHeaderRequiresZip64)
			{
				ed.StartNewEntry();

				if (useExtraUncompressedSize)
				{
					ed.AddLeLong(entry.Size);
				}

				if (useExtraCompressedSize)
				{
					ed.AddLeLong(entry.CompressedSize);
				}

				if (entry.Offset >= 0xffffffff)
				{
					ed.AddLeLong(entry.Offset);
				}

				ed.AddNewEntry(1);
			}
			else
			{
				ed.Delete(1);
			}

			byte[] centralExtraData = ed.GetEntryData();

			WriteLEShort(centralExtraData.Length);
			WriteLEShort(entry.Comment != null ? entry.Comment.Length : 0);

			WriteLEShort(0);    // disk number
			WriteLEShort(0);    // internal file attributes

			if (entry.ExternalFileAttributes != -1)
			{
				WriteLEInt(entry.ExternalFileAttributes);
			}
			else
			{
				if (entry.IsDirectory)
				{
					WriteLEUint(16);
				}
				else
				{
					WriteLEUint(0);
				}
			}

			if (entry.Offset >= 0xffffffff)
			{
				WriteLEUint(0xffffffff);
			}
			else
			{
				WriteLEUint((uint)(int)entry.Offset);
			}

			if (name.Length > 0)
			{
				baseStream_.Write(name, 0, name.Length);
			}

			if (centralExtraData.Length > 0)
			{
				baseStream_.Write(centralExtraData, 0, centralExtraData.Length);
			}

			byte[] rawComment = (entry.Comment != null) ? Encoding.ASCII.GetBytes(entry.Comment) : Empty.Array<byte>();

			if (rawComment.Length > 0)
			{
				baseStream_.Write(rawComment, 0, rawComment.Length);
			}

			return ZipConstants.CentralHeaderBaseSize + name.Length + centralExtraData.Length + rawComment.Length;
		}

		#endregion Writing Values/Headers

		private void PostUpdateCleanup()
		{
			updateDataSource_ = null;
			updates_ = null;
			updateIndex_ = null;

			if (archiveStorage_ != null)
			{
				archiveStorage_.Dispose();
				archiveStorage_ = null;
			}
		}

		private string GetTransformedFileName(string name)
		{
			INameTransform transform = NameTransform;
			return (transform != null) ?
				transform.TransformFile(name) :
				name;
		}

		private string GetTransformedDirectoryName(string name)
		{
			INameTransform transform = NameTransform;
			return (transform != null) ?
				transform.TransformDirectory(name) :
				name;
		}

		private byte[] GetBuffer()
		{
			if (copyBuffer_ == null)
			{
				copyBuffer_ = new byte[bufferSize_];
			}
			return copyBuffer_;
		}

		private void CopyDescriptorBytes(ZipUpdate update, Stream dest, Stream source)
		{
			var bytesToCopy = GetDescriptorSize(update, false);

			if (bytesToCopy == 0) return;

			var buffer = GetBuffer();

			source.Read(buffer, 0, sizeof(int));
			dest.Write(buffer, 0, sizeof(int));

			if (BitConverter.ToUInt32(buffer, 0) != ZipConstants.DataDescriptorSignature)
			{
				bytesToCopy -= buffer.Length;
			}

			while (bytesToCopy > 0)
			{
				int readSize = Math.Min(buffer.Length, bytesToCopy);

				int bytesRead = source.Read(buffer, 0, readSize);
				if (bytesRead > 0)
				{
					dest.Write(buffer, 0, bytesRead);
					bytesToCopy -= bytesRead;
				}
				else
				{
					throw new ZipException("Unxpected end of stream");
				}
			}
		}

		private void CopyBytes(ZipUpdate update, Stream destination, Stream source,
			long bytesToCopy, bool updateCrc)
		{
			if (destination == source)
			{
				throw new InvalidOperationException("Destination and source are the same");
			}

			var crc = new Crc32();
			byte[] buffer = GetBuffer();

			long targetBytes = bytesToCopy;
			long totalBytesRead = 0;

			int bytesRead;
			do
			{
				int readSize = buffer.Length;

				if (bytesToCopy < readSize)
				{
					readSize = (int)bytesToCopy;
				}

				bytesRead = source.Read(buffer, 0, readSize);
				if (bytesRead > 0)
				{
					if (updateCrc)
					{
						crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
					}
					destination.Write(buffer, 0, bytesRead);
					bytesToCopy -= bytesRead;
					totalBytesRead += bytesRead;
				}
			}
			while ((bytesRead > 0) && (bytesToCopy > 0));

			if (totalBytesRead != targetBytes)
			{
				throw new ZipException(string.Format("Failed to copy bytes expected {0} read {1}", targetBytes, totalBytesRead));
			}

			if (updateCrc)
			{
				update.OutEntry.Crc = crc.Value;
			}
		}

		private static int GetDescriptorSize(ZipUpdate update, bool includingSignature)
		{
			if (!((GeneralBitFlags)update.Entry.Flags).HasFlag(GeneralBitFlags.Descriptor)) 
				return 0;
			
			var descriptorWithSignature = update.Entry.LocalHeaderRequiresZip64 
				? ZipConstants.Zip64DataDescriptorSize 
				: ZipConstants.DataDescriptorSize;

			return includingSignature 
				? descriptorWithSignature 
				: descriptorWithSignature - sizeof(int);
		}

		private void CopyDescriptorBytesDirect(ZipUpdate update, Stream stream, ref long destinationPosition, long sourcePosition)
		{
			var buffer = GetBuffer(); ;

			stream.Position = sourcePosition;
			stream.Read(buffer, 0, sizeof(int));
			var sourceHasSignature = BitConverter.ToUInt32(buffer, 0) == ZipConstants.DataDescriptorSignature;

			var bytesToCopy = GetDescriptorSize(update, sourceHasSignature);

			while (bytesToCopy > 0)
			{
				stream.Position = sourcePosition;

				var bytesRead = stream.Read(buffer, 0, bytesToCopy);
				if (bytesRead > 0)
				{
					stream.Position = destinationPosition;
					stream.Write(buffer, 0, bytesRead);
					bytesToCopy -= bytesRead;
					destinationPosition += bytesRead;
					sourcePosition += bytesRead;
				}
				else
				{
					throw new ZipException("Unexpected end of stream");
				}
			}
		}

		private void CopyEntryDataDirect(ZipUpdate update, Stream stream, bool updateCrc, ref long destinationPosition, ref long sourcePosition)
		{
			long bytesToCopy = update.Entry.CompressedSize;

			var crc = new Crc32();
			byte[] buffer = GetBuffer();

			long targetBytes = bytesToCopy;
			long totalBytesRead = 0;

			int bytesRead;
			do
			{
				int readSize = buffer.Length;

				if (bytesToCopy < readSize)
				{
					readSize = (int)bytesToCopy;
				}

				stream.Position = sourcePosition;
				bytesRead = stream.Read(buffer, 0, readSize);
				if (bytesRead > 0)
				{
					if (updateCrc)
					{
						crc.Update(new ArraySegment<byte>(buffer, 0, bytesRead));
					}
					stream.Position = destinationPosition;
					stream.Write(buffer, 0, bytesRead);

					destinationPosition += bytesRead;
					sourcePosition += bytesRead;
					bytesToCopy -= bytesRead;
					totalBytesRead += bytesRead;
				}
			}
			while ((bytesRead > 0) && (bytesToCopy > 0));

			if (totalBytesRead != targetBytes)
			{
				throw new ZipException(string.Format("Failed to copy bytes expected {0} read {1}", targetBytes, totalBytesRead));
			}

			if (updateCrc)
			{
				update.OutEntry.Crc = crc.Value;
			}
		}

		private int FindExistingUpdate(ZipEntry entry)
		{
			int result = -1;
			if (updateIndex_.ContainsKey(entry.Name))
			{
				result = (int)updateIndex_[entry.Name];
			}
			/*
						for (int index = 0; index < updates_.Count; ++index)
						{
							ZipUpdate zu = ( ZipUpdate )updates_[index];
							if ( (zu.Entry.ZipFileIndex == entry.ZipFileIndex) &&
								(string.Compare(convertedName, zu.Entry.Name, true, CultureInfo.InvariantCulture) == 0) ) {
								result = index;
								break;
							}
						}
			 */
			return result;
		}

		private int FindExistingUpdate(string fileName, bool isEntryName = false)
		{
			int result = -1;

			string convertedName = !isEntryName ? GetTransformedFileName(fileName) : fileName;

			if (updateIndex_.ContainsKey(convertedName))
			{
				result = (int)updateIndex_[convertedName];
			}

			/*
						for ( int index = 0; index < updates_.Count; ++index ) {
							if ( string.Compare(convertedName, (( ZipUpdate )updates_[index]).Entry.Name,
								true, CultureInfo.InvariantCulture) == 0 ) {
								result = index;
								break;
							}
						}
			 */

			return result;
		}

		private Stream GetOutputStream(ZipEntry entry)
		{
			Stream result = baseStream_;

			if (entry.IsCrypted == true)
			{
				result = CreateAndInitEncryptionStream(result, entry);
			}

			switch (entry.CompressionMethod)
			{
				case CompressionMethod.Stored:
					if (!entry.IsCrypted)
					{
						result = new UncompressedStream(result);
					}
					break;

				case CompressionMethod.Deflated:
					var dos = new DeflaterOutputStream(result, new Deflater(9, true))
					{
						IsStreamOwner = entry.IsCrypted
					};
					result = dos;
					break;

				case CompressionMethod.BZip2:
					var bzos = new BZip2.BZip2OutputStream(result)
					{
						IsStreamOwner = entry.IsCrypted
					};
					result = bzos;
					break;

				default:
					throw new ZipException("Unknown compression method " + entry.CompressionMethod);
			}
			return result;
		}

		private void AddEntry(ZipFile workFile, ZipUpdate update)
		{
			Stream source = null;

			if (update.Entry.IsFile)
			{
				source = update.GetSource();

				if (source == null)
				{
					source = updateDataSource_.GetSource(update.Entry, update.Filename);
				}
			}

			var useCrc = update.Entry.AESKeySize == 0;

			if (source != null)
			{
				using (source)
				{
					long sourceStreamLength = source.Length;
					if (update.OutEntry.Size < 0)
					{
						update.OutEntry.Size = sourceStreamLength;
					}
					else
					{
						if (update.OutEntry.Size != sourceStreamLength)
						{
							throw new ZipException("Entry size/stream size mismatch");
						}
					}

					workFile.WriteLocalEntryHeader(update);

					long dataStart = workFile.baseStream_.Position;

					using (Stream output = workFile.GetOutputStream(update.OutEntry))
					{
						CopyBytes(update, output, source, sourceStreamLength, useCrc);
					}

					long dataEnd = workFile.baseStream_.Position;
					update.OutEntry.CompressedSize = dataEnd - dataStart;

					if ((update.OutEntry.Flags & (int)GeneralBitFlags.Descriptor) == (int)GeneralBitFlags.Descriptor)
					{
						var helper = new ZipHelperStream(workFile.baseStream_);
						helper.WriteDataDescriptor(update.OutEntry);
					}
				}
			}
			else
			{
				workFile.WriteLocalEntryHeader(update);
				update.OutEntry.CompressedSize = 0;
			}
		}

		private void ModifyEntry(ZipFile workFile, ZipUpdate update)
		{
			workFile.WriteLocalEntryHeader(update);
			long dataStart = workFile.baseStream_.Position;

			if (update.Entry.IsFile && (update.Filename != null))
			{
				using (Stream output = workFile.GetOutputStream(update.OutEntry))
				{
					using (Stream source = this.GetInputStream(update.Entry))
					{
						CopyBytes(update, output, source, source.Length, true);
					}
				}
			}

			long dataEnd = workFile.baseStream_.Position;
			update.Entry.CompressedSize = dataEnd - dataStart;
		}

		private void CopyEntryDirect(ZipFile workFile, ZipUpdate update, ref long destinationPosition)
		{
			bool skipOver = false || update.Entry.Offset == destinationPosition;

			if (!skipOver)
			{
				baseStream_.Position = destinationPosition;
				workFile.WriteLocalEntryHeader(update);
				destinationPosition = baseStream_.Position;
			}

			long sourcePosition = 0;

			const int NameLengthOffset = 26;

			long entryDataOffset = update.Entry.Offset + NameLengthOffset;

			baseStream_.Seek(entryDataOffset, SeekOrigin.Begin);

			
			uint nameLength = ReadLEUshort();
			uint extraLength = ReadLEUshort();

			sourcePosition = baseStream_.Position + nameLength + extraLength;

			if (skipOver)
			{
				if (update.OffsetBasedSize != -1)
				{
					destinationPosition += update.OffsetBasedSize;
				}
				else
				{
					destinationPosition += (sourcePosition - entryDataOffset) + NameLengthOffset;

					destinationPosition += update.Entry.CompressedSize;

					baseStream_.Seek(destinationPosition, SeekOrigin.Begin);

					var descriptorHasSignature = ReadLEUint() == ZipConstants.DataDescriptorSignature;

					destinationPosition += GetDescriptorSize(update, descriptorHasSignature);
				}
			}
			else
			{
				if (update.Entry.CompressedSize > 0)
				{
					CopyEntryDataDirect(update, baseStream_, false, ref destinationPosition, ref sourcePosition);
				}
				CopyDescriptorBytesDirect(update, baseStream_, ref destinationPosition, sourcePosition);
			}
		}

		private void CopyEntry(ZipFile workFile, ZipUpdate update)
		{
			workFile.WriteLocalEntryHeader(update);

			if (update.Entry.CompressedSize > 0)
			{
				const int NameLengthOffset = 26;

				long entryDataOffset = update.Entry.Offset + NameLengthOffset;

				baseStream_.Seek(entryDataOffset, SeekOrigin.Begin);

				uint nameLength = ReadLEUshort();
				uint extraLength = ReadLEUshort();

				baseStream_.Seek(nameLength + extraLength, SeekOrigin.Current);

				CopyBytes(update, workFile.baseStream_, baseStream_, update.Entry.CompressedSize, false);
			}
			CopyDescriptorBytes(update, workFile.baseStream_, baseStream_);
		}

		private void Reopen(Stream source)
		{
			isNewArchive_ = false;
			baseStream_ = source ?? throw new ZipException("Failed to reopen archive - no source");
			ReadEntries();
		}

		private void Reopen()
		{
			if (Name == null)
			{
				throw new InvalidOperationException("Name is not known cannot Reopen");
			}

			Reopen(File.Open(Name, FileMode.Open, FileAccess.Read, FileShare.Read));
		}

		private void UpdateCommentOnly()
		{
			long baseLength = baseStream_.Length;

			ZipHelperStream updateFile = null;

			if (archiveStorage_.UpdateMode == FileUpdateMode.Safe)
			{
				Stream copyStream = archiveStorage_.MakeTemporaryCopy(baseStream_);
				updateFile = new ZipHelperStream(copyStream)
				{
					IsStreamOwner = true
				};

				baseStream_.Dispose();
				baseStream_ = null;
			}
			else
			{
				if (archiveStorage_.UpdateMode == FileUpdateMode.Direct)
				{

					baseStream_ = archiveStorage_.OpenForDirectUpdate(baseStream_);
					updateFile = new ZipHelperStream(baseStream_);
				}
				else
				{
					baseStream_.Dispose();
					baseStream_ = null;
					updateFile = new ZipHelperStream(Name);
				}
			}

			using (updateFile)
			{
				long locatedCentralDirOffset =
					updateFile.LocateBlockWithSignature(ZipConstants.EndOfCentralDirectorySignature,
														baseLength, ZipConstants.EndOfCentralRecordBaseSize, 0xffff);
				if (locatedCentralDirOffset < 0)
				{
					throw new ZipException("Cannot find central directory");
				}

				const int CentralHeaderCommentSizeOffset = 16;
				updateFile.Position += CentralHeaderCommentSizeOffset;

				byte[] rawComment = newComment_.RawComment;

				updateFile.WriteLEShort(rawComment.Length);
				updateFile.Write(rawComment, 0, rawComment.Length);
				updateFile.SetLength(updateFile.Position);
			}

			if (archiveStorage_.UpdateMode == FileUpdateMode.Safe)
			{
				Reopen(archiveStorage_.ConvertTemporaryToFinal());
			}
			else
			{
				ReadEntries();
			}
		}

		private class UpdateComparer : IComparer<ZipUpdate>
		{
			public int Compare(ZipUpdate x, ZipUpdate y)
			{
				int result;

				if (x == null)
				{
					if (y == null)
					{
						result = 0;
					}
					else
					{
						result = -1;
					}
				}
				else if (y == null)
				{
					result = 1;
				}
				else
				{
					int xCmdValue = ((x.Command == UpdateCommand.Copy) || (x.Command == UpdateCommand.Modify)) ? 0 : 1;
					int yCmdValue = ((y.Command == UpdateCommand.Copy) || (y.Command == UpdateCommand.Modify)) ? 0 : 1;

					result = xCmdValue - yCmdValue;
					if (result == 0)
					{
						long offsetDiff = x.Entry.Offset - y.Entry.Offset;
						if (offsetDiff < 0)
						{
							result = -1;
						}
						else if (offsetDiff == 0)
						{
							result = 0;
						}
						else
						{
							result = 1;
						}
					}
				}
				return result;
			}
		}

		private void RunUpdates()
		{
			long sizeEntries = 0;
			long endOfStream = 0;
			bool directUpdate = false;
			long destinationPosition = 0; // NOT SFX friendly

			ZipFile workFile;

			if (IsNewArchive)
			{
				workFile = this;
				workFile.baseStream_.Position = 0;
				directUpdate = true;
			}
			else if (archiveStorage_.UpdateMode == FileUpdateMode.Direct)
			{
				workFile = this;
				workFile.baseStream_.Position = 0;
				directUpdate = true;

				updates_.Sort(new UpdateComparer());
			}
			else
			{
				workFile = ZipFile.Create(archiveStorage_.GetTemporaryOutput());
				workFile.UseZip64 = UseZip64;

				if (key != null)
				{
					workFile.key = (byte[])key.Clone();
				}
			}

			try
			{
				foreach (ZipUpdate update in updates_)
				{
					if (update != null)
					{
						switch (update.Command)
						{
							case UpdateCommand.Copy:
								if (directUpdate)
								{
									CopyEntryDirect(workFile, update, ref destinationPosition);
								}
								else
								{
									CopyEntry(workFile, update);
								}
								break;

							case UpdateCommand.Modify:
								ModifyEntry(workFile, update);
								break;

							case UpdateCommand.Add:
								if (!IsNewArchive && directUpdate)
								{
									workFile.baseStream_.Position = destinationPosition;
								}

								AddEntry(workFile, update);

								if (directUpdate)
								{
									destinationPosition = workFile.baseStream_.Position;
								}
								break;
						}
					}
				}

				if (!IsNewArchive && directUpdate)
				{
					workFile.baseStream_.Position = destinationPosition;
				}

				long centralDirOffset = workFile.baseStream_.Position;

				foreach (ZipUpdate update in updates_)
				{
					if (update != null)
					{
						sizeEntries += workFile.WriteCentralDirectoryHeader(update.OutEntry);
					}
				}

				byte[] theComment = (newComment_ != null) ? newComment_.RawComment : ZipStrings.ConvertToArray(comment_);
				using (ZipHelperStream zhs = new ZipHelperStream(workFile.baseStream_))
				{
					zhs.WriteEndOfCentralDirectory(updateCount_, sizeEntries, centralDirOffset, theComment);
				}

				endOfStream = workFile.baseStream_.Position;

				foreach (ZipUpdate update in updates_)
				{
					if (update != null)
					{
						if ((update.CrcPatchOffset > 0) && (update.OutEntry.CompressedSize > 0))
						{
							workFile.baseStream_.Position = update.CrcPatchOffset;
							workFile.WriteLEInt((int)update.OutEntry.Crc);
						}

						if (update.SizePatchOffset > 0)
						{
							workFile.baseStream_.Position = update.SizePatchOffset;
							if (update.OutEntry.LocalHeaderRequiresZip64)
							{
								workFile.WriteLeLong(update.OutEntry.Size);
								workFile.WriteLeLong(update.OutEntry.CompressedSize);
							}
							else
							{
								workFile.WriteLEInt((int)update.OutEntry.CompressedSize);
								workFile.WriteLEInt((int)update.OutEntry.Size);
							}
						}
					}
				}
			}
			catch
			{
				workFile.Close();
				if (!directUpdate && (workFile.Name != null))
				{
					File.Delete(workFile.Name);
				}
				throw;
			}

			if (directUpdate)
			{
				workFile.baseStream_.SetLength(endOfStream);
				workFile.baseStream_.Flush();
				isNewArchive_ = false;
				ReadEntries();
			}
			else
			{
				baseStream_.Dispose();
				Reopen(archiveStorage_.ConvertTemporaryToFinal());
			}
		}

		private void CheckUpdating()
		{
			if (updates_ == null)
			{
				throw new InvalidOperationException("BeginUpdate has not been called");
			}
		}

		#endregion Update Support

		#region ZipUpdate class

		private class ZipUpdate
		{
			#region Constructors

			public ZipUpdate(string fileName, ZipEntry entry)
			{
				command_ = UpdateCommand.Add;
				entry_ = entry;
				filename_ = fileName;
			}

			[Obsolete]
			public ZipUpdate(string fileName, string entryName, CompressionMethod compressionMethod)
			{
				command_ = UpdateCommand.Add;
				entry_ = new ZipEntry(entryName)
				{
					CompressionMethod = compressionMethod
				};
				filename_ = fileName;
			}

			[Obsolete]
			public ZipUpdate(string fileName, string entryName)
				: this(fileName, entryName, CompressionMethod.Deflated)
			{
			}

			[Obsolete]
			public ZipUpdate(IStaticDataSource dataSource, string entryName, CompressionMethod compressionMethod)
			{
				command_ = UpdateCommand.Add;
				entry_ = new ZipEntry(entryName)
				{
					CompressionMethod = compressionMethod
				};
				dataSource_ = dataSource;
			}

			public ZipUpdate(IStaticDataSource dataSource, ZipEntry entry)
			{
				command_ = UpdateCommand.Add;
				entry_ = entry;
				dataSource_ = dataSource;
			}

			public ZipUpdate(ZipEntry original, ZipEntry updated)
			{
				throw new ZipException("Modify not currently supported");
				/*
					command_ = UpdateCommand.Modify;
					entry_ = ( ZipEntry )original.Clone();
					outEntry_ = ( ZipEntry )updated.Clone();
				*/
			}

			public ZipUpdate(UpdateCommand command, ZipEntry entry)
			{
				command_ = command;
				entry_ = (ZipEntry)entry.Clone();
			}

			public ZipUpdate(ZipEntry entry)
				: this(UpdateCommand.Copy, entry)
			{
			}

			#endregion Constructors

			public ZipEntry Entry
			{
				get { return entry_; }
			}

			public ZipEntry OutEntry
			{
				get
				{
					if (outEntry_ == null)
					{
						outEntry_ = (ZipEntry)entry_.Clone();
					}

					return outEntry_;
				}
			}

			public UpdateCommand Command
			{
				get { return command_; }
			}

			public string Filename
			{
				get { return filename_; }
			}

			public long SizePatchOffset
			{
				get { return sizePatchOffset_; }
				set { sizePatchOffset_ = value; }
			}

			public long CrcPatchOffset
			{
				get { return crcPatchOffset_; }
				set { crcPatchOffset_ = value; }
			}

			public long OffsetBasedSize
			{
				get { return _offsetBasedSize; }
				set { _offsetBasedSize = value; }
			}

			public Stream GetSource()
			{
				Stream result = null;
				if (dataSource_ != null)
				{
					result = dataSource_.GetSource();
				}

				return result;
			}

			#region Instance Fields

			private ZipEntry entry_;
			private ZipEntry outEntry_;
			private readonly UpdateCommand command_;
			private IStaticDataSource dataSource_;
			private readonly string filename_;
			private long sizePatchOffset_ = -1;
			private long crcPatchOffset_ = -1;
			private long _offsetBasedSize = -1;

			#endregion Instance Fields
		}

		#endregion ZipUpdate class

		#endregion Updating

		#region Disposing

		#region IDisposable Members

		void IDisposable.Dispose()
		{
			Close();
		}

		#endregion IDisposable Members

		private void DisposeInternal(bool disposing)
		{
			if (!isDisposed_)
			{
				isDisposed_ = true;
				entries_ = Empty.Array<ZipEntry>();

				if (IsStreamOwner && (baseStream_ != null))
				{
					lock (baseStream_)
					{
						baseStream_.Dispose();
					}
				}

				PostUpdateCleanup();
			}
		}

		protected virtual void Dispose(bool disposing)
		{
			DisposeInternal(disposing);
		}

		#endregion Disposing

		#region Internal routines

		#region Reading

		private ushort ReadLEUshort()
		{
			int data1 = baseStream_.ReadByte();

			if (data1 < 0)
			{
				throw new EndOfStreamException("End of stream");
			}

			int data2 = baseStream_.ReadByte();

			if (data2 < 0)
			{
				throw new EndOfStreamException("End of stream");
			}

			return unchecked((ushort)((ushort)data1 | (ushort)(data2 << 8)));
		}

		private uint ReadLEUint()
		{
			return (uint)(ReadLEUshort() | (ReadLEUshort() << 16));
		}

		private ulong ReadLEUlong()
		{
			return ReadLEUint() | ((ulong)ReadLEUint() << 32);
		}

		#endregion Reading

		private long LocateBlockWithSignature(int signature, long endLocation, int minimumBlockSize, int maximumVariableData)
		{
			using (ZipHelperStream les = new ZipHelperStream(baseStream_))
			{
				return les.LocateBlockWithSignature(signature, endLocation, minimumBlockSize, maximumVariableData);
			}
		}

		private void ReadEntries()
		{

			if (baseStream_.CanSeek == false)
			{
				throw new ZipException("ZipFile stream must be seekable");
			}

			long locatedEndOfCentralDir = LocateBlockWithSignature(ZipConstants.EndOfCentralDirectorySignature,
				baseStream_.Length, ZipConstants.EndOfCentralRecordBaseSize, 0xffff);

			if (locatedEndOfCentralDir < 0)
			{
				throw new ZipException("Cannot find central directory");
			}

			ushort thisDiskNumber = ReadLEUshort();
			ushort startCentralDirDisk = ReadLEUshort();
			ulong entriesForThisDisk = ReadLEUshort();
			ulong entriesForWholeCentralDir = ReadLEUshort();
			ulong centralDirSize = ReadLEUint();
			long offsetOfCentralDir = ReadLEUint();
			uint commentSize = ReadLEUshort();

			if (commentSize > 0)
			{
				byte[] comment = new byte[commentSize];

				StreamUtils.ReadFully(baseStream_, comment);
				comment_ = ZipStrings.ConvertToString(comment);
			}
			else
			{
				comment_ = string.Empty;
			}

			bool isZip64 = false;
			bool requireZip64 = false;

			if ((thisDiskNumber == 0xffff) ||
				(startCentralDirDisk == 0xffff) ||
				(entriesForThisDisk == 0xffff) ||
				(entriesForWholeCentralDir == 0xffff) ||
				(centralDirSize == 0xffffffff) ||
				(offsetOfCentralDir == 0xffffffff))
			{
				requireZip64 = true;
			}

			long locatedZip64EndOfCentralDirLocator = LocateBlockWithSignature(
				ZipConstants.Zip64CentralDirLocatorSignature,
				locatedEndOfCentralDir - 4,
				ZipConstants.Zip64EndOfCentralDirectoryLocatorSize,
				0);

			if (locatedZip64EndOfCentralDirLocator < 0)
			{
				if (requireZip64)
				{
					throw new ZipException("Cannot find Zip64 locator");
				}
			}
			else
			{
				isZip64 = true;

				ReadLEUint(); // startDisk64 is not currently used
				ulong offset64 = ReadLEUlong();
				uint totalDisks = ReadLEUint();

				baseStream_.Position = (long)offset64;
				long sig64 = ReadLEUint();

				if (sig64 != ZipConstants.Zip64CentralFileHeaderSignature)
				{
					throw new ZipException(string.Format("Invalid Zip64 Central directory signature at {0:X}", offset64));
				}

				ulong recordSize = ReadLEUlong();
				int versionMadeBy = ReadLEUshort();
				int versionToExtract = ReadLEUshort();
				uint thisDisk = ReadLEUint();
				uint centralDirDisk = ReadLEUint();
				entriesForThisDisk = ReadLEUlong();
				entriesForWholeCentralDir = ReadLEUlong();
				centralDirSize = ReadLEUlong();
				offsetOfCentralDir = (long)ReadLEUlong();

			}

			entries_ = new ZipEntry[entriesForThisDisk];

			if (!isZip64 && (offsetOfCentralDir < locatedEndOfCentralDir - (4 + (long)centralDirSize)))
			{
				offsetOfFirstEntry = locatedEndOfCentralDir - (4 + (long)centralDirSize + offsetOfCentralDir);
				if (offsetOfFirstEntry <= 0)
				{
					throw new ZipException("Invalid embedded zip archive");
				}
			}

			baseStream_.Seek(offsetOfFirstEntry + offsetOfCentralDir, SeekOrigin.Begin);

			for (ulong i = 0; i < entriesForThisDisk; i++)
			{
				if (ReadLEUint() != ZipConstants.CentralHeaderSignature)
				{
					throw new ZipException("Wrong Central Directory signature");
				}

				int versionMadeBy = ReadLEUshort();
				int versionToExtract = ReadLEUshort();
				int bitFlags = ReadLEUshort();
				int method = ReadLEUshort();
				uint dostime = ReadLEUint();
				uint crc = ReadLEUint();
				var csize = (long)ReadLEUint();
				var size = (long)ReadLEUint();
				int nameLen = ReadLEUshort();
				int extraLen = ReadLEUshort();
				int commentLen = ReadLEUshort();

				int diskStartNo = ReadLEUshort();  // Not currently used
				int internalAttributes = ReadLEUshort();  // Not currently used

				uint externalAttributes = ReadLEUint();
				long offset = ReadLEUint();

				byte[] buffer = new byte[Math.Max(nameLen, commentLen)];

				StreamUtils.ReadFully(baseStream_, buffer, 0, nameLen);
				string name = ZipStrings.ConvertToStringExt(bitFlags, buffer, nameLen);

				var entry = new ZipEntry(name, versionToExtract, versionMadeBy, (CompressionMethod)method)
				{
					Crc = crc & 0xffffffffL,
					Size = size & 0xffffffffL,
					CompressedSize = csize & 0xffffffffL,
					Flags = bitFlags,
					DosTime = dostime,
					ZipFileIndex = (long)i,
					Offset = offset,
					ExternalFileAttributes = (int)externalAttributes
				};

				if ((bitFlags & 8) == 0)
				{
					entry.CryptoCheckValue = (byte)(crc >> 24);
				}
				else
				{
					entry.CryptoCheckValue = (byte)((dostime >> 8) & 0xff);
				}

				if (extraLen > 0)
				{
					byte[] extra = new byte[extraLen];
					StreamUtils.ReadFully(baseStream_, extra);
					entry.ExtraData = extra;
				}

				entry.ProcessExtraData(false);

				if (commentLen > 0)
				{
					StreamUtils.ReadFully(baseStream_, buffer, 0, commentLen);
					entry.Comment = ZipStrings.ConvertToStringExt(bitFlags, buffer, commentLen);
				}

				entries_[i] = entry;
			}
		}

		private long LocateEntry(ZipEntry entry)
		{
			return TestLocalHeader(entry, HeaderTest.Extract);
		}

		private Stream CreateAndInitDecryptionStream(Stream baseStream, ZipEntry entry)
		{
			CryptoStream result = null;

			if (entry.CompressionMethodForHeader == CompressionMethod.WinZipAES)
			{
				if (entry.Version >= ZipConstants.VERSION_AES)
				{
					OnKeysRequired(entry.Name);
					if (rawPassword_ == null)
					{
						throw new ZipException("No password available for AES encrypted stream");
					}
					int saltLen = entry.AESSaltLen;
					byte[] saltBytes = new byte[saltLen];
					int saltIn = StreamUtils.ReadRequestedBytes(baseStream, saltBytes, 0, saltLen);
					if (saltIn != saltLen)
						throw new ZipException("AES Salt expected " + saltLen + " got " + saltIn);
					byte[] pwdVerifyRead = new byte[2];
					StreamUtils.ReadFully(baseStream, pwdVerifyRead);
					int blockSize = entry.AESKeySize / 8;   // bits to bytes

					var decryptor = new ZipAESTransform(rawPassword_, saltBytes, blockSize, false);
					byte[] pwdVerifyCalc = decryptor.PwdVerifier;
					if (pwdVerifyCalc[0] != pwdVerifyRead[0] || pwdVerifyCalc[1] != pwdVerifyRead[1])
						throw new ZipException("Invalid password for AES");
					result = new ZipAESStream(baseStream, decryptor, CryptoStreamMode.Read);
				}
				else
				{
					throw new ZipException("Decryption method not supported");
				}
			}
			else
			{
				if ((entry.Version < ZipConstants.VersionStrongEncryption)
					|| (entry.Flags & (int)GeneralBitFlags.StrongEncryption) == 0)
				{
					var classicManaged = new PkzipClassicManaged();

					OnKeysRequired(entry.Name);
					if (HaveKeys == false)
					{
						throw new ZipException("No password available for encrypted stream");
					}

					result = new CryptoStream(baseStream, classicManaged.CreateDecryptor(key, null), CryptoStreamMode.Read);
					CheckClassicPassword(result, entry);
				}
				else
				{
					throw new ZipException("Decryption method not supported");
				}
			}

			return result;
		}

		private Stream CreateAndInitEncryptionStream(Stream baseStream, ZipEntry entry)
		{
			CryptoStream result = null;
			if ((entry.Version < ZipConstants.VersionStrongEncryption)
				|| (entry.Flags & (int)GeneralBitFlags.StrongEncryption) == 0)
			{
				var classicManaged = new PkzipClassicManaged();

				OnKeysRequired(entry.Name);
				if (HaveKeys == false)
				{
					throw new ZipException("No password available for encrypted stream");
				}

				result = new CryptoStream(new UncompressedStream(baseStream),
					classicManaged.CreateEncryptor(key, null), CryptoStreamMode.Write);

				if ((entry.Crc < 0) || (entry.Flags & 8) != 0)
				{
					WriteEncryptionHeader(result, entry.DosTime << 16);
				}
				else
				{
					WriteEncryptionHeader(result, entry.Crc);
				}
			}
			return result;
		}

		private static void CheckClassicPassword(CryptoStream classicCryptoStream, ZipEntry entry)
		{
			byte[] cryptbuffer = new byte[ZipConstants.CryptoHeaderSize];
			StreamUtils.ReadFully(classicCryptoStream, cryptbuffer);
			if (cryptbuffer[ZipConstants.CryptoHeaderSize - 1] != entry.CryptoCheckValue)
				throw new ZipException("Invalid password");
		}

		private static void WriteEncryptionHeader(Stream stream, long crcValue)
		{
			byte[] cryptBuffer = new byte[ZipConstants.CryptoHeaderSize];
			var rng = new RNGCryptoServiceProvider();
			rng.GetBytes(cryptBuffer);
			cryptBuffer[11] = (byte)(crcValue >> 24);
			stream.Write(cryptBuffer, 0, cryptBuffer.Length);
		}

		#endregion Internal routines

		#region Instance Fields

		private bool isDisposed_;
		private string name_;
		private string comment_;
		private string rawPassword_;
		private Stream baseStream_;
		private bool isStreamOwner;
		private long offsetOfFirstEntry;
		private ZipEntry[] entries_;
		private byte[] key;
		private bool isNewArchive_;

		private UseZip64 useZip64_ = UseZip64.Dynamic;

		#region Zip Update Instance Fields

		private List<ZipUpdate> updates_;
		private long updateCount_; // Count is managed manually as updates_ can contain nulls!
		private Dictionary<string, int> updateIndex_;
		private IArchiveStorage archiveStorage_;
		private IDynamicDataSource updateDataSource_;
		private bool contentsEdited_;
		private int bufferSize_ = DefaultBufferSize;
		private byte[] copyBuffer_;
		private ZipString newComment_;
		private bool commentEdited_;
		private IEntryFactory updateEntryFactory_ = new ZipEntryFactory();

		#endregion Zip Update Instance Fields

		#endregion Instance Fields

		#region Support Classes

		private class ZipString
		{
			#region Constructors

			public ZipString(string comment)
			{
				comment_ = comment;
				isSourceString_ = true;
			}

			public ZipString(byte[] rawString)
			{
				rawComment_ = rawString;
			}

			#endregion Constructors

			public bool IsSourceString
			{
				get { return isSourceString_; }
			}

			public int RawLength
			{
				get
				{
					MakeBytesAvailable();
					return rawComment_.Length;
				}
			}

			public byte[] RawComment
			{
				get
				{
					MakeBytesAvailable();
					return (byte[])rawComment_.Clone();
				}
			}

			public void Reset()
			{
				if (isSourceString_)
				{
					rawComment_ = null;
				}
				else
				{
					comment_ = null;
				}
			}

			private void MakeTextAvailable()
			{
				if (comment_ == null)
				{
					comment_ = ZipStrings.ConvertToString(rawComment_);
				}
			}

			private void MakeBytesAvailable()
			{
				if (rawComment_ == null)
				{
					rawComment_ = ZipStrings.ConvertToArray(comment_);
				}
			}

			static public implicit operator string(ZipString zipString)
			{
				zipString.MakeTextAvailable();
				return zipString.comment_;
			}

			#region Instance Fields

			private string comment_;
			private byte[] rawComment_;
			private readonly bool isSourceString_;

			#endregion Instance Fields
		}

		private class ZipEntryEnumerator : IEnumerator
		{
			#region Constructors

			public ZipEntryEnumerator(ZipEntry[] entries)
			{
				array = entries;
			}

			#endregion Constructors

			#region IEnumerator Members

			public object Current
			{
				get
				{
					return array[index];
				}
			}

			public void Reset()
			{
				index = -1;
			}

			public bool MoveNext()
			{
				return (++index < array.Length);
			}

			#endregion IEnumerator Members

			#region Instance Fields

			private ZipEntry[] array;
			private int index = -1;

			#endregion Instance Fields
		}

		private class UncompressedStream : Stream
		{
			#region Constructors

			public UncompressedStream(Stream baseStream)
			{
				baseStream_ = baseStream;
			}

			#endregion Constructors

			public override bool CanRead
			{
				get
				{
					return false;
				}
			}

			public override void Flush()
			{
				baseStream_.Flush();
			}

			public override bool CanWrite
			{
				get
				{
					return baseStream_.CanWrite;
				}
			}

			public override bool CanSeek
			{
				get
				{
					return false;
				}
			}

			public override long Length
			{
				get
				{
					return 0;
				}
			}

			public override long Position
			{
				get
				{
					return baseStream_.Position;
				}
				set
				{
					throw new NotImplementedException();
				}
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				return 0;
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				return 0;
			}

			public override void SetLength(long value)
			{
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				baseStream_.Write(buffer, offset, count);
			}

			private readonly

			#region Instance Fields

			Stream baseStream_;

			#endregion Instance Fields
		}

		private class PartialInputStream : Stream
		{
			#region Constructors

			public PartialInputStream(ZipFile zipFile, long start, long length)
			{
				start_ = start;
				length_ = length;


				zipFile_ = zipFile;
				baseStream_ = zipFile_.baseStream_;
				readPos_ = start;
				end_ = start + length;
			}

			#endregion Constructors

			public override int ReadByte()
			{
				if (readPos_ >= end_)
				{
					return -1;
				}

				lock (baseStream_)
				{
					baseStream_.Seek(readPos_++, SeekOrigin.Begin);
					return baseStream_.ReadByte();
				}
			}

			public override int Read(byte[] buffer, int offset, int count)
			{
				lock (baseStream_)
				{
					if (count > end_ - readPos_)
					{
						count = (int)(end_ - readPos_);
						if (count == 0)
						{
							return 0;
						}
					}
					if (baseStream_.Position != readPos_)
					{
						baseStream_.Seek(readPos_, SeekOrigin.Begin);
					}
					int readCount = baseStream_.Read(buffer, offset, count);
					if (readCount > 0)
					{
						readPos_ += readCount;
					}
					return readCount;
				}
			}

			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException();
			}

			public override void SetLength(long value)
			{
				throw new NotSupportedException();
			}

			public override long Seek(long offset, SeekOrigin origin)
			{
				long newPos = readPos_;

				switch (origin)
				{
					case SeekOrigin.Begin:
						newPos = start_ + offset;
						break;

					case SeekOrigin.Current:
						newPos = readPos_ + offset;
						break;

					case SeekOrigin.End:
						newPos = end_ + offset;
						break;
				}

				if (newPos < start_)
				{
					throw new ArgumentException("Negative position is invalid");
				}

				if (newPos > end_)
				{
					throw new IOException("Cannot seek past end");
				}
				readPos_ = newPos;
				return readPos_;
			}

			public override void Flush()
			{
			}

			public override long Position
			{
				get { return readPos_ - start_; }
				set
				{
					long newPos = start_ + value;

					if (newPos < start_)
					{
						throw new ArgumentException("Negative position is invalid");
					}

					if (newPos > end_)
					{
						throw new InvalidOperationException("Cannot seek past end");
					}
					readPos_ = newPos;
				}
			}

			public override long Length
			{
				get { return length_; }
			}

			public override bool CanWrite
			{
				get { return false; }
			}

			public override bool CanSeek
			{
				get { return true; }
			}

			public override bool CanRead
			{
				get { return true; }
			}

			public override bool CanTimeout
			{
				get { return baseStream_.CanTimeout; }
			}

			#region Instance Fields

			private ZipFile zipFile_;
			private Stream baseStream_;
			private readonly long start_;
			private readonly long length_;
			private long readPos_;
			private readonly long end_;

			#endregion Instance Fields
		}

		#endregion Support Classes
	}

	#endregion ZipFile Class

	#region DataSources

	public interface IStaticDataSource
	{
		Stream GetSource();
	}

	public interface IDynamicDataSource
	{
		Stream GetSource(ZipEntry entry, string name);
	}

	public class StaticDiskDataSource : IStaticDataSource
	{
		public StaticDiskDataSource(string fileName)
		{
			fileName_ = fileName;
		}

		#region IDataSource Members

		public Stream GetSource()
		{
			return File.Open(fileName_, FileMode.Open, FileAccess.Read, FileShare.Read);
		}

		private readonly

		#endregion IDataSource Members

		#region Instance Fields

		string fileName_;

		#endregion Instance Fields
	}

	public class DynamicDiskDataSource : IDynamicDataSource
	{
		#region IDataSource Members

		public Stream GetSource(ZipEntry entry, string name)
		{
			Stream result = null;

			if (name != null)
			{
				result = File.Open(name, FileMode.Open, FileAccess.Read, FileShare.Read);
			}

			return result;
		}

		#endregion IDataSource Members
	}

	#endregion DataSources

	#region Archive Storage

	public interface IArchiveStorage
	{
		FileUpdateMode UpdateMode { get; }

		Stream GetTemporaryOutput();

		Stream ConvertTemporaryToFinal();

		Stream MakeTemporaryCopy(Stream stream);

		Stream OpenForDirectUpdate(Stream stream);

		void Dispose();
	}

	abstract public class BaseArchiveStorage : IArchiveStorage
	{
		#region Constructors

		protected BaseArchiveStorage(FileUpdateMode updateMode)
		{
			updateMode_ = updateMode;
		}

		#endregion Constructors

		#region IArchiveStorage Members

		public abstract Stream GetTemporaryOutput();

		public abstract Stream ConvertTemporaryToFinal();

		public abstract Stream MakeTemporaryCopy(Stream stream);

		public abstract Stream OpenForDirectUpdate(Stream stream);

		public abstract void Dispose();

		public FileUpdateMode UpdateMode
		{
			get
			{
				return updateMode_;
			}
		}

		#endregion IArchiveStorage Members

		#region Instance Fields

		private readonly FileUpdateMode updateMode_;

		#endregion Instance Fields
	}

	public class DiskArchiveStorage : BaseArchiveStorage
	{
		#region Constructors

		public DiskArchiveStorage(ZipFile file, FileUpdateMode updateMode)
			: base(updateMode)
		{
			if (file.Name == null)
			{
				throw new ZipException("Cant handle non file archives");
			}

			fileName_ = file.Name;
		}

		public DiskArchiveStorage(ZipFile file)
			: this(file, FileUpdateMode.Safe)
		{
		}

		#endregion Constructors

		#region IArchiveStorage Members

		public override Stream GetTemporaryOutput()
		{
			temporaryName_ = PathUtils.GetTempFileName(temporaryName_);
			temporaryStream_ = File.Open(temporaryName_, FileMode.OpenOrCreate, FileAccess.Write, FileShare.None);

			return temporaryStream_;
		}

		public override Stream ConvertTemporaryToFinal()
		{
			if (temporaryStream_ == null)
			{
				throw new ZipException("No temporary stream has been created");
			}

			Stream result = null;

			string moveTempName = PathUtils.GetTempFileName(fileName_);
			bool newFileCreated = false;

			try
			{
				temporaryStream_.Dispose();
				File.Move(fileName_, moveTempName);
				File.Move(temporaryName_, fileName_);
				newFileCreated = true;
				File.Delete(moveTempName);

				result = File.Open(fileName_, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
			catch (Exception)
			{
				result = null;

				if (!newFileCreated)
				{
					File.Move(moveTempName, fileName_);
					File.Delete(temporaryName_);
				}

				throw;
			}

			return result;
		}

		public override Stream MakeTemporaryCopy(Stream stream)
		{
			stream.Dispose();

			temporaryName_ = PathUtils.GetTempFileName(fileName_);
			File.Copy(fileName_, temporaryName_, true);

			temporaryStream_ = new FileStream(temporaryName_,
				FileMode.Open,
				FileAccess.ReadWrite);
			return temporaryStream_;
		}

		public override Stream OpenForDirectUpdate(Stream stream)
		{
			Stream result;
			if ((stream == null) || !stream.CanWrite)
			{
				if (stream != null)
				{
					stream.Dispose();
				}

				result = new FileStream(fileName_,
						FileMode.Open,
						FileAccess.ReadWrite);
			}
			else
			{
				result = stream;
			}

			return result;
		}

		public override void Dispose()
		{
			if (temporaryStream_ != null)
			{
				temporaryStream_.Dispose();
			}
		}

		#endregion IArchiveStorage Members

		#region Instance Fields

		private Stream temporaryStream_;
		private readonly string fileName_;
		private string temporaryName_;

		#endregion Instance Fields
	}

	public class MemoryArchiveStorage : BaseArchiveStorage
	{
		#region Constructors

		public MemoryArchiveStorage()
			: base(FileUpdateMode.Direct)
		{
		}

		public MemoryArchiveStorage(FileUpdateMode updateMode)
			: base(updateMode)
		{
		}

		#endregion Constructors

		#region Properties

		public MemoryStream FinalStream
		{
			get { return finalStream_; }
		}

		#endregion Properties

		#region IArchiveStorage Members

		public override Stream GetTemporaryOutput()
		{
			temporaryStream_ = new MemoryStream();
			return temporaryStream_;
		}

		public override Stream ConvertTemporaryToFinal()
		{
			if (temporaryStream_ == null)
			{
				throw new ZipException("No temporary stream has been created");
			}

			finalStream_ = new MemoryStream(temporaryStream_.ToArray());
			return finalStream_;
		}

		public override Stream MakeTemporaryCopy(Stream stream)
		{
			temporaryStream_ = new MemoryStream();
			stream.Position = 0;
			StreamUtils.Copy(stream, temporaryStream_, new byte[4096]);
			return temporaryStream_;
		}

		public override Stream OpenForDirectUpdate(Stream stream)
		{
			Stream result;
			if ((stream == null) || !stream.CanWrite)
			{
				result = new MemoryStream();

				if (stream != null)
				{
					stream.Position = 0;
					StreamUtils.Copy(stream, result, new byte[4096]);

					stream.Dispose();
				}
			}
			else
			{
				result = stream;
			}

			return result;
		}

		public override void Dispose()
		{
			if (temporaryStream_ != null)
			{
				temporaryStream_.Dispose();
			}
		}

		#endregion IArchiveStorage Members

		#region Instance Fields

		private MemoryStream temporaryStream_;
		private MemoryStream finalStream_;

		#endregion Instance Fields
	}

	#endregion Archive Storage
}
