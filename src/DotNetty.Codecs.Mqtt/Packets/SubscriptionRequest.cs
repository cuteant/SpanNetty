// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Mqtt.Packets
{
    using System;

    public class SubscriptionRequest : IEquatable<SubscriptionRequest>
    {
        public SubscriptionRequest(string topicFilter, QualityOfService qualityOfService)
        {
            if (string.IsNullOrEmpty(topicFilter)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.topicFilter); }

            TopicFilter = topicFilter;
            QualityOfService = qualityOfService;
        }

        public string TopicFilter { get; }

        public QualityOfService QualityOfService { get; }

        public bool Equals(SubscriptionRequest other)
        {
            return QualityOfService == other.QualityOfService
                && string.Equals(TopicFilter, other.TopicFilter
#if NETCOREAPP_3_0_GREATER || NETSTANDARD_2_0_GREATER
                    );
#else
                    , StringComparison.Ordinal);
#endif
        }

        public override string ToString()
        {
            return $"{GetType().Name}[TopicFilter={TopicFilter}, QualityOfService={QualityOfService}]";
        }
    }
}