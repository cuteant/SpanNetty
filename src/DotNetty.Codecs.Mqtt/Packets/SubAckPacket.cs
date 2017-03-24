// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
  using System.Collections.Generic;

  public sealed class SubAckPacket : PacketWithId
  {
    public override PacketType PacketType => PacketType.SUBACK;

#if NET_4_0_GREATER
    public IReadOnlyList<QualityOfService> ReturnCodes { get; set; }
#else
    public IList<QualityOfService> ReturnCodes { get; set; }
#endif

    public static SubAckPacket InResponseTo(SubscribePacket subscribePacket, QualityOfService maxQoS)
    {
      var subAckPacket = new SubAckPacket
      {
        PacketId = subscribePacket.PacketId
      };
      var subscriptionRequests = subscribePacket.Requests;
      var returnCodes = new QualityOfService[subscriptionRequests.Count];
      for (int i = 0; i < subscriptionRequests.Count; i++)
      {
        QualityOfService requestedQos = subscriptionRequests[i].QualityOfService;
        returnCodes[i] = requestedQos <= maxQoS ? requestedQos : maxQoS;
      }

      subAckPacket.ReturnCodes = returnCodes;

      return subAckPacket;
    }
  }
}