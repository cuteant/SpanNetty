namespace DotNetty.Common.Concurrency
{
    using Thread = XThread;

    public interface IThreadFactory
    {
        Thread NewThread(XParameterizedThreadStart r);

        Thread NewThread(XParameterizedThreadStart r, string threadName);
    }
}
