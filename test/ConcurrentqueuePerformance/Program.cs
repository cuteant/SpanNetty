using System;
using System.Collections.Concurrent;
using System.Threading;
using DotNetty.Common.Internal;

namespace ConcurrentqueuePerformance
{
    //For future reference, the following code can be used to test if the problem is still occuring.
    //Running the application should make the Main thread use 100% CPU and is very very slow.
    //The code has been tested on Mono 5.2-5.16 (the issue does not occur on earlier versions) and .NET Core 2.0 - .NET Core 3.0 preview

    class Program
    {
        private static ConcurrentQueue<int> queue = new ConcurrentQueue<int>();
        //private static CompatibleConcurrentQueue<int> queue = new CompatibleConcurrentQueue<int>();
        private static volatile bool exit = false;

        static void Main(string[] args)
        {
            Thread t = new Thread(AddEventsThread);
            t.Name = "AddEventsThread";
            t.Start();

            Thread.Sleep(5000); //wait for 5 seconds to make sure the AddEventsThread thread has started
            var queueCount = 0L;
            var totalElapsed = 0L;

            Console.WriteLine("Starting to hammer queue.Count.");

            for (int i = 0; i < 1000000; i++)
            {
                var start = DateTime.UtcNow.Ticks;
                queueCount = queue.Count;
                totalElapsed += DateTime.UtcNow.Ticks - start;
            }

            Console.Out.WriteLine("Total Elapsed: {0}, Queue Count: {1}", totalElapsed, queueCount);
            Console.ReadKey();
            exit = true;
        }

        public static void AddEventsThread()
        {
            while (!exit)
            {
                Console.WriteLine("Enqueing");
                for (int i = 0; i < 50000 && !exit; i++)
                {
                    queue.Enqueue(50);
                    Thread.Sleep(1);
                }
                Console.WriteLine("Dequeing");
                for (int i = 0; i < 50000 && !exit; i++)
                {
                    int result;
                    queue.TryDequeue(out result);
                    Thread.Sleep(1);
                }
            }
        }
    }
}
