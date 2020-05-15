namespace DotNetty.Transport.Channels
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DotNetty.Common;
    using DotNetty.Common.Utilities;

    public static class IChannelHandlerContextExtensions
    {
        public static Task WriteAndFlushManyAsync(this IChannelHandlerContext context, params object[] msgs) => WriteAndFlushManyAsync(context, messages: msgs);

        public static Task WriteAndFlushManyAsync(this IChannelHandlerContext context, ICollection<object> messages)
        {
            if (messages is null || 0u >= (uint)messages.Count) { return TaskUtil.Completed; }

            var taskList = ThreadLocalList<Task>.NewInstance();
            foreach (object m in messages)
            {
                taskList.Add(context.WriteAsync(m));
            }
            context.Flush();

            var writeCloseCompletion = Task.WhenAll(taskList);
            writeCloseCompletion.ContinueWith(s_returnAfterWriteAction, taskList, TaskContinuationOptions.ExecuteSynchronously);
            return writeCloseCompletion;
        }

        private static readonly Action<Task, object> s_returnAfterWriteAction = ReturnAfterWriteAction;
        private static void ReturnAfterWriteAction(Task t, object s) => ((ThreadLocalList<Task>)s).Return();
    }
}
