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
 * Copyright (c) 2020 The Dotnetty-Span-Fork Project (cuteant@outlook.com) All rights reserved.
 *
 *   https://github.com/cuteant/dotnetty-span-fork
 *
 * Licensed under the MIT license. See LICENSE file in the project root for full license information.
 */

using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using DotNetty.Common.Utilities;
using DotNetty.Codecs.Mqtt.Packets;

namespace DotNetty.Codecs.Mqtt
{
    #region -- ExceptionArgument --

    /// <summary>The convention for this enum is using the argument name as the enum name</summary>
    internal enum ExceptionArgument
    {
        topicFilter,
    }

    #endregion

    #region -- ExceptionResource --

    /// <summary>The convention for this enum is using the resource name as the enum name</summary>
    internal enum ExceptionResource
    {
    }

    #endregion

    partial class ThrowHelper
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowArgumentException(Packet packet)
        {
            throw GetException();
            ArgumentException GetException()
            {
                return new ArgumentException("Unknown packet type: " + packet.PacketType, nameof(packet));
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_231_1()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-2.3.1-1]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_312_11()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-3.1.2-11]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_312_22()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-3.1.2-22]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_312_3()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-3.1.2-3]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_383_3()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-3.8.3-3]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_383_4(int qos)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"[MQTT-3.8.3-4]. Invalid QoS value: {qos}.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_393_2(QualityOfService returnCode)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"[MQTT-3.9.3-2]. Invalid return code: {returnCode}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_3103_2()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-3.10.3-2]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_471_2(string topicFilter)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"[MQTT-4.7.1-2]. Invalid topic filter: {topicFilter}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_471_3(string topicFilter)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"[MQTT-4.7.1-3]. Invalid topic filter: {topicFilter}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MQTT_473_1()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("[MQTT-4.7.3-1]");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_RemainingLenExceeds4BytesInLen()
        {
            throw GetException();

            static DecoderException GetException()
            {
                return new DecoderException("Remaining length exceeds 4 bytes in length");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_DeclaredRemainingLen(int remainingLength)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"Declared remaining length is bigger than packet data size by {remainingLength}.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_UnexpectedQoSValueForPublish(QualityOfService qualityOfService)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"Unexpected QoS value of {(int)qualityOfService} for {PacketType.PUBLISH} packet.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static Packet FromDecoderException_FirstPacketByteValueIsInvalid(int packetSignature)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"First packet byte value of `{packetSignature}` is invalid.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_MsgIsTooBig(int completeMessageSize)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException("Message is too big: " + completeMessageSize);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_UnexpectedProtocolName(string protocolName)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"Unexpected protocol name. Expected: {Util.ProtocolName}. Actual: {protocolName}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_UnexpectedProtocolLevel(int protocolLevel)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"Unexpected protocol level. Expected: {Util.ProtocolLevel}. Actual: {protocolLevel}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_UnexpectedWillQoSValueOf(QualityOfService qualityOfService)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"[MQTT-3.1.2-14] Unexpected Will QoS value of {(int)qualityOfService}.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_StrIsShorterThanMinSize(int minBytes, int size)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"String value is shorter than minimum allowed {minBytes}. Advertised length: {size}");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        internal static void ThrowDecoderException_StrIsLongerThanMaxSize(int maxBytes, int size)
        {
            throw GetException();
            DecoderException GetException()
            {
                return new DecoderException($"String value is longer than maximum allowed {maxBytes}. Advertised length: {size}");
            }
        }
    }
}
