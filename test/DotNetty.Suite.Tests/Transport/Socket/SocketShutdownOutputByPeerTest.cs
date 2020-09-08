namespace DotNetty.Suite.Tests.Transport.Socket
{
    using System.Net;
    using System.Net.Sockets;
    using DotNetty.Transport.Channels.Sockets;
    using Xunit.Abstractions;

    public class SocketShutdownOutputByPeerTest : AbstractSocketShutdownOutputByPeerTest
    {
        public SocketShutdownOutputByPeerTest(ITestOutputHelper output)
            : base(output)
        {
        }

        protected override void ShutdownOutput(Socket s)
        {
            s.Shutdown(SocketShutdown.Send);
        }

        protected override void Connect(Socket s, EndPoint address)
        {
            s.Connect(address);
        }

        protected override void Close(Socket s)
        {
            SocketEx.SafeClose(s);
        }

        protected override void Write(Socket s, int data)
        {
            s.Send(System.BitConverter.GetBytes(data));
        }

        protected override Socket NewSocket()
        {
            return new Socket(SocketType.Stream, ProtocolType.Tcp);
        }
    }
}
