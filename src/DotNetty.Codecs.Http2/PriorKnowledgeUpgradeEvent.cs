// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// User event that is fired to notify about HTTP/2 protocol is started.
    /// </summary>
    public sealed class PriorKnowledgeUpgradeEvent
    {
        public static readonly PriorKnowledgeUpgradeEvent Instance = new PriorKnowledgeUpgradeEvent();

        private PriorKnowledgeUpgradeEvent() { }
    }
}
