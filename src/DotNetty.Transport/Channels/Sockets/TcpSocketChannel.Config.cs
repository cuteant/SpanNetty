// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net.Sockets;
    using System.Threading;

    partial class TcpSocketChannel<TChannel>
    {
        sealed class TcpSocketChannelConfig : DefaultSocketChannelConfiguration
        {
            private int v_maxBytesPerGatheringWrite = int.MaxValue;

            public TcpSocketChannelConfig(TChannel channel, Socket javaSocket)
                : base(channel, javaSocket)
            {
                CalculateMaxBytesPerGatheringWrite();
            }

            public int GetMaxBytesPerGatheringWrite() => Volatile.Read(ref v_maxBytesPerGatheringWrite);

            public override int SendBufferSize
            {
                get => base.SendBufferSize;
                set
                {
                    base.SendBufferSize = value;
                    CalculateMaxBytesPerGatheringWrite();
                }
            }

            void CalculateMaxBytesPerGatheringWrite()
            {
                // Multiply by 2 to give some extra space in case the OS can process write data faster than we can provide.
                int newSendBufferSize = SendBufferSize << 1;
                if (newSendBufferSize > 0)
                {
                    Interlocked.Exchange(ref v_maxBytesPerGatheringWrite, newSendBufferSize);
                }
            }

            protected override void AutoReadCleared() => ((TChannel)Channel).ClearReadPending();
        }
    }
}