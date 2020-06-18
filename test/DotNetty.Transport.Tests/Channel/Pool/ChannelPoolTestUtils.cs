namespace DotNetty.Transport.Tests.Channel.Pool
{
    using System;
    using DotNetty.Common.Utilities;

    public sealed class ChannelPoolTestUtils
    {
        static readonly string LOCAL_ADDR_ID = "test.id";

        [ThreadStatic]
        static Random s_random;

        private static Random ThreadLocalRandom => s_random ??= new Random();

        public static string GetLocalAddrId()
        {
            return LOCAL_ADDR_ID + ThreadLocalRandom.NextLong();
        }
    }
}
