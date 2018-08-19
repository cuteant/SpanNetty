// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt
{
    using System;
    using System.Collections.Generic;
    using System.Runtime.CompilerServices;
    using System.Text;
    using CuteAnt.Collections;
    using DotNetty.Buffers;
    using DotNetty.Codecs.Mqtt.Packets;
    using DotNetty.Transport.Channels;

    partial class MqttDecoder
    {
        enum TopicFilterErrorStatus
        {
            None,
            MQTT_471_2,
            MQTT_471_3,
            MQTT_473_1,
        }

        private static readonly CachedReadConcurrentDictionary<string, TopicFilterErrorStatus> s_topicFilterErrCache =
            new CachedReadConcurrentDictionary<string, TopicFilterErrorStatus>(StringComparer.Ordinal);
        private static readonly Func<string, TopicFilterErrorStatus> s_getTopicFilterErrorStatusFunc = GetTopicFilterErrorStatus;

        static void ValidateTopicFilter(string topicFilter)
        {
            var errorStatus = s_topicFilterErrCache.GetOrAdd(topicFilter, s_getTopicFilterErrorStatusFunc);
            switch (errorStatus)
            {
                case TopicFilterErrorStatus.MQTT_471_2:
                    ThrowHelper.ThrowDecoderException_MQTT_471_2(topicFilter);
                    break;
                case TopicFilterErrorStatus.MQTT_471_3:
                    ThrowHelper.ThrowDecoderException_MQTT_471_3(topicFilter);
                    break;
                case TopicFilterErrorStatus.MQTT_473_1:
                    ThrowHelper.ThrowDecoderException_MQTT_473_1();
                    break;
                case TopicFilterErrorStatus.None:
                default:
                    break;
            }
        }

        static TopicFilterErrorStatus GetTopicFilterErrorStatus(string topicFilter)
        {
            int length = topicFilter.Length;
            if (length == 0)
            {
                return TopicFilterErrorStatus.MQTT_473_1;
            }

            for (int i = 0; i < length; i++)
            {
                char c = topicFilter[i];
                switch (c)
                {
                    case '+':
                        if ((i > 0 && topicFilter[i - 1] != '/') || (i < length - 1 && topicFilter[i + 1] != '/'))
                        {
                            return TopicFilterErrorStatus.MQTT_471_3;
                        }
                        break;
                    case '#':
                        if (i < length - 1 || (i > 0 && topicFilter[i - 1] != '/'))
                        {
                            return TopicFilterErrorStatus.MQTT_471_2;
                        }
                        break;
                }
            }

            return TopicFilterErrorStatus.None;
        }
    }
}