namespace Multimodal.Config
{
    public static class ServerConfig
    {
        public const string HttpBaseUrl = "http://localhost:8000";
        public const string WsBaseUrl = "ws://localhost:8000";

        private const string RealtimePrefix = "/api/realtime";

        /// <summary>Realtime API WebSocket URL</summary>
        public static string RealtimeWsUrl => $"{WsBaseUrl}{RealtimePrefix}";
    }
}
