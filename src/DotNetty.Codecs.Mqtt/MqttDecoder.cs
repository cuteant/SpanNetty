// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    public sealed partial class MqttDecoder : ReplayingDecoder<MqttDecoder.ParseState>
    {
        public enum ParseState
        {
            Ready,
            Failed
        }

        readonly bool isServer;
        readonly int maxMessageSize;

        public MqttDecoder(bool isServer, int maxMessageSize)
            : base(ParseState.Ready)
        {
            this.isServer = isServer;
            this.maxMessageSize = maxMessageSize;
        }

        protected override void Decode(IChannelHandlerContext context, IByteBuffer input, List<object> output)
        {
            try
            {
                switch (this.State)
                {
                    case ParseState.Ready:
                        if (!this.TryDecodePacket(input, context, out var packet))
                        {
                            this.RequestReplay();
                            return;
                        }

                        output.Add(packet);
                        this.Checkpoint();
                        break;
                    case ParseState.Failed:
                        // read out data until connection is closed
                        input.SkipBytes(input.ReadableBytes);
                        return;
                    default:
                        ThrowHelper.ThrowArgumentOutOfRangeException(); break;
                }
            }
            catch (DecoderException)
            {
                input.SkipBytes(input.ReadableBytes);
                this.Checkpoint(ParseState.Failed);
                throw;
            }
        }

        bool TryDecodePacket(IByteBuffer buffer, IChannelHandlerContext context, out Packet packet)
        {
            if (!buffer.IsReadable(2)) // packet consists of at least 2 bytes
            {
                packet = null;
                return false;
            }

            int signature = buffer.ReadByte();

            if (!this.TryDecodeRemainingLength(buffer, out var remainingLength) || !buffer.IsReadable(remainingLength))
            {
                packet = null;
                return false;
            }

            packet = this.DecodePacketInternal(buffer, signature, ref remainingLength, context);

            if (remainingLength > 0)
            {
                ThrowHelper.ThrowDecoderException_DeclaredRemainingLen(remainingLength);
            }

            return true;
        }

        Packet DecodePacketInternal(IByteBuffer buffer, int packetSignature, ref int remainingLength, IChannelHandlerContext context)
        {
            if (Signatures.IsPublish(packetSignature))
            {
                var qualityOfService = (QualityOfService)((packetSignature >> 1) & 0x3); // take bits #1 and #2 ONLY and convert them into QoS value
                if (qualityOfService == QualityOfService.Reserved)
                {
                    ThrowHelper.ThrowDecoderException_UnexpectedQoSValueForPublish(qualityOfService);
                }

                bool duplicate = (packetSignature & 0x8) == 0x8; // test bit#3
                bool retain = (packetSignature & 0x1) != 0; // test bit#0
                var packet = new PublishPacket(qualityOfService, duplicate, retain);
                DecodePublishPacket(buffer, packet, ref remainingLength);
                return packet;
            }

            switch (packetSignature) // strict match checks for valid message type + correct values in flags part
            {
                case Signatures.PubAck:
                    var pubAckPacket = new PubAckPacket();
                    DecodePacketIdVariableHeader(buffer, pubAckPacket, ref remainingLength);
                    return pubAckPacket;
                case Signatures.PubRec:
                    var pubRecPacket = new PubRecPacket();
                    DecodePacketIdVariableHeader(buffer, pubRecPacket, ref remainingLength);
                    return pubRecPacket;
                case Signatures.PubRel:
                    var pubRelPacket = new PubRelPacket();
                    DecodePacketIdVariableHeader(buffer, pubRelPacket, ref remainingLength);
                    return pubRelPacket;
                case Signatures.PubComp:
                    var pubCompPacket = new PubCompPacket();
                    DecodePacketIdVariableHeader(buffer, pubCompPacket, ref remainingLength);
                    return pubCompPacket;
                case Signatures.PingReq:
                    if (!this.isServer) ValidateServerPacketExpected(packetSignature);
                    return PingReqPacket.Instance;
                case Signatures.Subscribe:
                    if (!this.isServer) ValidateServerPacketExpected(packetSignature);
                    var subscribePacket = new SubscribePacket();
                    DecodePacketIdVariableHeader(buffer, subscribePacket, ref remainingLength);
                    DecodeSubscribePayload(buffer, subscribePacket, ref remainingLength);
                    return subscribePacket;
                case Signatures.Unsubscribe:
                    if (!this.isServer) ValidateServerPacketExpected(packetSignature);
                    var unsubscribePacket = new UnsubscribePacket();
                    DecodePacketIdVariableHeader(buffer, unsubscribePacket, ref remainingLength);
                    DecodeUnsubscribePayload(buffer, unsubscribePacket, ref remainingLength);
                    return unsubscribePacket;
                case Signatures.Connect:
                    if (!this.isServer) ValidateServerPacketExpected(packetSignature);
                    var connectPacket = new ConnectPacket();
                    DecodeConnectPacket(buffer, connectPacket, ref remainingLength, context);
                    return connectPacket;
                case Signatures.Disconnect:
                    if (!this.isServer) ValidateServerPacketExpected(packetSignature);
                    return DisconnectPacket.Instance;
                case Signatures.ConnAck:
                    if (this.isServer) ValidateClientPacketExpected(packetSignature);
                    var connAckPacket = new ConnAckPacket();
                    DecodeConnAckPacket(buffer, connAckPacket, ref remainingLength);
                    return connAckPacket;
                case Signatures.SubAck:
                    if (this.isServer) ValidateClientPacketExpected(packetSignature);
                    var subAckPacket = new SubAckPacket();
                    DecodePacketIdVariableHeader(buffer, subAckPacket, ref remainingLength);
                    DecodeSubAckPayload(buffer, subAckPacket, ref remainingLength);
                    return subAckPacket;
                case Signatures.UnsubAck:
                    if (this.isServer) ValidateClientPacketExpected(packetSignature);
                    var unsubAckPacket = new UnsubAckPacket();
                    DecodePacketIdVariableHeader(buffer, unsubAckPacket, ref remainingLength);
                    return unsubAckPacket;
                case Signatures.PingResp:
                    if (this.isServer) ValidateClientPacketExpected(packetSignature);
                    return PingRespPacket.Instance;
                default:
                    return ThrowHelper.ThrowDecoderException_FirstPacketByteValueIsInvalid(packetSignature);
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ValidateServerPacketExpected(int signature)
        {
            throw GetDecoderException();
            DecoderException GetDecoderException()
            {
                return new DecoderException($"Packet type determined through first packet byte `{signature}` is not supported by MQTT client.");
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ValidateClientPacketExpected(int signature)
        {
            throw GetDecoderException();
            DecoderException GetDecoderException()
            {
                return new DecoderException($"Packet type determined through first packet byte `{signature}` is not supported by MQTT server.");
            }
        }

        bool TryDecodeRemainingLength(IByteBuffer buffer, out int value)
        {
            int readable = buffer.ReadableBytes;

            int result = 0;
            int multiplier = 1;
            byte digit;
            int read = 0;
            do
            {
                if (readable < read + 1)
                {
                    value = default;
                    return false;
                }
                digit = buffer.ReadByte();
                result += (digit & 0x7f) * multiplier;
                multiplier <<= 7;
                read++;
            }
            while ((digit & 0x80) != 0 && read < 4);

            if (read == 4 && (digit & 0x80) != 0)
            {
                ThrowHelper.ThrowDecoderException_RemainingLenExceeds4BytesInLen();
            }

            int completeMessageSize = result + 1 + read;
            if (completeMessageSize > this.maxMessageSize)
            {
                ThrowHelper.ThrowDecoderException_MsgIsTooBig(completeMessageSize);
            }

            value = result;
            return true;
        }

        static void DecodeConnectPacket(IByteBuffer buffer, ConnectPacket packet, ref int remainingLength, IChannelHandlerContext context)
        {
            string protocolName = DecodeString(buffer, ref remainingLength);
            if (!string.Equals(Util.ProtocolName, protocolName
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                ))
#else
                , StringComparison.Ordinal))
#endif
            {
                ThrowHelper.ThrowDecoderException_UnexpectedProtocolName(protocolName);
            }
            packet.ProtocolName = Util.ProtocolName;

            DecreaseRemainingLength(ref remainingLength, 1);
            packet.ProtocolLevel = buffer.ReadByte();

            if (packet.ProtocolLevel != Util.ProtocolLevel)
            {
                var connAckPacket = new ConnAckPacket
                {
                    ReturnCode = ConnectReturnCode.RefusedUnacceptableProtocolVersion
                };
                context.WriteAndFlushAsync(connAckPacket);
                ThrowHelper.ThrowDecoderException_UnexpectedProtocolLevel(packet.ProtocolLevel);
            }

            DecreaseRemainingLength(ref remainingLength, 1);
            int connectFlags = buffer.ReadByte();

            packet.CleanSession = (connectFlags & 0x02) == 0x02;

            bool hasWill = (connectFlags & 0x04) == 0x04;
            if (hasWill)
            {
                packet.HasWill = true;
                packet.WillRetain = (connectFlags & 0x20) == 0x20;
                var willQualityOfService = packet.WillQualityOfService = (QualityOfService)((connectFlags & 0x18) >> 3);
                if (willQualityOfService == QualityOfService.Reserved)
                {
                    ThrowHelper.ThrowDecoderException_UnexpectedWillQoSValueOf(willQualityOfService);
                }
                packet.WillTopicName = string.Empty;
            }
            else if ((connectFlags & 0x38) != 0) // bits 3,4,5 [MQTT-3.1.2-11]
            {
                ThrowHelper.ThrowDecoderException_MQTT_312_11();
            }

            packet.HasUsername = (connectFlags & 0x80) == 0x80;
            packet.HasPassword = (connectFlags & 0x40) == 0x40;
            if (packet.HasPassword && !packet.HasUsername)
            {
                ThrowHelper.ThrowDecoderException_MQTT_312_22();
            }
            if ((connectFlags & 0x1) != 0) // [MQTT-3.1.2-3]
            {
                ThrowHelper.ThrowDecoderException_MQTT_312_3();
            }

            packet.KeepAliveInSeconds = DecodeUnsignedShort(buffer, ref remainingLength);

            string clientId = DecodeString(buffer, ref remainingLength);
            if (clientId == null) Util.ValidateClientId();
            packet.ClientId = clientId;

            if (hasWill)
            {
                packet.WillTopicName = DecodeString(buffer, ref remainingLength);
                int willMessageLength = DecodeUnsignedShort(buffer, ref remainingLength);
                DecreaseRemainingLength(ref remainingLength, willMessageLength);
                packet.WillMessage = buffer.ReadBytes(willMessageLength);
            }

            if (packet.HasUsername)
            {
                packet.Username = DecodeString(buffer, ref remainingLength);
            }

            if (packet.HasPassword)
            {
                packet.Password = DecodeString(buffer, ref remainingLength);
            }
        }

        static void DecodeConnAckPacket(IByteBuffer buffer, ConnAckPacket packet, ref int remainingLength)
        {
            int ackData = DecodeUnsignedShort(buffer, ref remainingLength);
            packet.SessionPresent = ((ackData >> 8) & 0x1) != 0;
            packet.ReturnCode = (ConnectReturnCode)(ackData & 0xFF);
        }

        static void DecodePublishPacket(IByteBuffer buffer, PublishPacket packet, ref int remainingLength)
        {
            string topicName = DecodeString(buffer, ref remainingLength, 1);
            if (0u >= (uint)topicName.Length) Util.ValidateTopicName();
            if (topicName.IndexOfAny(Util.TopicWildcards) > 0) Util.ValidateTopicName(topicName);

            packet.TopicName = topicName;
            if (packet.QualityOfService > QualityOfService.AtMostOnce)
            {
                DecodePacketIdVariableHeader(buffer, packet, ref remainingLength);
            }

            IByteBuffer payload;
            if (remainingLength > 0)
            {
                payload = buffer.ReadSlice(remainingLength);
                payload.Retain();
                remainingLength = 0;
            }
            else
            {
                payload = Unpooled.Empty;
            }
            packet.Payload = payload;
        }

        static void DecodePacketIdVariableHeader(IByteBuffer buffer, PacketWithId packet, ref int remainingLength)
        {
            int packetId = packet.PacketId = DecodeUnsignedShort(buffer, ref remainingLength);
            if (0u >= (uint)packetId)
            {
                ThrowHelper.ThrowDecoderException_MQTT_231_1();
            }
        }

        static void DecodeSubscribePayload(IByteBuffer buffer, SubscribePacket packet, ref int remainingLength)
        {
            const int ReservedQos = (int)QualityOfService.Reserved;
            var subscribeTopics = new List<SubscriptionRequest>();
            while (remainingLength > 0)
            {
                string topicFilter = DecodeString(buffer, ref remainingLength);
                ValidateTopicFilter(topicFilter);

                DecreaseRemainingLength(ref remainingLength, 1);
                int qos = buffer.ReadByte();
                if (qos >= ReservedQos)
                {
                    ThrowHelper.ThrowDecoderException_MQTT_383_4(qos);
                }

                subscribeTopics.Add(new SubscriptionRequest(topicFilter, (QualityOfService)qos));
            }

            if (0u >= (uint)subscribeTopics.Count)
            {
                ThrowHelper.ThrowDecoderException_MQTT_383_3();
            }

            packet.Requests = subscribeTopics;
        }

        //static void ValidateTopicFilter(string topicFilter)
        //{
        //    int length = topicFilter.Length;
        //    if (0u >= (uint)length)
        //    {
        //        ThrowHelper.ThrowDecoderException_MQTT_473_1();
        //    }

        //    for (int i = 0; i < length; i++)
        //    {
        //        char c = topicFilter[i];
        //        switch (c)
        //        {
        //            case '+':
        //                if ((i > 0 && topicFilter[i - 1] != '/') || (i < length - 1 && topicFilter[i + 1] != '/'))
        //                {
        //                    ThrowHelper.ThrowDecoderException_MQTT_471_3(topicFilter);
        //                }
        //                break;
        //            case '#':
        //                if (i < length - 1 || (i > 0 && topicFilter[i - 1] != '/'))
        //                {
        //                    ThrowHelper.ThrowDecoderException_MQTT_471_2(topicFilter);
        //                }
        //                break;
        //        }
        //    }
        //}

        static void DecodeSubAckPayload(IByteBuffer buffer, SubAckPacket packet, ref int remainingLength)
        {
            var returnCodes = new QualityOfService[remainingLength];
            for (int i = 0; i < remainingLength; i++)
            {
                var returnCode = (QualityOfService)buffer.ReadByte();
                if (returnCode > QualityOfService.ExactlyOnce && returnCode != QualityOfService.Failure)
                {
                    ThrowHelper.ThrowDecoderException_MQTT_393_2(returnCode);
                }
                returnCodes[i] = returnCode;
            }
            packet.ReturnCodes = returnCodes;

            remainingLength = 0;
        }

        static void DecodeUnsubscribePayload(IByteBuffer buffer, UnsubscribePacket packet, ref int remainingLength)
        {
            var unsubscribeTopics = new List<string>();
            while (remainingLength > 0)
            {
                string topicFilter = DecodeString(buffer, ref remainingLength);
                ValidateTopicFilter(topicFilter);
                unsubscribeTopics.Add(topicFilter);
            }

            if (0u >= (uint)unsubscribeTopics.Count)
            {
                ThrowHelper.ThrowDecoderException_MQTT_3103_2();
            }

            packet.TopicFilters = unsubscribeTopics;

            remainingLength = 0;
        }

        static int DecodeUnsignedShort(IByteBuffer buffer, ref int remainingLength)
        {
            DecreaseRemainingLength(ref remainingLength, 2);
            return buffer.ReadUnsignedShort();
        }

        static string DecodeString(IByteBuffer buffer, ref int remainingLength) => DecodeString(buffer, ref remainingLength, 0, int.MaxValue);

        static string DecodeString(IByteBuffer buffer, ref int remainingLength, int minBytes) => DecodeString(buffer, ref remainingLength, minBytes, int.MaxValue);

        static string DecodeString(IByteBuffer buffer, ref int remainingLength, int minBytes, int maxBytes)
        {
            int size = DecodeUnsignedShort(buffer, ref remainingLength);

            if (size < minBytes)
            {
                ThrowHelper.ThrowDecoderException_StrIsShorterThanMinSize(minBytes, size);
            }
            if (size > maxBytes)
            {
                ThrowHelper.ThrowDecoderException_StrIsLongerThanMaxSize(maxBytes, size);
            }

            if (0u >= (uint)size)
            {
                return string.Empty;
            }

            DecreaseRemainingLength(ref remainingLength, size);

            string value = buffer.ToString(buffer.ReaderIndex, size, Encoding.UTF8);
            // todo: enforce string definition by MQTT spec
            buffer.SetReaderIndex(buffer.ReaderIndex + size);
            return value;
        }

        [MethodImpl(InlineMethod.Value)] // we don't care about the method being on exception's stack so it's OK to inline
        static void DecreaseRemainingLength(ref int remainingLength, int minExpectedLength)
        {
            if (remainingLength < minExpectedLength)
            {
                ValidateDecreaseRemainingLength(remainingLength, minExpectedLength);
            }
            remainingLength -= minExpectedLength;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void ValidateDecreaseRemainingLength(int remainingLength, int minExpectedLength)
        {
            throw GetDecoderException();
            DecoderException GetDecoderException()
            {
                return new DecoderException($"Current Remaining Length of {remainingLength} is smaller than expected {minExpectedLength}.");
            }
        }
    }
}