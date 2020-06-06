namespace DotNetty.Handlers.Tls
{
    using System.Runtime.CompilerServices;

    internal static class TlsHandlerState
    {
        public const int Authenticating = 1;
        public const int Authenticated = 1 << 1;
        public const int FailedAuthentication = 1 << 2;
        public const int ReadRequestedBeforeAuthenticated = 1 << 3;
        public const int FlushedBeforeHandshake = 1 << 4;
        public const int AuthenticationStarted = Authenticating | Authenticated | FailedAuthentication;
        public const int AuthenticationCompleted = Authenticated | FailedAuthentication;
    }

    internal static class TlsHandlerStateExtensions
    {
        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static bool Has(this int value, int testValue) => (value & testValue) == testValue;

        [MethodImpl(InlineMethod.AggressiveOptimization)]
        public static bool HasAny(this int value, int testValue) => (value & testValue) != 0;
    }
}
