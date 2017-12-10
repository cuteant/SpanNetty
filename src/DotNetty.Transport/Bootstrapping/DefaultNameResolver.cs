// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Bootstrapping
{
    using System.Net;
    using System.Threading.Tasks;

    public class DefaultNameResolver : INameResolver
    {
        public bool IsResolved(EndPoint address) => !(address is DnsEndPoint);

        public async Task<EndPoint> ResolveAsync(EndPoint address)
        {
            if (address is DnsEndPoint asDns)
            {
#if NET40
                IPHostEntry resolved = Dns.GetHostEntry(asDns.Host);
                await DotNetty.Common.Utilities.TaskUtil.Completed;
#else
                IPHostEntry resolved = await Dns.GetHostEntryAsync(asDns.Host);
#endif
                return new IPEndPoint(resolved.AddressList[0], asDns.Port);
            }
            else
            {
                return address;
            }
        }
    }
}