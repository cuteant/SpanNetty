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
    using System;
    using DotNetty.Common.Internal;

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
        private static readonly Func<string, TopicFilterErrorStatus> s_getTopicFilterErrorStatusFunc = tf => GetTopicFilterErrorStatus(tf);

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
            if (0u >= (uint)length)
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