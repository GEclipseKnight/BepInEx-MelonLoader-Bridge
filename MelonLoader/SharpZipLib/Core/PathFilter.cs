using System;
using System.IO;

namespace MelonLoader.ICSharpCode.SharpZipLib.Core
{
	public class PathFilter : IScanFilter
	{
		#region Constructors

		public PathFilter(string filter)
		{
			nameFilter_ = new NameFilter(filter);
		}

		#endregion Constructors

		#region IScanFilter Members

		public virtual bool IsMatch(string name)
		{
			bool result = false;

			if (name != null)
			{
				string cooked = (name.Length > 0) ? Path.GetFullPath(name) : "";
				result = nameFilter_.IsMatch(cooked);
			}
			return result;
		}

		private readonly

		#endregion IScanFilter Members

		#region Instance Fields

		NameFilter nameFilter_;

		#endregion Instance Fields
	}

	public class ExtendedPathFilter : PathFilter
	{
		#region Constructors

		public ExtendedPathFilter(string filter,
			long minSize, long maxSize)
			: base(filter)
		{
			MinSize = minSize;
			MaxSize = maxSize;
		}

		public ExtendedPathFilter(string filter,
			DateTime minDate, DateTime maxDate)
			: base(filter)
		{
			MinDate = minDate;
			MaxDate = maxDate;
		}

		public ExtendedPathFilter(string filter,
			long minSize, long maxSize,
			DateTime minDate, DateTime maxDate)
			: base(filter)
		{
			MinSize = minSize;
			MaxSize = maxSize;
			MinDate = minDate;
			MaxDate = maxDate;
		}

		#endregion Constructors

		#region IScanFilter Members

		public override bool IsMatch(string name)
		{
			bool result = base.IsMatch(name);

			if (result)
			{
				var fileInfo = new FileInfo(name);
				result =
					(MinSize <= fileInfo.Length) &&
					(MaxSize >= fileInfo.Length) &&
					(MinDate <= fileInfo.LastWriteTime) &&
					(MaxDate >= fileInfo.LastWriteTime)
					;
			}
			return result;
		}

		#endregion IScanFilter Members

		#region Properties

		public long MinSize
		{
			get { return minSize_; }
			set
			{
				if ((value < 0) || (maxSize_ < value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				minSize_ = value;
			}
		}

		public long MaxSize
		{
			get { return maxSize_; }
			set
			{
				if ((value < 0) || (minSize_ > value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				maxSize_ = value;
			}
		}

		public DateTime MinDate
		{
			get
			{
				return minDate_;
			}

			set
			{
				if (value > maxDate_)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "Exceeds MaxDate");
				}

				minDate_ = value;
			}
		}

		public DateTime MaxDate
		{
			get
			{
				return maxDate_;
			}

			set
			{
				if (minDate_ > value)
				{
					throw new ArgumentOutOfRangeException(nameof(value), "Exceeds MinDate");
				}

				maxDate_ = value;
			}
		}

		#endregion Properties

		#region Instance Fields

		private long minSize_;
		private long maxSize_ = long.MaxValue;
		private DateTime minDate_ = DateTime.MinValue;
		private DateTime maxDate_ = DateTime.MaxValue;

		#endregion Instance Fields
	}

	[Obsolete("Use ExtendedPathFilter instead")]
	public class NameAndSizeFilter : PathFilter
	{
		public NameAndSizeFilter(string filter, long minSize, long maxSize)
			: base(filter)
		{
			MinSize = minSize;
			MaxSize = maxSize;
		}

		public override bool IsMatch(string name)
		{
			bool result = base.IsMatch(name);

			if (result)
			{
				var fileInfo = new FileInfo(name);
				long length = fileInfo.Length;
				result =
					(MinSize <= length) &&
					(MaxSize >= length);
			}
			return result;
		}

		public long MinSize
		{
			get { return minSize_; }
			set
			{
				if ((value < 0) || (maxSize_ < value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				minSize_ = value;
			}
		}

		public long MaxSize
		{
			get { return maxSize_; }
			set
			{
				if ((value < 0) || (minSize_ > value))
				{
					throw new ArgumentOutOfRangeException(nameof(value));
				}

				maxSize_ = value;
			}
		}

		#region Instance Fields

		private long minSize_;
		private long maxSize_ = long.MaxValue;

		#endregion Instance Fields
	}
}
