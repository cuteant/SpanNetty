/*
 * Copyright 2012 The Netty Project
 *
 * The Netty Project licenses this file to you under the Apache License,
 * version 2.0 (the "License"); you may not use this file except in compliance
 * with the License. You may obtain a copy of the License at:
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS, WITHOUT
 * WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. See the
 * License for the specific language governing permissions and limitations
 * under the License.
 *
 * Copyright (c) The DotNetty Project (Microsoft). All rights reserved.
 *
 *   https://github.com/azure/dotnetty
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 *
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

namespace DotNetty.Codecs.Mqtt
{
    using System.Runtime.CompilerServices;
    using DotNetty.Codecs.Mqtt.Packets;

    static class Signatures
    {
        const byte QoS1Signature = (int)QualityOfService.AtLeastOnce << 1;

        // most often used (anticipated) come first

        [MethodImpl(InlineMethod.AggressiveInlining)]
        public static bool IsPublish(int signature)
        {
            const byte TypeOnlyMask = 0xf << 4;
            return (signature & TypeOnlyMask) == ((int)PacketType.PUBLISH << 4);
        }

        public const byte PubAck = (int)PacketType.PUBACK << 4;
        public const byte PubRec = (int)PacketType.PUBREC << 4;
        public const byte PubRel = ((int)PacketType.PUBREL << 4) | QoS1Signature;
        public const byte PubComp = (int)PacketType.PUBCOMP << 4;
        public const byte Connect = (int)PacketType.CONNECT << 4;
        public const byte ConnAck = (int)PacketType.CONNACK << 4;
        public const byte Subscribe = ((int)PacketType.SUBSCRIBE << 4) | QoS1Signature;
        public const byte SubAck = (int)PacketType.SUBACK << 4;
        public const byte PingReq = (int)PacketType.PINGREQ << 4;
        public const byte PingResp = (int)PacketType.PINGRESP << 4;
        public const byte Disconnect = (int)PacketType.DISCONNECT << 4;
        public const byte Unsubscribe = ((int)PacketType.UNSUBSCRIBE << 4) | QoS1Signature;
        public const byte UnsubAck = (int)PacketType.UNSUBACK << 4;
    }
}