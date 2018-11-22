// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /// <summary>
    /// Signifies that the <a href="https://tools.ietf.org/html/rfc7540#section-3.5">connection preface</a> and
    /// the initial SETTINGS frame have been sent. The client sends the preface, and the server receives the preface.
    /// The client shouldn't write any data until this event has been processed.
    /// </summary>
    public sealed class Http2ConnectionPrefaceAndSettingsFrameWrittenEvent
    {
        public static readonly Http2ConnectionPrefaceAndSettingsFrameWrittenEvent Instance = new Http2ConnectionPrefaceAndSettingsFrameWrittenEvent();

        private Http2ConnectionPrefaceAndSettingsFrameWrittenEvent() { }
    }
}
