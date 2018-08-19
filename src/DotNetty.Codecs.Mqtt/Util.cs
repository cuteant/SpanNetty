// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            DecoderException GetDecoderException()
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
            DecoderException GetDecoderException()
            {
                return new DecoderException("Client identifier is required.");
            }
        }
    }
}