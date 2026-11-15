using System;
using System.Collections;
using System.Collections.Generic;

namespace MelonLoader
{
	[Serializable]
    public class LemonArraySegment<T> : IList<T>, ICollection<T>, IEnumerable<T>, IEnumerable
    {
        public T[] Array { get; private set; }

        public int Offset { get; private set; }

        public int Count { get; private set; }

        public LemonArraySegment(T[] array)
        {
            if (array == null)
                throw new ArgumentNullException("array");
            Array = array;
            Offset = 0;
            Count = array.Length;
        }

        public LemonArraySegment(T[] array, int offset, int count)
        {
            if (array == null)
                throw new ArgumentNullException("array");

            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset", "Non-negative number required.");

            if (count < 0)
                throw new ArgumentOutOfRangeException("count", "Non-negative number required.");

            if (array.Length - offset < count)
                throw new ArgumentException("Offset and length were out of bounds for the array or count is greater than the number of elements from index to the end of the source collection.");

            Array = array;
            Offset = offset;
            Count = count;
        }

		public override int GetHashCode()
		{
			if (Array != null)
				return Array.GetHashCode() ^ Offset ^ Count;
			return 0;
		}

		public override bool Equals(object obj)
            => obj is LemonArraySegment<T> && Equals((LemonArraySegment<T>)obj);

        public bool Equals(LemonArraySegment<T> obj)
            => obj.Array == Array && obj.Offset == Offset && obj.Count == Count;

		public static bool operator ==(LemonArraySegment<T> a, LemonArraySegment<T> b)
			=> a.Equals(b);

		public static bool operator !=(LemonArraySegment<T> a, LemonArraySegment<T> b)
			=> !(a == b);

		T IList<T>.this[int index]
		{
			get
			{
				if (Array == null)
					throw new InvalidOperationException("The underlying array is null.");
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException("index");
				return Array[Offset + index];
			}
			set
			{
				if (Array == null)
					throw new InvalidOperationException("The underlying array is null.");
				if (index < 0 || index >= Count)
					throw new ArgumentOutOfRangeException("index");
				Array[Offset + index] = value;
			}
		}

		int IList<T>.IndexOf(T item)
		{
			if (Array == null)
				throw new InvalidOperationException("The underlying array is null.");
			int num = System.Array.IndexOf(Array, item, Offset, Count);
			if (num < 0)
				return -1;
			return num - Offset;
		}

		void IList<T>.Insert(int index, T item)
			=> throw new NotSupportedException();

		void IList<T>.RemoveAt(int index)
			=> throw new NotSupportedException();

		bool ICollection<T>.IsReadOnly { get => true; }

		void ICollection<T>.Add(T item)
			=> throw new NotSupportedException();

		void ICollection<T>.Clear()
			=> throw new NotSupportedException();

		bool ICollection<T>.Contains(T item)
		{
			if (Array == null)
				throw new InvalidOperationException("The underlying array is null.");
			return System.Array.IndexOf<T>(Array, item, Offset, Count) >= 0;
		}

		void ICollection<T>.CopyTo(T[] array, int arrayIndex)
		{
			if (Array == null)
				throw new InvalidOperationException("The underlying array is null.");
			System.Array.Copy(Array, Offset, array, arrayIndex, Count);
		}

		bool ICollection<T>.Remove(T item)
			=> throw new NotSupportedException();

		IEnumerator<T> IEnumerable<T>.GetEnumerator()
		{
			if (Array == null)
				throw new InvalidOperationException("The underlying array is null.");
			return new LemonArraySegmentEnumerator(this);
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			if (Array == null)
				throw new InvalidOperationException("The underlying array is null.");
			return new LemonArraySegmentEnumerator(this);
		}

		[Serializable]
		private sealed class LemonArraySegmentEnumerator : IEnumerator<T>, IDisposable, IEnumerator
		{
			private T[] _array;
			private int _start;
			private int _end;
			private int _current;

			internal LemonArraySegmentEnumerator(LemonArraySegment<T> arraySegment)
			{
				_array = arraySegment.Array;
				_start = arraySegment.Offset;
				_end = _start + arraySegment.Count;
				_current = _start - 1;
			}

			public bool MoveNext()
			{
				if (_current < _end)
				{
					_current++;
					return _current < _end;
				}
				return false;
			}

			public T Current
			{
				get
				{
					if (_current < _start)
						throw new InvalidOperationException("Enumeration has not started. Call MoveNext.");
					if (_current >= _end)
						throw new InvalidOperationException("Enumeration already finished.");
					return _array[_current];
				}
			}

			object IEnumerator.Current { get => Current; }

			void IEnumerator.Reset()
				=> _current = _start - 1;

			public void Dispose() { }
		}
	}
}
