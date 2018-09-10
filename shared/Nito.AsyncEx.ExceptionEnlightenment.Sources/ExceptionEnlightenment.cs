using System;
#if !NET40
using System.Runtime.CompilerServices;
#else
using System.Linq.Expressions;
using System.Reflection;
#endif

namespace System.Runtime.ExceptionServices
{
  internal static class ExceptionEnlightenment
  {
#if !NET40
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Exception PrepareForRethrow(Exception exception)
    {
      ExceptionDispatchInfo.Capture(exception).Throw();

      // The code cannot ever get here. We just return a value to work around a badly-designed API (ExceptionDispatchInfo.Throw):
      //  https://connect.microsoft.com/VisualStudio/feedback/details/689516/exceptiondispatchinfo-api-modifications (http://www.webcitation.org/6XQ7RoJmO)
      return exception;
    }
#else
    /// <summary>A delegate that will call <c>ExceptionDispatchInfo.Capture</c> followed by <c>ExceptionDispatchInfo.Throw</c>, 
    /// or <c>null</c> if the <c>ExceptionDispatchInfo</c> type does not exist.</summary>
    private static readonly Action<Exception> CaptureAndThrow;

    /// <summary>A delegate that will call <c>Exception.PrepForRemoting</c>, or <c>null</c> if the method does not exist. 
    /// This member is always <c>null</c> if <see cref="CaptureAndThrow"/> is non-<c>null</c>.</summary>
    private static readonly Action<Exception> PrepForRemoting;

    static ExceptionEnlightenment()
    {
      var exception = Expression.Parameter(typeof(Exception), "exception");
      var capture = ReflectionHelper.Call(ReflectionHelper.Type("System.Runtime.ExceptionServices.ExceptionDispatchInfo"), "Capture", exception);
      var @throw = ReflectionHelper.Call(capture, "Throw");
      CaptureAndThrow = ReflectionHelper.Compile<Action<Exception>>(@throw, exception);

      var prepForRemoting = ReflectionHelper.Call(exception, "PrepForRemoting", BindingFlags.Instance | BindingFlags.NonPublic);
      PrepForRemoting = ReflectionHelper.Compile<Action<Exception>>(prepForRemoting, exception);
    }

    internal static Exception PrepareForRethrow(Exception exception)
    {
      if (CaptureAndThrow != null)
      {
        CaptureAndThrow(exception);
      }
      else if (PrepForRemoting != null)
      {
        PrepForRemoting(exception);
      }
      else
      {
        TryAddStackTrace(exception);
      }

      return exception;
    }

    /// <summary>Attempts to add the original stack trace to the <see cref="Exception.Data"/> collection.</summary>
    /// <param name="exception">The exception. May not be <c>null</c>.</param>
    private static void TryAddStackTrace(Exception exception)
    {
      try
      {
        exception.Data.Add("Original stack trace", exception.StackTrace);
      }
      catch (ArgumentException)
      {
        // Vexing exception
      }
      catch (NotSupportedException)
      {
        // Vexing exception
      }
    }
#endif
  }
}
