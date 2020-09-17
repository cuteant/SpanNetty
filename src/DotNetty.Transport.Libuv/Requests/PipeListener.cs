using System;
using System.Collections.Generic;
using System.Text;
using DotNetty.Transport.Libuv.Handles;
using DotNetty.Transport.Libuv.Native;

namespace DotNetty.Transport.Libuv.Requests
{
    internal sealed class PipeListener : IDisposable
    {
        private readonly List<Pipe> _pipes;
        private readonly WindowsApi _windowsApi;

        public PipeListener()
        {
            _pipes = new List<Pipe>();
            _windowsApi = new WindowsApi();
        }



        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
