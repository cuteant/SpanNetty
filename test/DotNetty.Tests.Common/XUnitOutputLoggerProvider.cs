#if !NET40
namespace DotNetty.Tests.Common
{
#if NET40
  using CuteAnt.Extensions.Logging;
#else
  using Microsoft.Extensions.Logging;
#endif
  using Xunit.Abstractions;

  sealed class XUnitOutputLoggerProvider : ILoggerProvider
  {
    readonly ITestOutputHelper output;

    public XUnitOutputLoggerProvider(ITestOutputHelper output)
    {
      this.output = output;
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName) => new XUnitOutputLogger(categoryName, this.output);
  }
}
#endif