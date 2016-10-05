﻿using System;
using System.Collections.Generic;

namespace Greatbone.Core
{
    /// <summary>
    /// To generate a UTF-8 encoded JSON document. An extension of putting byte array is supported.
    /// </summary>
    public class JsonContent : DynamicContent, ISink<JsonContent>
    {
        const int InitialCapacity = 16 * 1024;

        // starting positions of each level
        readonly int[] nums;

        // current level
        int level;

        public JsonContent(int capacity = InitialCapacity) : base(capacity)
        {
            nums = new int[8];
            level = -1;
        }

        public override string Type => "application/json";


        public void Arr(Action a)
        {
            if (level >= 0 && nums[level] > 0)
            {
                Add(',');
            }

            level++;
            Add('[');

            a?.Invoke();

            Add(']');
            level--;
        }

        //
        // READ OBJECT
        //

        public void Obj(Action a)
        {
            if (level >= 0 && nums[level] > 0)
            {
                Add(',');
            }

            level++;
            Add('{');

            a?.Invoke();

            Add('}');
            level--;
        }

        public void Obj<T>(T obj) where T : IPersist
        {
        }


        public void _(int value)
        {
            if (nums[level] > 0)
            {
                Add(',');
            }

            Add(value);
        }

        public JsonContent Put<T>(T value, int x = -1) where T : IPersist
        {
            throw new NotImplementedException();
        }

        public JsonContent Arr<T>(List<T> lst, int x = -1) where T : IPersist
        {
            Arr(delegate
            {
                for (int i = 0; i < lst.Count; i++)
                {
                    Put(lst[i], x);
                }
            });
            return this;
        }

        public JsonContent Put(string name, bool value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');

            Add(value ? "true" : "false");

            return this;
        }

        public JsonContent Put(string name, short value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');
            Add(value);

            return this;
        }

        public JsonContent Put(string name, int value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');
            Add(value);

            return this;
        }

        public JsonContent Put(string name, long value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');
            Add(value);

            return this;
        }

        public JsonContent Put(string name, decimal value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');
            Add(value);

            return this;
        }

        public JsonContent Put(string name, DateTime value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');
            Add(value);

            return this;
        }

        public JsonContent Put(string name, string value)
        {
            if (nums[level]++ > 0)
            {
                Add(',');
            }

            Add('"');
            Add(name);
            Add('"');
            Add(':');

            if (value == null)
            {
                Add("null");
            }
            else
            {
                Add('"');
                Add(value);
                Add('"');
            }

            return this;
        }

        public JsonContent PutNull(string name)
        {
            throw new NotImplementedException();
        }

        public JsonContent Put<T>(string name, T v, int x = -1) where T : IPersist
        {
            throw new NotImplementedException();
        }

        public JsonContent Put<T>(string name, List<T> v, int x = -1) where T : IPersist
        {
            throw new NotImplementedException();
        }

        public JsonContent Put(string name, byte[] v)
        {
            throw new NotImplementedException();
        }

        public JsonContent Put(string name, JObj v)
        {
            throw new NotImplementedException();
        }

        public JsonContent Put(string name, JArr v)
        {
            throw new NotImplementedException();
        }
    }
}