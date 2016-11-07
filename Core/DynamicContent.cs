﻿using System;
using System.Globalization;
using System.Text;

namespace Greatbone.Core
{
    ///
    /// A dynamically generated content of either bytes or characters.
    ///
    public abstract class DynamicContent : IContent
    {
        static readonly char[] DIGIT =
        {
            '0',  '1',  '2',  '3',  '4',  '5',  '6',  '7',  '8',  '9'
        };

        // hexidecimal characters
        protected static readonly char[] HEX =
        {
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'
        };

        // sexagesimal numbers
        protected static readonly string[] SEX = {
            "00", "01", "02", "03", "04", "05", "06", "07", "08", "09",
            "10", "11", "12", "13", "14", "15", "16", "17", "18", "19",
            "20", "21", "22", "23", "24", "25", "26", "27", "28", "29",
            "30", "31", "32", "33", "34", "35", "36", "37", "38", "39",
            "40", "41", "42", "43", "44", "45", "46", "47", "48", "49",
            "50", "51", "52", "53", "54", "55", "56", "57", "58", "59"
        };

        static readonly short[] SHORT =
        {
            1,
            10,
            100,
            1000,
            10000
        };

        static readonly int[] INT =
        {
            1,
            10,
            100,
            1000,
            10000,
            100000,
            1000000,
            10000000,
            100000000,
            1000000000
        };

        static readonly long[] LONG =
        {
            1L,
            10L,
            100L,
            1000L,
            10000L,
            100000L,
            1000000L,
            10000000L,
            100000000L,
            1000000000L,
            10000000000L,
            100000000000L,
            1000000000000L,
            10000000000000L,
            100000000000000L,
            1000000000000000L,
            10000000000000000L,
            100000000000000000L,
            1000000000000000000L
        };

        readonly bool pooled;

        protected byte[] bytebuf; // NOTE: HttpResponseStream doesn't have internal buffer

        protected char[] charbuf;

        // number of bytes or chars
        protected int count;

        // byte-wise etag checksum, for text-based output only
        protected ulong checksum;

        protected DynamicContent(bool raw, bool pooled, int capacity)
        {
            this.pooled = pooled;
            if (raw)
            {
                bytebuf = pooled ? BufferUtility.GetByteBuffer(capacity) : new byte[capacity];
            }
            else
            {
                charbuf = pooled ? BufferUtility.GetCharBuffer(capacity) : new char[capacity];
            }
            this.count = 0;
        }

        public abstract string Type { get; }

        public bool IsRaw => bytebuf != null;

        public byte[] ByteBuffer => bytebuf;

        public char[] CharBuffer => charbuf;

        public int Size => count;

        public DateTime? Modified { get; set; } = null;

        public bool IsPooled => pooled;

        public ulong ETag => checksum;


        void AddByte(byte b)
        {
            // ensure capacity
            int olen = bytebuf.Length; // old length
            if (count >= olen)
            {
                int nlen = olen * 4; // new length
                byte[] obuf = bytebuf;
                bytebuf = pooled ? BufferUtility.GetByteBuffer(nlen) : new byte[nlen];
                Array.Copy(obuf, 0, bytebuf, 0, olen);
                if (pooled) BufferUtility.Return(obuf);
            }
            bytebuf[count++] = b;

            // calculate checksum
            ulong cs = checksum;
            cs ^= b; // XOR
            checksum = cs >> 57 | cs << 7; // circular left shift 7 bit
        }

        public void AddChar(char c)
        {
            if (IsRaw) // byte-oriented
            {
                // UTF-8 encoding but without surrogate support
                if (c < 0x80)
                {
                    // have at most seven bits
                    AddByte((byte)c);
                }
                else if (c < 0x800)
                {
                    // 2 char, 11 bits
                    AddByte((byte)(0xc0 | (c >> 6)));
                    AddByte((byte)(0x80 | (c & 0x3f)));
                }
                else
                {
                    // 3 char, 16 bits
                    AddByte((byte)(0xe0 | ((c >> 12))));
                    AddByte((byte)(0x80 | ((c >> 6) & 0x3f)));
                    AddByte((byte)(0x80 | (c & 0x3f)));
                }
            }
            else // char-oriented
            {
                // ensure capacity
                int olen = charbuf.Length; // old length
                if (count >= olen)
                {
                    int nlen = olen * 4; // new length
                    char[] obuf = charbuf;
                    charbuf = pooled ? BufferUtility.GetCharBuffer(nlen) : new char[nlen];
                    Array.Copy(obuf, 0, charbuf, 0, olen);
                    if (pooled) BufferUtility.Return(obuf);
                }
                charbuf[count++] = c;
            }
        }

        public void Add(bool v)
        {
            Add(v ? "true" : "false");
        }

        public void Add(char[] v)
        {
            Add(v, 0, v.Length);
        }

        public void Add(char[] v, int offset, int len)
        {
            if (v != null)
            {
                for (int i = offset; i < len; i++)
                {
                    Add(v[i], false);
                }
            }
        }

        public void Add(string v)
        {
            Add(v, 0, v.Length);
        }

        public void Add(string v, int offset, int len)
        {
            if (v != null)
            {
                for (int i = offset; i < len; i++)
                {
                    AddChar(v[i]);
                }
            }
        }

        public void Add(StringBuilder v)
        {
            Add(v, 0, v.Length);
        }

