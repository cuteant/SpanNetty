// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs
{
    using System;

    public sealed class UnsupportedValueConverter<T> : IValueConverter<T>
    {
        public static readonly UnsupportedValueConverter<T> Instance = new UnsupportedValueConverter<T>();

        UnsupportedValueConverter()
        {
        }
        
        public T ConvertObject(object value)
        {
            throw new NotSupportedException();
        }

        public T ConvertBoolean(bool value)
        {
            throw new NotSupportedException();
        }

        public bool ConvertToBoolean(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertByte(byte value)
        {
            throw new NotSupportedException();
        }

        public byte ConvertToByte(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertChar(char value)
        {
            throw new NotSupportedException();
        }

        public char ConvertToChar(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertShort(short value)
        {
            throw new NotSupportedException();
        }

        public short ConvertToShort(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertInt(int value)
        {
            throw new NotSupportedException();
        }

        public int ConvertToInt(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertLong(long value)
        {
            throw new NotSupportedException();
        }

        public long ConvertToLong(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertTimeMillis(long value)
        {
            throw new NotSupportedException();
        }

        public long ConvertToTimeMillis(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertFloat(float value)
        {
            throw new NotSupportedException();
        }

        public float ConvertToFloat(T value)
        {
            throw new NotSupportedException();
        }

        public T ConvertDouble(double value)
        {
            throw new NotSupportedException();
        }

        public double ConvertToDouble(T value)
        {
            throw new NotSupportedException();
        }
    }
}