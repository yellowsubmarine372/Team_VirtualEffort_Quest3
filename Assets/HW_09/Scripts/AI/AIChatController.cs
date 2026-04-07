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
    [SerializeField] private RealtimeAudioPlayer audioPlayer;

    [Header("UI")]
    [SerializeField] private TMP_Text chatText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button micButton;
    [SerializeField] private ScrollRect scrollRect;

    private TMP_Text _micButtonText;
    private bool _isMicActive;
    private string _chatLog = "";

    // 백그라운드 스레드 → 메인 스레드 마샬링용 큐
    private readonly ConcurrentQueue<Action> _mainThreadQueue = new ConcurrentQueue<Action>();

    private void Start()
    {
        _micButtonText = micButton.GetComponentInChildren<TMP_Text>();
        micButton.onClick.AddListener(OnMicButtonClicked);

        voiceManager.OnConnected += () => Enqueue(() =>
        {
            statusText.text = "연결됨 - 말하세요";
            statusText.color = Color.green;
        });

        voiceManager.OnDisconnected += (reason) => Enqueue(() =>
        {
            statusText.text = "연결 끊김";
            statusText.color = Color.red;
            SetMicUI(false);
        });

        voiceManager.OnSpeechDetected += () => Enqueue(() =>
        {
            statusText.text = "듣는 중...";
            statusText.color = Color.yellow;
        });

        voiceManager.OnTranscript += (transcript) => Enqueue(() =>
        {
            AppendChat($"<color=#88ccff>나: {transcript}</color>\n");
            statusText.text = "응답 중...";
            statusText.color = new Color(1f, 0.6f, 0f);
        });

        voiceManager.OnStreamingText += (delta) => Enqueue(() =>
        {
            chatText.text += delta;
            ScrollToBottom();
        });

        voiceManager.OnAIResponse += (fullText) => Enqueue(() =>
        {
            _chatLog = chatText.text + "\n\n";
            chatText.text = _chatLog;
            statusText.text = "말하세요";
            statusText.color = Color.green;
            ScrollToBottom();
        });

        voiceManager.OnAudioDelta += (audioData) => Enqueue(() =>
        {
            audioPlayer.EnqueueAudio(audioData);
        });

        voiceManager.OnError += (code, msg) => Enqueue(() =>
        {
            statusText.text = $"오류: {msg}";
            statusText.color = Color.red;
            Debug.LogError($"[AIChatController] {code}: {msg}");
        });

        statusText.text = "마이크 버튼을 눌러 시작";
        statusText.color = Color.gray;
        chatText.text = "";
    }

    private void Update()
    {
        // 매 프레임 큐에 쌓인 액션을 메인 스레드에서 실행
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
                micButton.interactable = false;
                statusText.text = "연결 중...";
                await voiceManager.StartVoice("ko");
                SetMicUI(true);
            }
            catch (Exception ex)
            {
                statusText.text = $"시작 실패: {ex.Message}";
                statusText.color = Color.red;
            }
            finally
            {
                micButton.interactable = true;
            }
        }
        else
        {
            voiceManager.StopVoice();
            audioPlayer.Clear();
            SetMicUI(false);
            statusText.text = "마이크 버튼을 눌러 시작";
            statusText.color = Color.gray;
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

    private void AppendChat(string text)
    {
        _chatLog += text;
        chatText.text = _chatLog + "\n<color=#aaffaa>AI: </color>";
        ScrollToBottom();
    }

    private void ScrollToBottom()
    {
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }
}
