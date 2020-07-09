namespace DotNetty.Common.Concurrency
{
    using System.Globalization;
    using System.Threading;
    using DotNetty.Common.Utilities;
    using Thread = XThread;

    /// <summary>
    /// A <see cref="IThreadFactory"/> implementation with a simple naming rule.
    /// </summary>
    public class DefaultThreadFactory<TPool> : DefaultThreadFactory
    {
        private static readonly string s_poolName;

        public static readonly DefaultThreadFactory<TPool> Instance;

        static DefaultThreadFactory()
        {
            s_poolName = ToPoolName();
            Instance = new DefaultThreadFactory<TPool>();
        }

        private DefaultThreadFactory()
            : base(s_poolName)
        {
        }

        private static string ToPoolName()
        {
            string poolName = StringUtil.SimpleClassName<TPool>();
            if (poolName.Length == 1)
            {
                return poolName.ToLowerInvariant();
            }
            if (char.IsUpper(poolName[0]) && char.IsLower(poolName[1]))
            {
                return char.ToLowerInvariant(poolName[0]) + poolName.Substring(1);
            }
            else
            {
                return poolName;
            }
        }
    }

    /// <summary>
    /// A <see cref="IThreadFactory"/> implementation with a simple naming rule.
    /// </summary>
    public class DefaultThreadFactory : IThreadFactory
    {
        private const string c_poolName = "default";

        private static int s_poolId;

        private readonly string _threadPrefix;
        private int v_nextId;

        public DefaultThreadFactory()
            : this(c_poolName)
        {
        }

        public DefaultThreadFactory(string poolName)
        {
            if (string.IsNullOrEmpty(poolName)) { ThrowHelper.ThrowArgumentNullException(ExceptionArgument.poolName); }

            _threadPrefix = poolName + '-' + Interlocked.Increment(ref s_poolId).ToString(CultureInfo.InvariantCulture) + '-';
        }

        public Thread NewThread(XParameterizedThreadStart r)
        {
            var threadId = Interlocked.Increment(ref v_nextId);
            return NewThread(r, _threadPrefix + threadId.ToString(CultureInfo.InvariantCulture));
        }

        public Thread NewThread(XParameterizedThreadStart r, string threadName)
        {
            return new Thread(r)
            {
                Name = threadName
            };
        }
    }
}
