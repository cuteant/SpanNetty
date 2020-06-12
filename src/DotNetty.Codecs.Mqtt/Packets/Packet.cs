// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    public abstract class Packet
    {
        public abstract PacketType PacketType { get; }

        public virtual bool Duplicate => false;

        public virtual QualityOfService QualityOfService => QualityOfService.AtMostOnce;

        public virtual bool RetainRequested => false;

        public override string ToString()
        {
            return $"{GetType().Name}[Type={PacketType}, QualityOfService={QualityOfService}, Duplicate={Duplicate}, Retain={RetainRequested}]";
        }
    }
}