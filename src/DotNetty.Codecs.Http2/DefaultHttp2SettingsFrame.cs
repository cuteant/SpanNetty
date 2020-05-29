// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Common.Utilities;

    /// <summary>
    /// The default <see cref="IHttp2SettingsFrame"/> implementation.
    /// </summary>
    public class DefaultHttp2SettingsFrame : IHttp2SettingsFrame, IEquatable<DefaultHttp2SettingsFrame>
    {
        private readonly Http2Settings _settings;

        public DefaultHttp2SettingsFrame(Http2Settings settings)
        {
            if (settings is null) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.settings); }
            _settings = settings;
        }

        public Http2Settings Settings => _settings;

        public string Name => "SETTINGS";

        public override bool Equals(object obj)
        {
            return Equals(obj as DefaultHttp2SettingsFrame);
        }

        public bool Equals(DefaultHttp2SettingsFrame other)
        {
            return ReferenceEquals(this, other);
        }

        public override int GetHashCode() => _settings.GetHashCode();

        public override string ToString()
        {
            return StringUtil.SimpleClassName(this) + "(settings=" + _settings + ')';
        }
    }
}
