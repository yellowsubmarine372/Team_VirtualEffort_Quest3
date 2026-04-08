using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Multimodal.Voice;

public class AIChatController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RealtimeVoiceManager voiceManager;
    [SerializeField] private RealtimeAudioPlayer audioPlayer; // null이면 음성 재생 안 함

    [Header("UI (비워두면 해당 UI 무시)")]
    [SerializeField] private TMP_Text userText;
    [SerializeField] private TMP_Text aiText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button micButton;

    private TMP_Text _micButtonText;
    private bool _isMicActive;

    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    private void Start()
    {
        if (micButton != null)
        {
            _micButtonText = micButton.GetComponentInChildren<TMP_Text>();
            micButton.onClick.AddListener(OnMicButtonClicked);
        }

        voiceManager.OnConnected += () => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = "연결됨 - 말하세요"; statusText.color = Color.green; }
        });

        voiceManager.OnDisconnected += (reason) => Enqueue(() =>
        {
            if (statusText != null) { statusText.text = "연결 끊김"; statusText.color = Color.red; }
            SetMicUI(false);
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
        if (statusText != null) { statusText.text = "마이크 버튼을 눌러 시작"; statusText.color = Color.gray; }
    }

    private void Update()
    {
        while (_mainThreadQueue.TryDequeue(out var action))
        {
            action.Invoke();
        }
    }

    private void Enqueue(Action action)
    {
        _mainThreadQueue.Enqueue(action);
    }

    private async void OnMicButtonClicked()
    {
        if (!_isMicActive)
        {
            try
            {
                if (micButton != null) micButton.interactable = false;
                if (statusText != null) statusText.text = "연결 중...";
                await voiceManager.StartVoice("ko");
                SetMicUI(true);
            }
            catch (Exception ex)
            {
                if (statusText != null) { statusText.text = $"시작 실패: {ex.Message}"; statusText.color = Color.red; }
            }
            finally
            {
                if (micButton != null) micButton.interactable = true;
            }
        }
        else
        {
            voiceManager.StopVoice();
            if (audioPlayer != null) audioPlayer.Clear();
            SetMicUI(false);
            if (statusText != null) { statusText.text = "마이크 버튼을 눌러 시작"; statusText.color = Color.gray; }
        }
    }

    private void SetMicUI(bool active)
    {
        _isMicActive = active;
        if (_micButtonText != null)
        {
            _micButtonText.text = active ? "마이크 중지" : "마이크 시작";
        }
    }
}
