// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2SettingsFrame"/> implementation.
    /// </summary>
    public class DefaultHttp2SettingsFrame : IHttp2SettingsFrame
    {
        private readonly Http2Settings settings;

        public DefaultHttp2SettingsFrame(Http2Settings settings)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }
            this.settings = settings;
        }

        public Http2Settings Settings => this.settings;

        public string Name => "SETTINGS";

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(settings=" + this.settings + ')';
        }
    }
}
