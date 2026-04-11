using System;
using System.Collections.Concurrent;
using UnityEngine;
using TMPro;
using Multimodal.Voice;

/// <summary>
/// UI 버튼 없이 컨트롤러 트리거로 마이크 켜/끄기
/// - 오른쪽 인덱스 트리거: 마이크 토글
/// - Canvas는 텍스트 표시만
/// </summary>
public class AIChatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RealtimeVoiceManager voiceManager;
    [SerializeField] private RealtimeAudioPlayer audioPlayer;

    [Header("UI (비워두면 해당 UI 무시)")]
    [SerializeField] private TMP_Text userText;
    [SerializeField] private TMP_Text aiText;
    [SerializeField] private TMP_Text statusText;

    private bool _isMicActive;
    private bool _isProcessing; // 연결/해제 중 중복 방지

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    private void Start()
    {
        voiceManager.OnConnected += () => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = "연결됨 - 말하세요"; statusText.color = Color.green; }
        });

        voiceManager.OnDisconnected += (reason) => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = "연결 끊김"; statusText.color = Color.red; }
            _isMicActive = false;
        });

        voiceManager.OnSpeechDetected += () => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = "듣는 중..."; statusText.color = Color.yellow; }
            if (aiText != null) aiText.text = "";
        });

        voiceManager.OnTranscript += (transcript) => Enqueue(() =>
        {
            if (userText != null) userText.text = transcript;
            if (statusText != null) { statusText.text = "응답 중..."; statusText.color = new Color(1f, 0.6f, 0f); }
        });

        voiceManager.OnStreamingText += (delta) => Enqueue(() =>
        {
            if (aiText != null) aiText.text += delta;
        });

        voiceManager.OnAIResponse += (fullText) => Enqueue(() =>
        {
            if (aiText != null) aiText.text = fullText;
            if (statusText != null) { statusText.text = "말하세요"; statusText.color = Color.green; }
        });

        voiceManager.OnAudioDelta += (audioData) => Enqueue(() =>
        {
            if (audioPlayer != null) audioPlayer.EnqueueAudio(audioData);
        });

        voiceManager.OnError += (code, msg) => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = $"오류: {msg}"; statusText.color = Color.red; }
        });

        if (userText != null) userText.text = "";
        if (aiText != null) aiText.text = "";
        if (statusText != null) { statusText.text = "트리거를 눌러 시작"; statusText.color = Color.gray; }
    }

    private void Update()
    {
        // 메인 스레드 큐 처리
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }

        // A 버튼으로 마이크 토글
        if (OVRInput.GetDown(OVRInput.RawButton.A) && !_isProcessing)
        {
            ToggleMic();
        }
    }

    private async void ToggleMic()
    {
        _isProcessing = true;

        if (!_isMicActive)
        {
            try
            {
                if (statusText != null) statusText.text = "연결 중...";
                await voiceManager.StartVoice("ko");
                _isMicActive = true;
            }
            catch (Exception ex)
            {
                if (statusText != null) { statusText.text = $"시작 실패: {ex.Message}"; statusText.color = Color.red; }
            }
        }
        else
        {
            voiceManager.StopVoice();
            if (audioPlayer != null) audioPlayer.Clear();
            _isMicActive = false;
            if (statusText != null) { statusText.text = "트리거를 눌러 시작"; statusText.color = Color.gray; }
        }

        _isProcessing = false;
    }

    private void Enqueue(Action action)
    {
        _mainThreadQueue.Enqueue(action);
    }
}
