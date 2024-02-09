// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Examples.Common
{
    using DotNetty.Common.Internal.Logging;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using NLog.Extensions.Logging;
    using System;

    public static class ExampleHelper
    {
        static ExampleHelper()
        {
            Configuration = new ConfigurationBuilder()
                .SetBasePath(ProcessDirectory)
                .AddJsonFile("appsettings.json")
                .Build();
        }

        public static string ProcessDirectory
        {
            get
            {
                return AppDomain.CurrentDomain.BaseDirectory;
            }
        }

        public static IConfigurationRoot Configuration { get; }

        public static void SetConsoleLogger()
        {
            var f = new LoggerFactory();
            f.AddNLog();
            InternalLoggerFactory.DefaultFactory = f;
        }
    }
}