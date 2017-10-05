// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// ReSharper disable ConvertToAutoProperty
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable ForCanBeConvertedToForeach
namespace DotNetty.Codecs
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics.Contracts;
    using System.Runtime.CompilerServices;
    using DotNetty.Common.Utilities;

    using static Common.Internal.MathUtil;

    public class DefaultHeaders<TKey, TValue> : IHeaders<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        const int HashCodeSeed = unchecked((int)0xc2b2ae35);

        static readonly DefaultHashingStrategy<TValue> DefaultValueHashingStrategy = new DefaultHashingStrategy<TValue>();
        static readonly DefaultHashingStrategy<TKey> DefaultKeyHashingStragety = new DefaultHashingStrategy<TKey>();
        static readonly NullNameValidator<TKey> DefaultKeyNameValidator = new NullNameValidator<TKey>();

        readonly HeaderEntry<TKey, TValue>[] entries;
        readonly HeaderEntry<TKey, TValue> head;

        readonly byte hashMask;
        protected readonly IValueConverter<TValue> ValueConverter;
        readonly INameValidator<TKey> nameValidator;
        readonly IHashingStrategy<TKey> hashingStrategy;
        int size;

        public DefaultHeaders(IValueConverter<TValue> valueConverter)
            : this(DefaultKeyHashingStragety, valueConverter, DefaultKeyNameValidator, 16)
        {
        }

        public DefaultHeaders(IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator)
            : this(DefaultKeyHashingStragety, valueConverter, nameValidator, 16)
        {
        }

        public DefaultHeaders(IHashingStrategy<TKey> nameHashingStrategy, IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator) 
            : this(nameHashingStrategy, valueConverter, nameValidator, 16)
        {
        }

        public DefaultHeaders(IHashingStrategy<TKey> nameHashingStrategy,
            IValueConverter<TValue> valueConverter, INameValidator<TKey> nameValidator, int arraySizeHint)
        {
            Contract.Requires(nameHashingStrategy != null);
            Contract.Requires(valueConverter != null);
            Contract.Requires(nameValidator != null);

            this.hashingStrategy = nameHashingStrategy;
            this.ValueConverter = valueConverter;
            this.nameValidator = nameValidator;

            // Enforce a bound of [2, 128] because hashMask is a byte. The max possible value of hashMask is one less
            // than the length of this array, and we want the mask to be > 0.
            this.entries = new HeaderEntry<TKey, TValue>[FindNextPositivePowerOfTwo(Math.Max(2, Math.Min(arraySizeHint, 128)))];
            this.hashMask = (byte)(this.entries.Length - 1);
            this.head = new HeaderEntry<TKey, TValue>();
        }

        public TValue Get(TKey name)
        {
            Contract.Requires(name != null);

            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            TValue value = null;
            // loop until the first header was found
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
                {
                    value = e.value;
                }

                e = e.Next;
            }
            return value;
        }

        public TValue Get(TKey name, TValue defaultValue)
        {
            TValue value = this.Get(name);
            return value ?? defaultValue;
        }

        public TValue GetAndRemove(TKey name)
        {
            Contract.Requires(name != null);

            int h = this.hashingStrategy.HashCode(name);
            return this.Remove0(h, this.Index(h), name);
        }

        public TValue GetAndRemove(TKey name, TValue defaultValue)
        {
            TValue value = this.GetAndRemove(name);
            return value ?? defaultValue;
        }

        public virtual IList<TValue> GetAll(TKey name)
        {
            Contract.Requires(name != null);

            var values = new List<TValue>();
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
                {
                    values.Insert(0, e.value);
                }

                e = e.Next;
            }
            return values;
        }

        public virtual IEnumerable<TValue> ValueIterator(TKey name) => new ValueEnumerator(this, name);

        public IList<TValue> GetAllAndRemove(TKey name)
        {
            IList<TValue> all = this.GetAll(name);
            this.Remove(name);
            return all;
        }

        public bool Contains(TKey name) => this.Get(name) != null;

        public bool ContainsObject(TKey name, object value)
        {
            Contract.Requires(value != null);
            return this.Contains(name, this.ValueConverter.ConvertObject(value));
        }

        public bool ContainsBoolean(TKey name, bool value) => this.Contains(name, this.ValueConverter.ConvertBoolean(value));

        public bool ContainsByte(TKey name, byte value) => this.Contains(name, this.ValueConverter.ConvertByte(value));

        public bool ContainsChar(TKey name, char value) => this.Contains(name, this.ValueConverter.ConvertChar(value));

        public bool ContainsShort(TKey name, short value) => this.Contains(name, this.ValueConverter.ConvertShort(value));

        public bool ContainsInt(TKey name, int value) => this.Contains(name, this.ValueConverter.ConvertInt(value));

        public bool ContainsLong(TKey name, long value) => this.Contains(name, this.ValueConverter.ConvertLong(value));

        public bool ContainsFloat(TKey name, float value) => this.Contains(name, this.ValueConverter.ConvertFloat(value));

        public bool ContainsDouble(TKey name, double value) => this.Contains(name, this.ValueConverter.ConvertDouble(value));

        public bool ContainsTimeMillis(TKey name, long value) => this.Contains(name, this.ValueConverter.ConvertTimeMillis(value));

        public bool Contains(TKey name, TValue value) => this.Contains(name, value, DefaultValueHashingStrategy);

        public bool Contains(TKey name, TValue value, IHashingStrategy<TValue> valueHashingStrategy)
        {
            Contract.Requires(name != null);

            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            HeaderEntry<TKey, TValue> e = this.entries[i];
            while (e != null)
            {
                if (e.Hash == h && this.hashingStrategy.Equals(name, e.key) 
                    && valueHashingStrategy.Equals(value, e.value))
                {
                    return true;
                }
                e = e.Next;
            }
            return false;
        }

        public int Size => this.size;

        public bool IsEmpty => this.head == this.head.After;

        public ISet<TKey> Names()
        {
            if (this.IsEmpty)
            {
                return ImmutableHashSet<TKey>.Empty;
            }

            var names = new HashSet<TKey>(this.hashingStrategy);
            HeaderEntry<TKey, TValue> e = this.head.After;
            while (e != this.head)
            {
                names.Add(e.key);
                e = e.After;
            }
            return names;
        }

        public virtual IHeaders<TKey, TValue> Add(TKey name, TValue value)
        {
            Contract.Requires(value != null);

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            this.Add0(h, i, name, value);
            return this;
        }

        public virtual IHeaders<TKey, TValue> Add(TKey name, IEnumerable<TValue> values)
        {
            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            foreach (TValue v in values)
            {
                this.Add0(h, i, name, v);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, object value)
        {
            Contract.Requires(value != null);

            return this.Add(name, this.ValueConverter.ConvertObject(value));
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, IEnumerable<object> values)
        {
            foreach (object value in values)
            {
                this.AddObject(name, value);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> AddObject(TKey name, params object[] values)
        {
            // ReSharper disable once ForCanBeConvertedToForeach
            // Avoid enumerator allocations
            for (int i = 0; i < values.Length; i++)
            {
                this.AddObject(name, values[i]);
            }

            return this;
        }

        public IHeaders<TKey, TValue> AddInt(TKey name, int value) => this.Add(name, this.ValueConverter.ConvertInt(value));

        public IHeaders<TKey, TValue> AddLong(TKey name, long value) => this.Add(name, this.ValueConverter.ConvertLong(value));

        public IHeaders<TKey, TValue> AddDouble(TKey name, double value) => this.Add(name, this.ValueConverter.ConvertDouble(value));

        public IHeaders<TKey, TValue> AddTimeMillis(TKey name, long value) => this.Add(name, this.ValueConverter.ConvertTimeMillis(value));

        public IHeaders<TKey, TValue> AddChar(TKey name, char value) => this.Add(name, this.ValueConverter.ConvertChar(value));

        public IHeaders<TKey, TValue> AddBoolean(TKey name, bool value) => this.Add(name, this.ValueConverter.ConvertBoolean(value));

        public IHeaders<TKey, TValue> AddFloat(TKey name, float value) =>  this.Add(name, this.ValueConverter.ConvertFloat(value));

        public IHeaders<TKey, TValue> AddByte(TKey name, byte value) => this.Add(name, this.ValueConverter.ConvertByte(value));

        public IHeaders<TKey, TValue> AddShort(TKey name, short value) => this.Add(name, this.ValueConverter.ConvertShort(value));

        public virtual IHeaders<TKey, TValue> Add(IHeaders<TKey, TValue> headers)
        {
            if (ReferenceEquals(headers, this))
            {
                throw new ArgumentException("can't add to itself.");
            }
            this.AddImpl(headers);
            return this;
        }

        protected void AddImpl(IHeaders<TKey, TValue> headers)
        {
            if (headers is DefaultHeaders<TKey, TValue> defaultHeaders)
            {
                HeaderEntry<TKey, TValue> e = defaultHeaders.head.After;

                if (defaultHeaders.hashingStrategy == this.hashingStrategy
                    && defaultHeaders.nameValidator == this.nameValidator)
                {
                    // Fastest copy
                    while (e != defaultHeaders.head)
                    {
                        this.Add0(e.Hash, this.Index(e.Hash), e.key, e.value);
                        e = e.After;
                    }
                }
                else
                {
                    // Fast copy
                    while (e != defaultHeaders.head)
                    {
                        this.Add(e.key, e.value);
                        e = e.After;
                    }
                }
            }
            else
            {
                // Slow copy
                foreach (HeaderEntry<TKey, TValue> header in headers)
                {
                    this.Add(header.key, header.value);
                }
            }
        }

        public IHeaders<TKey, TValue> Set(TKey name, TValue value)
        {
            Contract.Requires(value != null);

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);
            this.Remove0(h, i, name);
            this.Add0(h, i, name, value);
            return this;
        }

        public virtual IHeaders<TKey, TValue> Set(TKey name, IEnumerable<TValue> values)
        {
            Contract.Requires(values != null);

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);

            this.Remove0(h, i, name);
            foreach (TValue v in values)
            {
                if (v ==  null)
                {
                    break;
                }
                this.Add0(h, i, name, v);
            }

            return this;
        }

        public virtual IHeaders<TKey, TValue> SetObject(TKey name, object value)
        {
            Contract.Requires(value != null);

            TValue convertedValue = this.ValueConverter.ConvertObject(value);
            return this.Set(name, convertedValue);
        }

        public virtual IHeaders<TKey, TValue> SetObject(TKey name, IEnumerable<object> values)
        {
            Contract.Requires(values != null);

            this.nameValidator.ValidateName(name);
            int h = this.hashingStrategy.HashCode(name);
            int i = this.Index(h);

            this.Remove0(h, i, name);
            foreach (object v in values)
            {
                if (v == null)
                {
                    break;
                }
                this.Add0(h, i, name, this.ValueConverter.ConvertObject(v));
            }

            return this;
        }

        public IHeaders<TKey, TValue> SetInt(TKey name, int value) => this.Set(name, this.ValueConverter.ConvertInt(value));

        public IHeaders<TKey, TValue> SetLong(TKey name, long value) => this.Set(name, this.ValueConverter.ConvertLong(value));

        public IHeaders<TKey, TValue> SetDouble(TKey name, double value) => this.Set(name, this.ValueConverter.ConvertDouble(value));

        public IHeaders<TKey, TValue> SetTimeMillis(TKey name, long value) => this.Set(name, this.ValueConverter.ConvertTimeMillis(value));

        public IHeaders<TKey, TValue> SetFloat(TKey name, float value) => this.Set(name, this.ValueConverter.ConvertFloat(value));

        public IHeaders<TKey, TValue> SetChar(TKey name, char value) => this.Set(name, this.ValueConverter.ConvertChar(value));

        public IHeaders<TKey, TValue> SetBoolean(TKey name, bool value) => this.Set(name, this.ValueConverter.ConvertBoolean(value));

        public IHeaders<TKey, TValue> SetByte(TKey name, byte value) => this.Set(name, this.ValueConverter.ConvertByte(value));

        public IHeaders<TKey, TValue> SetShort(TKey name, short value) => this.Set(name, this.ValueConverter.ConvertShort(value));



        public virtual IHeaders<TKey, TValue> Set(IHeaders<TKey, TValue> headers)
        {
            if (!ReferenceEquals(headers, this))
            {
                this.Clear();
                this.AddImpl(headers);
            }
            return this;
        }

        public virtual IHeaders<TKey, TValue> SetAll(IHeaders<TKey, TValue> headers)
        {
            if (!ReferenceEquals(headers, this))
            {
                foreach (TKey key in headers.Names())
                {
                    this.Remove(key);
                }
                this.AddImpl(headers);
            }
            return this;
        }

        public bool Remove(TKey name) => this.GetAndRemove(name) != null;

        public IHeaders<TKey, TValue> Clear()
        {
            this.entries.Fill(null);
            this.head.Before = this.head.After = this.head;
            this.size = 0;
            return this;
        }

        public IEnumerator<HeaderEntry<TKey, TValue>> GetEnumerator() => new HeaderEnumerator(this);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public bool? GetBoolean(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToBoolean(v) : default(bool?);
        }

        public bool GetBoolean(TKey name, bool defaultValue) => this.GetBoolean(name) ?? defaultValue;

        public byte? GetByte(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToByte(v) : default(byte?);
        }

        public byte GetByte(TKey name, byte defaultValue) => this.GetByte(name) ?? defaultValue;

        public char? GetChar(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToChar(v) : default(char?);
        }

        public char GetChar(TKey name, char defaultValue) => this.GetChar(name) ?? defaultValue;

        public short? GetShort(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToShort(v) : default(short?);
        }

        public short GetShort(TKey name, short defaultValue) => this.GetShort(name) ?? defaultValue;

        public int? GetInt(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToInt(v) : default(int?);
        }

        public int GetInt(TKey name, int defaultValue) => this.GetInt(name) ?? defaultValue;

        public long? GetLong(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToLong(v) : default(long?);
        }

        public long GetLong(TKey name, long defaultValue) => this.GetLong(name) ?? defaultValue;

        public float? GetFloat(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToFloat(v) : default(float?);
        }

        public float GetFloat(TKey name, float defaultValue) => this.GetFloat(name) ?? defaultValue;

        public double? GetDouble(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToDouble(v) : default(double?);
        }

        public double GetDouble(TKey name, double defaultValue) => this.GetDouble(name) ?? defaultValue;

        public long? GetTimeMillis(TKey name)
        {
            TValue v = this.Get(name);
            return v != null ? this.ValueConverter.ConvertToTimeMillis(v) : default(long?);
        }

        public long GetTimeMillis(TKey name, long defaultValue) => this.GetTimeMillis(name) ?? defaultValue;

        public bool? GetBooleanAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToBoolean(v) : default(bool?);
        }

        public bool GetBooleanAndRemove(TKey name, bool defaultValue) => this.GetBooleanAndRemove(name) ?? defaultValue;

        public byte? GetByteAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToByte(v) : default(byte?);
        }

        public byte GetByteAndRemove(TKey name, byte defaultValue) => this.GetByteAndRemove(name) ?? defaultValue;

        public char? GetCharAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            if (v == null)
            {
                return null;
            }
            try
            {
                return this.ValueConverter.ConvertToChar(v);
            }
            catch
            {
                return null;
            }
        }

        public char GetCharAndRemove(TKey name, char defaultValue) => this.GetCharAndRemove(name) ?? defaultValue;

        public short? GetShortAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToShort(v) : default(short?);
        }

        public short GetShortAndRemove(TKey name, short defaultValue) => this.GetShortAndRemove(name) ?? defaultValue;

        public int? GetIntAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToInt(v) : default(int?);
        }

        public int GetIntAndRemove(TKey name, int defaultValue) => this.GetIntAndRemove(name) ?? defaultValue;

        public long? GetLongAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToLong(v) : default(long?);
        }

        public long GetLongAndRemove(TKey name, long defaultValue) => this.GetLongAndRemove(name) ?? defaultValue;

        public float? GetFloatAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToFloat(v) : default(float?);
        }

        public float GetFloatAndRemove(TKey name, float defaultValue) => this.GetFloatAndRemove(name) ?? defaultValue;

        public double? GetDoubleAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToDouble(v) : default(double?);
        }

        public double GetDoubleAndRemove(TKey name, double defaultValue) => this.GetDoubleAndRemove(name) ?? defaultValue;

        public long? GetTimeMillisAndRemove(TKey name)
        {
            TValue v = this.GetAndRemove(name);
            return v != null ? this.ValueConverter.ConvertToTimeMillis(v) : default(long?);
        }

        public long GetTimeMillisAndRemove(TKey name, long defaultValue) => this.GetTimeMillisAndRemove(name) ?? defaultValue;

        public override bool Equals(object obj) => 
            obj is IHeaders<TKey, TValue> headers && this.Equals(headers, DefaultValueHashingStrategy);

        public override int GetHashCode() => this.HashCode(DefaultValueHashingStrategy);

        public bool Equals(IHeaders<TKey, TValue> h2, IHashingStrategy<TValue> valueHashingStrategy)
        {
            if (h2.Size != this.size)
            {
                return false;
            }

            if (ReferenceEquals(this, h2))
            {
                return true;
            }

            foreach (TKey name in this.Names())
            {
                IList<TValue> otherValues = h2.GetAll(name);
                IList<TValue> values = this.GetAll(name);
                if (otherValues.Count != values.Count)
                {
                    return false;
                }
                for (int i = 0; i < otherValues.Count; i++)
                {
                    if (!valueHashingStrategy.Equals(otherValues[i], values[i]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        public int HashCode(IHashingStrategy<TValue> valueHashingStrategy)
        {
            int result = HashCodeSeed;
            foreach (TKey name in this.Names())
            {
                result = 31 * result + this.hashingStrategy.HashCode(name);
                IList<TValue> values = this.GetAll(name);
                for (int i = 0; i < values.Count; ++i)
                {
                    result = 31 * result + valueHashingStrategy.HashCode(values[i]);
                }
            }
            return result;
        }

        public override string ToString() => HeadersUtils.ToString(this, this.size);

        protected HeaderEntry<TKey, TValue> NewHeaderEntry(int h, TKey name, TValue value, HeaderEntry<TKey, TValue> next) =>
            new HeaderEntry<TKey, TValue>(h, name, value, next, this.head);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        int Index(int hash) => hash & this.hashMask;

        void Add0(int h, int i, TKey name, TValue value)
        {
            // Update the hash table.
            this.entries[i] = this.NewHeaderEntry(h, name, value, this.entries[i]);
            ++this.size;
        }

        TValue Remove0(int h, int i, TKey name)
        {
            HeaderEntry<TKey, TValue> e = this.entries[i];
            if (e == null)
            {
                return null;
            }

            TValue value = null;
            HeaderEntry<TKey, TValue> next = e.Next;
            while (next != null)
            {
                if (next.Hash == h && this.hashingStrategy.Equals(name, next.key))
                {
                    value = next.value;
                    e.Next = next.Next;
                    next.Remove();
                    --this.size;
                }
                else
                {
                    e = next;
                }

                next = e.Next;
            }

            e = this.entries[i];
            if (e.Hash == h && this.hashingStrategy.Equals(name, e.key))
            {
                if (value == null)
                {
                    value = e.value;
                }
                this.entries[i] = e.Next;
                e.Remove();
                --this.size;
            }

            return value;
        }

        struct ValueEnumerator : IEnumerator<TValue>, IEnumerable<TValue>
        {
            readonly IHashingStrategy<TKey> hashingStrategy;
            readonly int hash;
            readonly TKey name;
            readonly HeaderEntry<TKey, TValue> head;
            HeaderEntry<TKey, TValue> node;
            TValue current;

            public ValueEnumerator(DefaultHeaders<TKey, TValue> headers, TKey name)
            {
                Contract.Requires(name != null);

                this.hashingStrategy = headers.hashingStrategy;
                this.hash = this.hashingStrategy.HashCode(name);
                this.name = name;
                this.node = this.head = headers.entries[headers.Index(this.hash)];
                this.current = null;
            }

            bool IEnumerator.MoveNext()
            {
                if (this.node == null)
                {
                    return false;
                }

                this.current = this.node.value;
                this.CalculateNext(this.node.Next);
                return true;
            }

            void CalculateNext(HeaderEntry<TKey, TValue> entry)
            {
                while (entry != null)
                {
                    if (entry.Hash == this.hash && this.hashingStrategy.Equals(this.name, entry.key))
                    {
                        this.node = entry;
                        return;
                    }
                    entry = entry.Next;
                }
                this.node = null;
            }

            TValue IEnumerator<TValue>.Current => this.current;

            object IEnumerator.Current => this.current;

            void IEnumerator.Reset()
            {
                this.node = this.head;
                this.current = null;
            }

            void IDisposable.Dispose()
            {
                this.node = null;
                this.current = null;
            }

            public IEnumerator<TValue> GetEnumerator() => this;

            IEnumerator IEnumerable.GetEnumerator() => this;
        }

        struct HeaderEnumerator : IEnumerator<HeaderEntry<TKey, TValue>>
        {
            readonly HeaderEntry<TKey, TValue> head;
            readonly int size;

            HeaderEntry<TKey, TValue> node;
            int index;

            public HeaderEnumerator(DefaultHeaders<TKey, TValue> headers)
            {
                this.head = headers.head;
                this.size = headers.size;
                this.node = this.head;
                this.index = 0;
            }

            public HeaderEntry<TKey, TValue> Current => this.node;

            object IEnumerator.Current
            {
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                get
                {
                    if (this.index == 0 || this.index == this.size + 1)
                    {
                        ThrowInvalidOperationException("Enumerator not initialized or completed.");
                    }
                    return this.node;
                }
            }

            public bool MoveNext()
            {
                if (this.node == null)
                {
                    this.index = this.size + 1;
                    return false;
                }

                this.index++;
                this.node = this.node.After;
                if (this.node == this.head)
                {
                    this.node = null;
                    return false;
                }
                return true;
            }

            public void Reset()
            {
                this.node = this.head.After;
                this.index = 0;
            }

            public void Dispose()
            {
                this.node = null;
                this.index = 0;
            }
        }

        static void ThrowInvalidOperationException(string message) => throw new InvalidOperationException(message);
    }

    public sealed class HeaderEntry<TKey, TValue>
        where TKey : class
        where TValue : class
    {
        internal readonly int Hash;
        // ReSharper disable InconsistentNaming
        internal readonly TKey key;
        internal TValue value;
        // ReSharper restore InconsistentNaming

        internal HeaderEntry<TKey, TValue> Next;
        internal HeaderEntry<TKey, TValue> Before;
        internal HeaderEntry<TKey, TValue> After;

        public HeaderEntry(int hash, TKey key)
        {
            this.Hash = hash;
            this.key = key;
        }

        internal HeaderEntry()
        {
            this.Hash = -1;
            this.key = default(TKey);
            this.Before = this;
            this.After = this;
        }

        internal HeaderEntry(int hash, TKey key, TValue value,
            HeaderEntry<TKey, TValue> next, HeaderEntry<TKey, TValue> head)
        {
            this.Hash = hash;
            this.key = key;
            this.value = value;
            this.Next = next;

            this.After = head;
            this.Before = head.Before;
            // PointNeighborsToThis
            this.Before.After = this;
            this.After.Before = this;
        }

        internal void Remove()
        {
            this.Before.After = this.After;
            this.After.Before = this.Before;
        }

        public override int GetHashCode() => this.Hash;

        public TKey Key => this.key;

        public TValue Value => this.value;

        public TValue SetValue(TValue newValue)
        {
            Contract.Requires(newValue != null);

            TValue oldValue = this.value;
            this.value = newValue;

            return oldValue;
        }

        public override string ToString() => this.Hash == -1 ? "Empty" : $"{this.key}={this.value}";
    }
}
