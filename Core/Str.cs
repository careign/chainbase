namespace Greatbone.Core
{

    ///
    /// <summary>
    /// A reusable string builder that supports UTF-8 decoding.
    /// </summary>
    ///
    public class Str : Text
    {
        int sum; // combination of bytes

        int rest; // number of rest octets

        internal Str(int capacity) : base(capacity)
        {
            sum = 0;
            rest = 0;
        }

        // utf-8 decoding 
        public void Add(byte b)
        {
            if (rest == 0)
            {
                if (b < 0x80)
                {
                    Add((char)b); // single byte 
                }
                else if (b >= 0xc0 && b < 0xe0)
                {
                    sum = (b & 0x1f) << 6;
                    rest = 1;
                }
                else if (b >= 0xe0 && b < 0xf0)
                {
                    sum = (b & 0x0f) << 12;
                    rest = 2;
                }
            }
            else if (rest == 1)
            {
                sum |= (b & 0x3f);
                rest--;
                Add((char)sum);
            }
            else if (rest == 2)
            {
                sum |= (b & 0x3f) << 6;
                rest--;
            }
        }

        public void Clear()
        {
            count = 0;
            sum = 0;
            rest = 0;
        }

        public int ToInt()
        {
            return 0;
        }

    }

}