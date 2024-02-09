// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Examples.Common;

namespace Factorial.Client
{
    public class ClientSettings : Examples.Common.ClientSettings
    {
        public static int Count => int.Parse(ExampleHelper.Configuration["count"]);
    }
}
