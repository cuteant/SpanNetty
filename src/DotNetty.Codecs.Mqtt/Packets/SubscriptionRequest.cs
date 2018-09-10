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

            this.TopicFilter = topicFilter;
            this.QualityOfService = qualityOfService;
        }

        public string TopicFilter { get; }

        public QualityOfService QualityOfService { get; }

        public bool Equals(SubscriptionRequest other)
        {
            return this.QualityOfService == other.QualityOfService
                && string.Equals(this.TopicFilter, other.TopicFilter, StringComparison.Ordinal);
        }

        public override string ToString()
        {
            return $"{this.GetType().Name}[TopicFilter={this.TopicFilter}, QualityOfService={this.QualityOfService}]";
        }
    }
}