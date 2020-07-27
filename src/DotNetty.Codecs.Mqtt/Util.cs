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

using System.Runtime.CompilerServices;

namespace DotNetty.Codecs.Mqtt
{
    static class Util
    {
        public const string ProtocolName = "MQTT";
        public const int ProtocolLevel = 4;

        internal static readonly char[] TopicWildcards = { '#', '+' };

        public static void ValidateTopicName()
        {
            throw GetDecoderException();

            static DecoderException GetDecoderException()
            {
                return new DecoderException("[MQTT-4.7.3-1]");
            }
        }
        public static void ValidateTopicName(string topicName)
        {
            throw GetDecoderException();
            DecoderException GetDecoderException()
            {
                return new DecoderException($"Invalid PUBLISH topic name: {topicName}");
            }
        }

        public static void ValidatePacketId(int packetId)
        {
            if (packetId < 1)
            {
                throw new DecoderException("Invalid packet identifier: " + packetId);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        public static void ValidateClientId()
        {
            throw GetDecoderException();

            static DecoderException GetDecoderException()
            {
                return new DecoderException("Client identifier is required.");
            }
        }
    }
}