// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Buffers;
using DotNetty.Buffers;
using Xunit;

namespace System.Memory.Tests.BufferReader
{
    public class CopyTo
    {
        [Fact]
        public void TryCopyTo_Empty()
        {
            var reader = new ByteBufferReader(ReadOnlySequence<byte>.Empty);

            // Nothing to nothing is always possible
            Assert.True(reader.TryCopyTo(Span<byte>.Empty));

            // Nothing to something doesn't work
            Assert.False(reader.TryCopyTo(new byte[1]));
        }

        [Fact]
        public void TryCopyTo_Multisegment()
        {
            ReadOnlySequence<byte> chars = SequenceFactory.Create(new byte[][] {
                new byte[] { (byte)'A'           },
                new byte[] { (byte)'B', (byte)'C'      },
                new byte[] { (byte)'D', (byte)'E', (byte)'F' }
            });

            ReadOnlySpan<byte> linear = new byte[] { (byte)'A', (byte)'B', (byte)'C', (byte)'D', (byte)'E', (byte)'F' };

            var reader = new ByteBufferReader(chars);

            // Something to nothing is always possible
            Assert.True(reader.TryCopyTo(Span<byte>.Empty));
            Span<byte> buffer;

            // Read out ABCDEF, ABCDE, etc.
            for (int i = linear.Length; i > 0; i--)
            {
                buffer = new byte[i];
                Assert.True(reader.TryCopyTo(buffer));
                Assert.True(buffer.SequenceEqual(linear.Slice(0, i)));
            }

            buffer = new byte[1];

            // Read out one at a time and move through
            for (int i = 0; i < linear.Length; i++)
            {
                Assert.True(reader.TryCopyTo(buffer));
                Assert.True(reader.TryRead(out byte value));
                Assert.Equal(buffer[0], value);
            }

            // Trying to get more data than there is will fail
            Assert.False(reader.TryCopyTo(new byte[reader.Remaining + 1]));
        }
    }
}
