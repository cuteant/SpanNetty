// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System.Collections.Generic;

    public interface IHeaders<TKey, TValue> : IEnumerable<HeaderEntry<TKey, TValue>> 
        where TKey : class
        where TValue : class
    {
        TValue Get(TKey name);

        TValue Get(TKey name, TValue defaultValue);

        TValue GetAndRemove(TKey name);

        TValue GetAndRemove(TKey name, TValue defaultValue);

        IList<TValue> GetAll(TKey name);

        IList<TValue> GetAllAndRemove(TKey name);

        bool? GetBoolean(TKey name);

        bool GetBoolean(TKey name, bool defaultValue);

        byte? GetByte(TKey name);

        byte GetByte(TKey name, byte defaultValue);

        char? GetChar(TKey name);

        char GetChar(TKey name, char defaultValue);

        short? GetShort(TKey name);

        short GetShort(TKey name, short defaultValue);

        int? GetInt(TKey name);

        int GetInt(TKey name, int defaultValue);

        long? GetLong(TKey name);

        long GetLong(TKey name, long defaultValue);

        float? GetFloat(TKey name);

        float GetFloat(TKey name, float defaultValue);

        double? GetDouble(TKey name);

        double GetDouble(TKey name, double defaultValue);

        long? GetTimeMillis(TKey name);

        long GetTimeMillis(TKey name, long defaultValue);

        bool? GetBooleanAndRemove(TKey name);

        bool GetBooleanAndRemove(TKey name, bool defaultValue);

        byte? GetByteAndRemove(TKey name);

        byte GetByteAndRemove(TKey name, byte defaultValue);

        char? GetCharAndRemove(TKey name);

        char GetCharAndRemove(TKey name, char defaultValue);

        short? GetShortAndRemove(TKey name);

        short GetShortAndRemove(TKey name, short defaultValue);

        int? GetIntAndRemove(TKey name);

        int GetIntAndRemove(TKey name, int defaultValue);

        long? GetLongAndRemove(TKey name);

        long GetLongAndRemove(TKey name, long defaultValue);

        float? GetFloatAndRemove(TKey name);

        float GetFloatAndRemove(TKey name, float defaultValue);

        double? GetDoubleAndRemove(TKey name);

        double GetDoubleAndRemove(TKey name, double defaultValue);

        long? GetTimeMillisAndRemove(TKey name);

        long GetTimeMillisAndRemove(TKey name, long defaultValue);

        bool Contains(TKey name);

        bool Contains(TKey name, TValue value);

        bool ContainsObject(TKey name, object value);

        bool ContainsBoolean(TKey name, bool value);

        bool ContainsByte(TKey name, byte value);

        bool ContainsChar(TKey name, char value);

        bool ContainsShort(TKey name, short value);

        bool ContainsInt(TKey name, int value);

        bool ContainsLong(TKey name, long value);

        bool ContainsFloat(TKey name, float value);

        bool ContainsDouble(TKey name, double value);

        bool ContainsTimeMillis(TKey name, long value);

        int Size { get; }

        bool IsEmpty { get; }

        ISet<TKey> Names();

        IHeaders<TKey, TValue> Add(TKey name, TValue value);

        IHeaders<TKey, TValue> Add(TKey name, IEnumerable<TValue> values);

        IHeaders<TKey, TValue> AddObject(TKey name, object value);

        IHeaders<TKey, TValue> AddObject(TKey name, IEnumerable<object> values);

        IHeaders<TKey, TValue> AddBoolean(TKey name, bool value);

        IHeaders<TKey, TValue> AddByte(TKey name, byte value);

        IHeaders<TKey, TValue> AddChar(TKey name, char value);

        IHeaders<TKey, TValue> AddShort(TKey name, short value);

        IHeaders<TKey, TValue> AddInt(TKey name, int value);

        IHeaders<TKey, TValue> AddLong(TKey name, long value);

        IHeaders<TKey, TValue> AddFloat(TKey name, float value);

        IHeaders<TKey, TValue> AddDouble(TKey name, double value);

        IHeaders<TKey, TValue> AddTimeMillis(TKey name, long value);

        IHeaders<TKey, TValue> Add(IHeaders<TKey, TValue> headers);

        IHeaders<TKey, TValue> Set(TKey name, TValue value);

        IHeaders<TKey, TValue> Set(TKey name, IEnumerable<TValue> values);

        IHeaders<TKey, TValue> SetObject(TKey name, object value);

        IHeaders<TKey, TValue> SetObject(TKey name, IEnumerable<object> values);

        IHeaders<TKey, TValue> SetBoolean(TKey name, bool value);

        IHeaders<TKey, TValue> SetByte(TKey name, byte value);

        IHeaders<TKey, TValue> SetChar(TKey name, char value);

        IHeaders<TKey, TValue> SetShort(TKey name, short value);

        IHeaders<TKey, TValue> SetInt(TKey name, int value);

        IHeaders<TKey, TValue> SetLong(TKey name, long value);

        IHeaders<TKey, TValue> SetFloat(TKey name, float value);

        IHeaders<TKey, TValue> SetDouble(TKey name, double value);

        IHeaders<TKey, TValue> SetTimeMillis(TKey name, long value);

        IHeaders<TKey, TValue> Set(IHeaders<TKey, TValue> headers);

        IHeaders<TKey, TValue> SetAll(IHeaders<TKey, TValue> headers);

        bool Remove(TKey name);

        IHeaders<TKey, TValue> Clear();
    }
}
