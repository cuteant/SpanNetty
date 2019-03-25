// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Buffers;
using System.Buffers.Text;
using System.Text;
using Xunit;

namespace DotNetty.Buffers.Tests
{
    public partial class BufferWriterTests
    {
        [Fact]
        public void Basics()
        {
            var buffer = Unpooled.Buffer(1024);
            var writer = new ByteBufferWriter(buffer);
            writer.WriteByte(1);
            writer.WriteShort(12);
            writer.WriteShortLE(21);

            writer.WriteMedium(13);
            writer.WriteMediumLE(31);

            writer.WriteInt(14);
            writer.WriteIntLE(41);
            writer.WriteLong(15);
            writer.WriteLongLE(51);

            writer.WriteDecimal(168.86m);
            writer.WriteDecimalLE(188.88m);

            writer.Flush();

            var reader = new ByteBufferReader(buffer);
            reader.TryRead(out byte b);
            Assert.Equal(1, b);
            reader.TryReadShort(out var sV);
            Assert.Equal(12, sV);
            reader.TryReadShortLE(out sV);
            Assert.Equal(21, sV);

            reader.TryReadMedium(out var iV);
            Assert.Equal(13, iV);
            reader.TryReadMediumLE(out iV);
            Assert.Equal(31, iV);

            reader.TryReadInt(out iV);
            Assert.Equal(14, iV);
            reader.TryReadIntLE(out iV);
            Assert.Equal(41, iV);
            reader.TryReadLong(out var lV);
            Assert.Equal(15, lV);
            reader.TryReadLongLE(out lV);
            Assert.Equal(51, lV);

            reader.TryReadDecimal(out var mV);
            Assert.Equal(168.86m, mV);
            reader.TryReadDecimalLE(out mV);
            Assert.Equal(188.88m, mV);
        }
    }
}
