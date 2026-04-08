using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Multimodal.AIBridge
{
    public class RealtimeWebSocketClient : IDisposable
    {
        #region Message Types
        private static class MessageType
        {
            // Client -> Server
            public const string STREAM_START = "STREAM_START";
            public const string STREAM_AUDIO = "STREAM_AUDIO";
            public const string STREAM_STOP = "STREAM_STOP";

            // Server -> Client
            public const string STREAM_ACK = "STREAM_ACK";
            public const string SPEECH_STARTED = "SPEECH_STARTED";
            public const string TRANSCRIPT = "TRANSCRIPT";
            public const string TEXT_DELTA = "TEXT_DELTA";
            public const string AUDIO_DELTA = "AUDIO_DELTA";
            public const string AUDIO_DONE = "AUDIO_DONE";
            public const string RESPONSE_END = "RESPONSE_END";
            public const string STREAM_ERROR = "STREAM_ERROR";
        }
        #endregion

        #region Events
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action OnSpeechStarted;
        public event Action<string> OnTranscript;
        public event Action<string> OnTextDelta;
        public event Action<byte[]> OnAudioDelta;
        public event Action OnAudioDone;
        public event Action<string> OnResponseEnd;
        public event Action<string, string> OnError;
        #endregion

        #region Fields
        private Meta.Net.NativeWebSocket.WebSocket _webSocket;
        private readonly string _serverUrl;
        private string _sessionId;
        private bool _isConnected;
        private bool _isStreaming;
        private CancellationTokenSource _cts;
        private TaskCompletionSource<bool> _connectTcs;

        // 프레임 조각 재조립용 버퍼
        private readonly StringBuilder _messageBuffer = new StringBuilder();
        #endregion

        #region Constructor
        public RealtimeWebSocketClient(string serverUrl)
        {
            _serverUrl = serverUrl.TrimEnd('/');
        }
        #endregion

        #region Connection Management
        public async Task ConnectAsync()
        {
            if (_isConnected)
            {
                Debug.LogWarning("[Realtime] Already connected");
                return;
            }

            try
            {
                _cts = new CancellationTokenSource();
                _connectTcs = new TaskCompletionSource<bool>();

                _webSocket = new Meta.Net.NativeWebSocket.WebSocket($"{_serverUrl}/ws/realtime/v1");

                _webSocket.OnOpen += HandleOpen;
                _webSocket.OnMessage += HandleMessage;
                _webSocket.OnError += HandleError;
                _webSocket.OnClose += HandleClose;

                Debug.Log("[Realtime] Connecting to WebSocket...");

                _ = _webSocket.Connect();

                var timeoutTask = Task.Delay(10000);
                var completedTask = await Task.WhenAny(_connectTcs.Task, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    throw new Exception("Connection timeout - OnOpen not received");
                }

                Debug.Log("[Realtime] Connection established successfully");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Connection failed: {ex.Message}");
                OnError?.Invoke("CONNECTION_FAILED", ex.Message);
                throw;
            }
        }

        public async Task DisconnectAsync()
        {
            if (!_isConnected) return;

            try
            {
                if (_isStreaming)
                {
                    await StopStreamAsync();
                }

                _cts?.Cancel();

                if (_webSocket != null && _webSocket.State == Meta.Net.NativeWebSocket.WebSocketState.Open)
                {
                    await _webSocket.Close();
                }

                _isConnected = false;
                Debug.Log("[Realtime] Disconnected");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Disconnect error: {ex.Message}");
            }
        }
        #endregion

        #region Streaming Control
        public async Task StartStreamAsync(string sessionId, string language = "ko")
        {
            if (!_isConnected)
            {
                throw new InvalidOperationException("Not connected to WebSocket");
            }

            if (_isStreaming)
            {
                Debug.LogWarning("[Realtime] Stream already started");
                return;
            }

            _sessionId = sessionId;

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_START,
                ["session_id"] = sessionId,
                ["language"] = language
            };

            await SendJsonAsync(message);
            _isStreaming = true;

            Debug.Log($"[Realtime] Stream started: {sessionId}, language: {language}");
        }

        public async Task SendAudioChunkAsync(byte[] audioData)
        {
            if (!_isStreaming)
            {
                Debug.LogWarning("[Realtime] Stream not started");
                return;
            }

            var base64Audio = Convert.ToBase64String(audioData);

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_AUDIO,
                ["session_id"] = _sessionId,
                ["audio_data"] = base64Audio
            };

            await SendJsonAsync(message);
        }

        public async Task StopStreamAsync()
        {
            if (!_isStreaming) return;

            var message = new JObject
            {
                ["type"] = MessageType.STREAM_STOP,
                ["session_id"] = _sessionId
            };

            await SendJsonAsync(message);
            _isStreaming = false;

            Debug.Log($"[Realtime] Stream stopped: {_sessionId}");
        }
        #endregion

        #region Message Handling
        private void HandleOpen()
        {
            _isConnected = true;
            Debug.Log("[Realtime] WebSocket connected");
            _connectTcs?.TrySetResult(true);
            OnConnected?.Invoke();
        }

        private void HandleMessage(byte[] data, int offset, int length)
        {
            try
            {
                var fragment = Encoding.UTF8.GetString(data, offset, length);
                _messageBuffer.Append(fragment);

                // JSON 파싱 시도 — 실패하면 아직 조각이 더 와야 함
                string buffered = _messageBuffer.ToString();
                JObject message;
                try
                {
                    message = JObject.Parse(buffered);
                }
                catch (JsonReaderException)
                {
                    // 아직 불완전한 메시지 — 다음 조각 대기
                    return;
                }

                // 파싱 성공 — 버퍼 초기화
                _messageBuffer.Clear();

                var type = message["type"]?.ToString();
                Debug.Log($"[Realtime] Received: {type}");

                switch (type)
                {
                    case MessageType.STREAM_ACK:
                        break;

                    case MessageType.SPEECH_STARTED:
                        OnSpeechStarted?.Invoke();
                        break;

                    case MessageType.TRANSCRIPT:
                        var transcript = message["transcript"]?.ToString();
                        if (!string.IsNullOrEmpty(transcript))
                            OnTranscript?.Invoke(transcript);
                        break;

                    case MessageType.TEXT_DELTA:
                        var delta = message["delta"]?.ToString();
                        if (!string.IsNullOrEmpty(delta))
                            OnTextDelta?.Invoke(delta);
                        break;

                    case MessageType.AUDIO_DELTA:
                        var audioBase64 = message["delta"]?.ToString();
                        if (!string.IsNullOrEmpty(audioBase64))
                        {
                            byte[] audioBytes = Convert.FromBase64String(audioBase64);
                            OnAudioDelta?.Invoke(audioBytes);
                        }
                        break;

                    case MessageType.AUDIO_DONE:
                        OnAudioDone?.Invoke();
                        break;

                    case MessageType.RESPONSE_END:
                        var fullText = message["full_text"]?.ToString();
                        OnResponseEnd?.Invoke(fullText);
                        break;

                    case MessageType.STREAM_ERROR:
                        var errorCode = message["error_code"]?.ToString() ?? "UNKNOWN";
                        var errorMessage = message["error_message"]?.ToString() ?? "Unknown error";
                        OnError?.Invoke(errorCode, errorMessage);
                        break;

                    default:
                        Debug.LogWarning($"[Realtime] Unknown message type: {type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Realtime] Message handling error: {ex.Message}");
                _messageBuffer.Clear();
            }
        }

        private void HandleError(string error)
        {
            Debug.LogError($"[Realtime] WebSocket error: {error}");
            _connectTcs?.TrySetException(new Exception(error));
            OnError?.Invoke("WEBSOCKET_ERROR", error);
        }

        private void HandleClose(Meta.Net.NativeWebSocket.WebSocketCloseCode code)
        {
            _isConnected = false;
            _isStreaming = false;
            _messageBuffer.Clear();
            var reason = $"Code: {code}";
            Debug.Log($"[Realtime] WebSocket closed: {reason}");
            _connectTcs?.TrySetException(new Exception($"Connection closed: {reason}"));
            OnDisconnected?.Invoke(reason);
        }
        #endregion

        #region Helper Methods
        private async Task SendJsonAsync(JObject message)
        {
            if (_webSocket == null || _webSocket.State != Meta.Net.NativeWebSocket.WebSocketState.Open)
            {
                throw new InvalidOperationException("WebSocket not connected");
            }

            var json = message.ToString(Formatting.None);
            await _webSocket.SendText(json);
        }
        #endregion

        #region IDisposable
        public void Dispose()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _messageBuffer.Clear();
            _webSocket?.CancelConnection();
            _webSocket = null;
        }
        #endregion

        #region Properties
        public bool IsConnected => _isConnected;
        public bool IsStreaming => _isStreaming;
        public string SessionId => _sessionId;
        #endregion
    }
}
