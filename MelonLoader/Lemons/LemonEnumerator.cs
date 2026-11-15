using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace MelonLoader
{
    public class LemonEnumerator<T> : IEnumerator<T>, IEnumerable<T>
    {
        private T[] LemonPatch;
        private int NextLemon = 0;

        public LemonEnumerator(T[] lemons)
            => LemonPatch = lemons.ToArray();

        public LemonEnumerator(IList<T> lemons)
            => LemonPatch = lemons.ToArray();

        object IEnumerator.Current => Current;
        public T Current { get; private set; }

        bool IEnumerator.MoveNext() => MoveNext();
        public bool MoveNext()
        {
            if ((LemonPatch == null)
                   || (LemonPatch.Length <= 0)
                   || (NextLemon >= LemonPatch.Length))
                return false;
            Current = LemonPatch[NextLemon];
            NextLemon++;
            return true;
        }

        void IEnumerator.Reset() => Reset();
        public void Reset()
        {
            NextLemon = 0;
            Current = default;
        }

        public IEnumerator<T> GetEnumerator() // for foreach loops
            => this;

        IEnumerator IEnumerable.GetEnumerator()
            => this;

        public void Dispose()
        {
            Reset();
            LemonPatch = null;
        }
    }
}
