using System;
using UnityEngine;

namespace Multimodal.Voice
{
    public class MicrophoneRecorder
    {
        private AudioClip _recordingClip;
        private string _deviceName;
        private int _sampleRate;
        private int _lastReadPosition;
        private bool _isRecording;

        private const int RecordingLength = 10;
        private const int Channels = 1;

        public event Action OnRecordingStarted;
        public event Action OnRecordingStopped;
        public event Action<string> OnError;

        public bool StartRecording(int sampleRate = 24000, string deviceName = null)
        {
            if (_isRecording)
            {
                Debug.LogWarning("[MicRecorder] Already recording");
                return false;
            }

            try
            {
                if (Microphone.devices.Length == 0)
                {
                    var errorMsg = "No microphone devices found";
                    Debug.LogError($"[MicRecorder] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return false;
                }

                _deviceName = deviceName ?? (Microphone.devices.Length > 0 ? Microphone.devices[0] : null);
                _sampleRate = sampleRate;
                _lastReadPosition = 0;

                _recordingClip = Microphone.Start(_deviceName, loop: true, RecordingLength, _sampleRate);

                if (_recordingClip == null)
                {
                    var errorMsg = "Failed to create AudioClip";
                    Debug.LogError($"[MicRecorder] {errorMsg}");
                    OnError?.Invoke(errorMsg);
                    return false;
                }

                _isRecording = true;
                Debug.Log($"[MicRecorder] Recording started: {_deviceName}, {_sampleRate}Hz");
                OnRecordingStarted?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Start recording failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return false;
            }
        }

        public void StopRecording()
        {
            if (!_isRecording) return;

            try
            {
                if (Microphone.IsRecording(_deviceName))
                {
                    Microphone.End(_deviceName);
                }

                _isRecording = false;
                _recordingClip = null;
                _lastReadPosition = 0;

                Debug.Log("[MicRecorder] Recording stopped");
                OnRecordingStopped?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Stop recording failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
            }
        }

        public float[] GetLatestAudioChunk(int chunkSizeInSamples = 0)
        {
            if (!_isRecording || _recordingClip == null) return null;

            try
            {
                int currentPosition = Microphone.GetPosition(_deviceName);
                if (currentPosition < 0) return null;

                int samplesToRead;
                if (chunkSizeInSamples > 0)
                {
                    samplesToRead = chunkSizeInSamples;
                }
                else
                {
                    if (currentPosition == _lastReadPosition) return null;
                    samplesToRead = currentPosition > _lastReadPosition
                        ? currentPosition - _lastReadPosition
                        : (_recordingClip.samples - _lastReadPosition) + currentPosition;
                }

                samplesToRead = Mathf.Min(samplesToRead, _recordingClip.samples);
                if (samplesToRead <= 0) return null;

                float[] samples = new float[samplesToRead];
                _recordingClip.GetData(samples, _lastReadPosition);
                _lastReadPosition = currentPosition % _recordingClip.samples;
                return samples;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MicRecorder] Get audio chunk failed: {ex.Message}");
                OnError?.Invoke(ex.Message);
                return null;
            }
        }

        public byte[] GetLatestAudioChunkAsPCM16(int chunkSizeInSamples = 0)
        {
            var samples = GetLatestAudioChunk(chunkSizeInSamples);
            if (samples == null || samples.Length == 0) return null;
            return ConvertToPCM16(samples);
        }

        public static byte[] ConvertToPCM16(float[] samples)
        {
            if (samples == null || samples.Length == 0) return Array.Empty<byte>();

            byte[] pcm16 = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1.0f, 1.0f);
                short sample16 = (short)(clamped * short.MaxValue);
                pcm16[i * 2] = (byte)(sample16 & 0xFF);
                pcm16[i * 2 + 1] = (byte)((sample16 >> 8) & 0xFF);
            }
            return pcm16;
        }

        public int TimeToSamples(float seconds)
        {
            return Mathf.RoundToInt(seconds * _sampleRate);
        }

        public bool IsRecording => _isRecording;
        public int SampleRate => _sampleRate;
    }
}
