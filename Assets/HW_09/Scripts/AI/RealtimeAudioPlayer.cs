using System;
using System.Collections.Generic;
using UnityEngine;

namespace Multimodal.Voice
{
    /// <summary>
    /// 서버에서 받은 PCM16 오디오 청크를 실시간 재생
    ///
    /// 사용법:
    /// 1. AudioSource가 있는 GameObject에 붙이기
    /// 2. RealtimeVoiceManager.OnAudioDelta += audioPlayer.EnqueueAudio;
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class RealtimeAudioPlayer : MonoBehaviour
    {
        [Header("Audio Settings")]
        [SerializeField] private int sampleRate = 24000;

        private AudioSource _audioSource;
        private readonly Queue<float> _pcmBuffer = new Queue<float>();
        private readonly object _bufferLock = new object();
        private AudioClip _streamClip;
        private int _playbackPosition;
        private bool _isPlaying;

        // 재생 시작 전 최소 버퍼 (샘플 수) - 끊김 방지
        private const int MinBufferSamples = 4800; // 200ms at 24kHz
        private const int ClipLengthSeconds = 60;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;

            _streamClip = AudioClip.Create(
                "RealtimeStream",
                sampleRate * ClipLengthSeconds,
                1,
                sampleRate,
                false
            );
            _audioSource.clip = _streamClip;
        }

        /// <summary>
        /// PCM16 바이트 배열을 버퍼에 추가 (서버에서 받은 AUDIO_DELTA)
        /// </summary>
        public void EnqueueAudio(byte[] pcm16Data)
        {
            if (pcm16Data == null || pcm16Data.Length < 2) return;

            lock (_bufferLock)
            {
                for (int i = 0; i < pcm16Data.Length - 1; i += 2)
                {
                    short sample = (short)(pcm16Data[i] | (pcm16Data[i + 1] << 8));
                    _pcmBuffer.Enqueue(sample / (float)short.MaxValue);
                }
            }
        }

        private void Update()
        {
            int buffered;
            lock (_bufferLock)
            {
                buffered = _pcmBuffer.Count;
            }

            if (!_isPlaying && buffered >= MinBufferSamples)
            {
                FlushBufferToClip();
                _audioSource.Play();
                _isPlaying = true;
            }
            else if (_isPlaying && buffered > 0)
            {
                FlushBufferToClip();
            }
            else if (_isPlaying && buffered == 0)
            {
                // 버퍼 소진 - 재생 위치가 쓴 위치를 넘어갔으면 정지
                if (_audioSource.timeSamples >= _playbackPosition || !_audioSource.isPlaying)
                {
                    _audioSource.Stop();
                    _isPlaying = false;
                    _playbackPosition = 0;
                    _audioSource.timeSamples = 0;
                }
            }
        }

        private void FlushBufferToClip()
        {
            float[] samples;
            lock (_bufferLock)
            {
                if (_pcmBuffer.Count == 0) return;
                samples = new float[_pcmBuffer.Count];
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = _pcmBuffer.Dequeue();
                }
            }

            int clipLength = _streamClip.samples;

            // 순환 버퍼가 넘치면 리셋
            if (_playbackPosition + samples.Length >= clipLength)
            {
                _playbackPosition = 0;
                _audioSource.timeSamples = 0;
            }

            _streamClip.SetData(samples, _playbackPosition);
            _playbackPosition += samples.Length;
        }

        /// <summary>
        /// 재생 중지 및 버퍼 초기화
        /// </summary>
        public void Clear()
        {
            _audioSource.Stop();
            _isPlaying = false;
            _playbackPosition = 0;

            lock (_bufferLock)
            {
                _pcmBuffer.Clear();
            }
        }

        /// <summary>
        /// 오디오가 재생 중이거나 버퍼에 데이터가 남아있으면 true
        /// </summary>
        public bool IsPlaying
        {
            get
            {
                if (_isPlaying) return true;
                lock (_bufferLock)
                {
                    return _pcmBuffer.Count > 0;
                }
            }
        }
    }
}
