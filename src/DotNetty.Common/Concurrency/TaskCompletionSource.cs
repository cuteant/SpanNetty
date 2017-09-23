// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NET40
using System;
using System.Threading;
#endif
using System.Threading.Tasks;

namespace DotNetty.Common.Concurrency
{
  public sealed class TaskCompletionSource : TaskCompletionSource<int>
  {
    public static readonly TaskCompletionSource Void = CreateVoidTcs();

    public TaskCompletionSource(object state)
      : base(state)
    {
    }

    public TaskCompletionSource()
    {
    }

    public bool TryComplete()
    {
#if NET40
      // TaskCompletionSource TrySetResult SetResult twice
      //var result = this.TrySetResult(0);
      //SetResultAsync(this, 0);
      FakeSynchronizationContext.Execute(() => this.SetResult(0));
      return true;
#else
      return this.TrySetResult(0);
#endif
    }

    public void Complete()
    {
#if NET40
      // TaskCompletionSource TrySetResult SetResult twice
      //var result = this.TrySetResult(0);
      //SetResultAsync(this, 0);
      FakeSynchronizationContext.Execute(() => this.SetResult(0));
#else
      this.SetResult(0);
#endif
    }

    // todo: support cancellation token where used
    public bool SetUncancellable() => true;

    public override string ToString() => "TaskCompletionSource[status: " + this.Task.Status.ToString() + "]";

    static TaskCompletionSource CreateVoidTcs()
    {
      var tcs = new TaskCompletionSource();
      tcs.TryComplete();
      return tcs;
    }
  }

#if NET40
  /// <summary>FakeSynchronizationContext: http://stackoverflow.com/questions/21845495/synchronous-or-asynchronous-continuation-upon-taskcompletionsource-trysetresult </summary>
  public sealed class FakeSynchronizationContext : SynchronizationContext
  {
    private static readonly ThreadLocal<FakeSynchronizationContext> s_context =
        new ThreadLocal<FakeSynchronizationContext>(() => new FakeSynchronizationContext());

    private FakeSynchronizationContext() { }

    public static FakeSynchronizationContext Instance { get { return s_context.Value; } }

    public static void Execute(Action action)
    {
      var savedContext = SynchronizationContext.Current;
      SynchronizationContext.SetSynchronizationContext(FakeSynchronizationContext.Instance);
      try
      {
        action();
      }
      catch { }
      finally
      {
        SynchronizationContext.SetSynchronizationContext(savedContext);
      }
    }

    // SynchronizationContext methods

    public override SynchronizationContext CreateCopy()
    {
      return this;
    }

    public override void OperationStarted()
    {
      throw new NotImplementedException("OperationStarted");
    }

    public override void OperationCompleted()
    {
      throw new NotImplementedException("OperationCompleted");
    }

    public override void Post(SendOrPostCallback d, object state)
    {
      throw new NotImplementedException("Post");
    }

    public override void Send(SendOrPostCallback d, object state)
    {
      throw new NotImplementedException("Send");
    }
  }
#endif
}