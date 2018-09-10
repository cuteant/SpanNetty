// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Tls
{
    using System;
    using System.Threading.Tasks;

    public sealed class ServerTlsSniSettings
    {
        public ServerTlsSniSettings(Func<string, Task<ServerTlsSettings>> serverTlsSettingMap, string defaultServerHostName = null)
        {
            if (null == serverTlsSettingMap) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.serverTlsSettingMap); }
            this.ServerTlsSettingMap = serverTlsSettingMap;
            this.DefaultServerHostName = defaultServerHostName;
        }

        public Func<string, Task<ServerTlsSettings>> ServerTlsSettingMap { get; }

        public string DefaultServerHostName { get; } 
    }
}