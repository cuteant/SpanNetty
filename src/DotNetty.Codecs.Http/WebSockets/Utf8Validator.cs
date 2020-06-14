// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http.WebSockets
{
    using System;
    using System.Runtime.CompilerServices;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class Utf8Validator : IByteProcessor
    {
        private const int Utf8Accept = 0;
        private const int Utf8Reject = 12;

        private static ReadOnlySpan<byte> Types => new byte[]
        {
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1,
            1, 1, 1, 1, 1, 1, 1, 1, 1, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 9, 7, 7, 7, 7,
            7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 7, 8,
            8, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2, 2,
            2, 2, 10, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 3, 4, 3, 3, 11, 6, 6, 6, 5, 8, 8, 8, 8, 8,
            8, 8, 8, 8, 8, 8
        };

        private static ReadOnlySpan<byte> States => new byte[]
        {
            0, 12, 24, 36, 60, 96, 84, 12, 12, 12, 48, 72, 12, 12,
            12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 12, 0, 12, 12, 12, 12, 12, 0, 12, 0, 12, 12,
            12, 24, 12, 12, 12, 12, 12, 24, 12, 24, 12, 12, 12, 12, 12, 12, 12, 12, 12, 24, 12, 12,
            12, 12, 12, 24, 12, 12, 12, 12, 12, 12, 12, 24, 12, 12, 12, 12, 12, 12, 12, 12, 12, 36,
            12, 36, 12, 12, 12, 36, 12, 12, 12, 12, 12, 36, 12, 36, 12, 12, 12, 36, 12, 12, 12, 12,
            12, 12, 12, 12, 12, 12
        };

        private int _state = Utf8Accept;
        private int _codep;
        private bool _checking;

        public void Check(IByteBuffer buffer)
        {
            _checking = true;
            _ = buffer.ForEachByte(this);
        }

        public void Finish()
        {
            _checking = false;
            _codep = 0;
            if (_state != Utf8Accept)
            {
                _state = Utf8Accept;
                ThrowCorruptedFrameException();
            }
        }

        public bool Process(byte value)
        {
            var b = (int)value;
            byte type = Types[b & 0xFF];

            _codep = _state != Utf8Accept ? b & 0x3f | _codep << 6 : 0xff >> type & b;

            _state = States[_state + type];

            if (_state == Utf8Reject)
            {
                _checking = false;
                ThrowCorruptedFrameException();
            }
            return true;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ThrowCorruptedFrameException()
        {
            throw GetCorruptedFrameException();

            static CorruptedWebSocketFrameException GetCorruptedFrameException()
            {
                return new CorruptedWebSocketFrameException(WebSocketCloseStatus.InvalidPayloadData, "bytes are not UTF-8");
            }
        }

        public bool IsChecking => _checking;
    }
}
