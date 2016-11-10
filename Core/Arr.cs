using System;

namespace Greatbone.Core
{
    ///
    /// A JSON array model.
    ///
    public class Arr
    {
        const int InitialCapacity = 16;

        Member[] elements;

        int count;

        internal Arr(int capacity = InitialCapacity)
        {
            elements = new Member[capacity];
            count = 0;
        }

        public Member this[int index] => elements[index];

        public int Count => count;

        internal void Add(Member mbr)
        {
            int len = elements.Length;
            if (count >= len)
            {
                Member[] alloc = new Member[len * 4];
                Array.Copy(elements, 0, alloc, 0, len);
                elements = alloc;
            }
            elements[count++] = mbr;
        }

        internal void Dump<R>(ISink<R> snk) where R : ISink<R>
        {
            for (int i = 0; i < count; i++)
            {
                Member mbr = elements[i];
                MemberType typ = mbr.type;
                if (typ == MemberType.Array)
                {
                    snk.Put((Arr) mbr);
                }
                else if (typ == MemberType.Object)
                {
                    snk.Put((Obj) mbr);
                }
                else if (typ == MemberType.String)
                {
                    snk.Put((string) mbr);
                }
                else if (typ == MemberType.Number)
                {
                    snk.Put((Number) mbr);
                }
                else if (typ == MemberType.True)
                {
                    snk.Put(true);
                }
                else if (typ == MemberType.False)
                {
                    snk.Put(false);
                }
                else if (typ == MemberType.Null)
                {
                    snk.PutNull();
                }
            }
        }

        public D[] ToDatas<D>(byte z = 0) where D : IData, new()
        {
            D[] dats = new D[count];
            for (int i = 0; i < dats.Length; i++)
            {
                D dat = new D();
                dat.Load((Obj) this[i], z);
                dats[i] = dat;
            }
            return dats;
        }
    }
}