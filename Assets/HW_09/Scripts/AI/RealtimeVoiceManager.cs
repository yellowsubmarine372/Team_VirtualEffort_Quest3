using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using Multimodal.AIBridge;
using Multimodal.Config;

namespace Multimodal.Voice
{
    public class RealtimeVoiceManager : MonoBehaviour
    {
        #region Inspector Fields
        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 24000;
        [SerializeField] private float chunkIntervalSeconds = 0.2f;

        [Header("Debug")]
        [SerializeField] private bool enableDebugLogs = true;
        #endregion

        #region Events
        public event Action OnConnected;
        public event Action<string> OnDisconnected;
        public event Action OnSpeechDetected;
        public event Action<string> OnTranscript;
        public event Action<string> OnStreamingText;
        public event Action<string> OnAIResponse;
        public event Action<byte[]> OnAudioDelta;
        public event Action<string, string> OnError;
        #endregion

        #region Private Fields
        private RealtimeWebSocketClient _realtimeClient;
        private MicrophoneRecorder _micRecorder;

        private bool _isVoiceActive;
        private string _currentSessionId;
        private Coroutine _audioStreamingCoroutine;
        private string _currentResponse = "";
        #endregion

        #region Unity Lifecycle
        private void Awake()
        {
            _realtimeClient = new RealtimeWebSocketClient(ServerConfig.RealtimeWsUrl);
            _micRecorder = new MicrophoneRecorder();

            _realtimeClient.OnConnected += HandleConnected;
            _realtimeClient.OnDisconnected += HandleDisconnected;
            _realtimeClient.OnSpeechStarted += HandleSpeechStarted;
            _realtimeClient.OnTranscript += HandleTranscript;
            _realtimeClient.OnTextDelta += HandleTextDelta;
            _realtimeClient.OnResponseEnd += HandleResponseEnd;
            _realtimeClient.OnAudioDelta += HandleAudioDelta;
            _realtimeClient.OnError += HandleError;

            _micRecorder.OnError += (msg) => HandleError("MIC_ERROR", msg);

            DebugLog("RealtimeVoiceManager initialized");
        }

        private void OnDestroy()
        {
            StopVoice();
            _realtimeClient?.Dispose();
        }
        #endregion

        #region Public API
        public async Task StartVoice(string language = "ko")
        {
            if (_isVoiceActive)
            {
                Debug.LogWarning("[RealtimeVoiceManager] Voice already active");
                return;
            }

            try
            {
                DebugLog("Starting voice...");

                if (!_realtimeClient.IsConnected)
                {
                    await _realtimeClient.ConnectAsync();
                }

                _currentSessionId = Guid.NewGuid().ToString();
                await _realtimeClient.StartStreamAsync(_currentSessionId, language);

                if (!_micRecorder.StartRecording(sampleRate))
                {
                    throw new Exception("Failed to start microphone");
                }

                _isVoiceActive = true;
                _currentResponse = "";
                _audioStreamingCoroutine = StartCoroutine(StreamAudioCoroutine());

                DebugLog($"Voice started: session={_currentSessionId}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Start voice failed: {ex.Message}");
                OnError?.Invoke("START_FAILED", ex.Message);
                StopVoice();
                throw;
            }
        }

        public void StopVoice()
        {
            if (!_isVoiceActive) return;

            try
            {
                DebugLog("Stopping voice...");

                if (_audioStreamingCoroutine != null)
                {
                    StopCoroutine(_audioStreamingCoroutine);
                    _audioStreamingCoroutine = null;
                }

                _micRecorder.StopRecording();

                if (_realtimeClient.IsStreaming)
                {
                    _ = _realtimeClient.StopStreamAsync();
                }

                if (_realtimeClient.IsConnected)
                {
                    _ = _realtimeClient.DisconnectAsync();
                }

                _isVoiceActive = false;
                _currentSessionId = null;

                DebugLog("Voice stopped");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Stop voice failed: {ex.Message}");
            }
        }
        #endregion

        #region Audio Streaming
        private IEnumerator StreamAudioCoroutine()
        {
            var waitInterval = new WaitForSeconds(chunkIntervalSeconds);
            int chunkSize = _micRecorder.TimeToSamples(chunkIntervalSeconds);

            while (_isVoiceActive)
            {
                byte[] audioChunk = _micRecorder.GetLatestAudioChunkAsPCM16(chunkSize);
                if (audioChunk != null && audioChunk.Length > 0)
                {
                    _ = SendAudioChunkAsync(audioChunk);
                }
                yield return waitInterval;
            }
        }

        private async Task SendAudioChunkAsync(byte[] audioData)
        {
            try
            {
                await _realtimeClient.SendAudioChunkAsync(audioData);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[RealtimeVoiceManager] Send audio chunk failed: {ex.Message}");
            }
        }
        #endregion

        #region Event Handlers
        private void HandleConnected()
        {
            DebugLog("WebSocket connected");
            OnConnected?.Invoke();
        }

        private void HandleDisconnected(string reason)
        {
            DebugLog($"WebSocket disconnected: {reason}");
            _isVoiceActive = false;
            OnDisconnected?.Invoke(reason);
        }

        private void HandleSpeechStarted()
        {
            DebugLog("Speech detected by OpenAI VAD");
            OnSpeechDetected?.Invoke();
        }

        private void HandleTranscript(string transcript)
        {
            DebugLog($"Transcript: {transcript}");
            OnTranscript?.Invoke(transcript);
        }

        private void HandleTextDelta(string delta)
        {
            _currentResponse += delta;
            OnStreamingText?.Invoke(delta);
        }

        private void HandleResponseEnd(string fullText)
        {
            DebugLog($"Response complete: {fullText}");
            OnAIResponse?.Invoke(fullText);
            _currentResponse = "";
        }

        private void HandleAudioDelta(byte[] audioData)
        {
            OnAudioDelta?.Invoke(audioData);
        }

        private void HandleError(string errorCode, string errorMessage)
        {
            Debug.LogError($"[RealtimeVoiceManager] Error: {errorCode} - {errorMessage}");
            OnError?.Invoke(errorCode, errorMessage);
        }
        #endregion

        #region Debug
        private void DebugLog(string message)
        {
            if (enableDebugLogs)
            {
                Debug.Log($"[RealtimeVoiceManager] {message}");
            }
        }
        #endregion

        #region Properties
        public bool IsVoiceActive => _isVoiceActive;
        public bool IsConnected => _realtimeClient?.IsConnected ?? false;
        public string CurrentSessionId => _currentSessionId;
        public string CurrentResponse => _currentResponse;
        public int SampleRate => sampleRate;
        #endregion
    }
}
