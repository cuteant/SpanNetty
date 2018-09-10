using System;
using System.Net.Sockets;

namespace DotNetty.Transport.Channels.Sockets
{
    public static class SocketEx
    {
        internal static Socket CreateSocket()
        {
#if NET40
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
#else
            // .Net45+，默认为AddressFamily.InterNetworkV6，并设置 DualMode 为 true，双线绑定
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.EnableFastpath();
#endif
            return socket;
        }
        internal static Socket CreateSocket(AddressFamily addressFamily)
        {
            var socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.EnableFastpath();
            return socket;
        }

        internal static void SafeClose(this Socket socket)
        {
            if (socket == null)
            {
                return;
            }

            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (ObjectDisposedException)
            {
                // Socket is already closed -- we're done here
                return;
            }
            catch (Exception)
            {
                // Ignore
            }

            try
            {
                if (socket.Connected)
                    socket.Disconnect(false);
                else
                    socket.Close();
            }
            catch (Exception)
            {
                // Ignore
            }

            try
            {
                socket.Dispose();
            }
            catch (Exception)
            {
                // Ignore
            }
        }

        /// <summary>Enables TCP Loopback Fast Path on a socket.
        /// See https://blogs.technet.microsoft.com/wincat/2012/12/05/fast-tcp-loopback-performance-and-low-latency-with-windows-server-2012-tcp-loopback-fast-path/
        /// for more information.</summary>
        /// <param name="socket">The socket for which FastPath should be enabled.</param>
        /// <remarks>Code take from Orleans(See https://github.com/dotnet/orleans/blob/master/src/Orleans.Core/Messaging/SocketExtensions.cs). </remarks>
        internal static void EnableFastpath(this Socket socket)
        {
#if NET40
            // nothing to do
#else
            if (!PlatformApis.IsWindows) { return; }

            const int SIO_LOOPBACK_FAST_PATH = -1744830448;
            var optionInValue = BitConverter.GetBytes(1);
            try
            {
                socket.IOControl(SIO_LOOPBACK_FAST_PATH, optionInValue, null);
            }
            catch
            {
                // If the operating system version on this machine did
                // not support SIO_LOOPBACK_FAST_PATH (i.e. version
                // prior to Windows 8 / Windows Server 2012), handle the exception
            }
#endif
        }

        public static bool IsSocketAbortError(this SocketError errorCode)
        {
            switch (errorCode)
            {
                case SocketError.OperationAborted:
                case SocketError.InvalidArgument:
                case SocketError.Interrupted:

                case SocketError.Shutdown:
                    return true;
                default:
                    return false;
            }
        }

        public static bool IsSocketResetError(this SocketError errorCode)
        {
            switch (errorCode)
            {
                case SocketError.ConnectionAborted:
                case SocketError.ConnectionReset:
                case SocketError.ConnectionRefused:

                case SocketError.OperationAborted:
                case SocketError.InvalidArgument:
                case SocketError.Interrupted:

                case SocketError.Shutdown:
                case SocketError.TimedOut:
                    return true;
                default:
                    return false;
            }
        }
    }
}
