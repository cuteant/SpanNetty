// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    public interface IHttp2SettingsFrame: IHttp2Frame
    {
        Http2Settings Settings { get; }
    }
}