        public void Add(StringBuilder v, int offset, int len)
        {
            if (v != null)
            {
                for (int i = offset; i < len; i++)
                {
                    Add(v[i], false);
                }
            }
        }

        public void Add(short v)
        {
            if (v == 0)
            {
                AddByte((byte)'0');
                return;
            }
            int x = v; // convert to int
            if (v < 0)
            {
                AddChar('-');
                x = -x;
            }
            bool bgn = false;
            for (int i = SHORT.Length - 1; i > 0; i--)
            {
                int bas = SHORT[i];
                int q = x / bas;
                x = x % bas;
                if (q != 0 || bgn)
                {
                    AddChar(DIGIT[q]);
                    bgn = true;
                }
            }
            AddChar(DIGIT[x]); // last reminder
        }

        public void Add(int v)
        {
            if (v >= short.MinValue && v <= short.MaxValue)
            {
                Add((short)v);
                return;
            }

            if (v < 0)
            {
                AddChar('-');
                v = -v;
            }
            bool bgn = false;
            for (int i = INT.Length - 1; i > 0; i--)
            {
                int bas = INT[i];
                int q = v / bas;
                v = v % bas;
                if (q != 0 || bgn)
                {
                    AddChar(DIGIT[q]);
                    bgn = true;
                }
            }
            AddChar(DIGIT[v]); // last reminder
        }

        public void Add(long v)
        {
            if (v >= int.MinValue && v <= int.MaxValue)
            {
                Add((int)v);
                return;
            }

            if (v < 0)
            {
                AddChar('-');
                v = -v;
            }
            bool bgn = false;
            for (int i = LONG.Length - 1; i > 0; i--)
            {
                long bas = LONG[i];
                long q = v / bas;
                v = v % bas;
                if (q != 0 || bgn)
                {
                    AddChar(DIGIT[q]);
                    bgn = true;
                }
            }
            AddChar(DIGIT[v]); // last reminder
        }

        public void Add(decimal v)
        {
            Add(v, true);
        }

        public void Add(Number v)
        {
            Add(v.Long);
            if (v.Pt)
            {
                AddChar('.');
                Add(v.fract);
            }
        }

        // sign mask
        private const int Sign = unchecked((int)0x80000000);

        public void Add(decimal dec, bool money)
        {
            if (money)
            {
                int[] bits = decimal.GetBits(dec); // get the binary representation
                int low = bits[0], mid = bits[1], flags = bits[3];

                if ((flags & Sign) != 0) // negative
                {
                    AddChar('-');
                }
                if (mid != 0) // money
                {
                    long x = (low & 0x00ffffff) + ((long)(byte)(low >> 24) << 24) + ((long)mid << 32);
                    bool bgn = false;
                    for (int i = LONG.Length - 1; i >= 2; i--)
                    {
                        long bas = INT[i];
                        long q = x / bas;
                        x = x % bas;
                        if (q != 0 || bgn)
                        {
                            AddChar(DIGIT[q]);
                            bgn = true;
                        }
                        if (i == 4)
                        {
                            if (!bgn)
                            {
                                AddChar('0');
                                bgn = true;
                            }
                            AddChar('.');
                        }
                    }
                }
                else // smallmoney
                {
                    int x = low;
                    bool bgn = false;
                    for (int i = INT.Length - 1; i >= 2; i--)
                    {
                        int bas = INT[i];
                        int q = x / bas;
                        x = x % bas;
                        if (q != 0 || bgn)
                        {
                            AddChar(DIGIT[q]);
                            bgn = true;
                        }
                        if (i == 4)
                        {
                            if (!bgn)
                            {
                                AddChar('0');
                                bgn = true;
                            }
                            AddChar('.');
                        }
                    }
                }
            }
            else // ordinal decimal number
            {
                Add(dec.ToString(NumberFormatInfo.CurrentInfo));
            }
        }

        public void Add(DateTime v)
        {
            short yr = (short)v.Year;
            byte mon = (byte)v.Month,
            day = (byte)v.Day;

            // yyyy-mm-dd
            if (yr < 1000) AddChar('0');
            if (yr < 100) AddChar('0');
            if (yr < 10) AddChar('0');
            Add(v.Year);
            AddChar('-');
            Add(SEX[v.Month]);
            AddChar('-');
            Add(SEX[v.Day]);

            int hr = v.Hour, min = v.Minute, sec = v.Second, mil = v.Millisecond;
            if (hr == 0 && min == 0 && sec == 0 && mil == 0) return;

            AddChar(' '); // a space for separation
            Add(SEX[hr]);
            AddChar(':');
            Add(SEX[min]);
            AddChar(':');
            Add(SEX[sec]);
        }

        public void Replace(byte[] buffer, int count)
        {
            this.bytebuf = buffer;
            this.count = count;
        }


        public void Encrypt(int mask, int order)
        {
            int[] masks = { (mask >> 24) & 0xff, (mask >> 16) & 0xff, (mask >> 8) & 0xff, mask & 0xff };
            byte[] buf = new byte[count * 2]; // the target bytebuf
            int p = 0;
            for (int i = 0; i < count; i++)
            {
                // masking
                int b = bytebuf[i] ^ masks[i % 4];

                //transform
                buf[p++] = (byte)HEX[(b >> 4) & 0x0f];
                buf[p++] = (byte)HEX[(b) & 0x0f];

                // reordering

            }

            // replace
            bytebuf = buf;
            count = p;
        }

    }

}