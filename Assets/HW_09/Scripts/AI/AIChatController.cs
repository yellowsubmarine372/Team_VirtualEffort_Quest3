using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Multimodal.Voice;

/// <summary>
/// 씬의 UI와 RealtimeVoiceManager/RealtimeAudioPlayer를 연결하는 컨트롤러
///
/// Inspector에서 다음을 드래그 연결:
/// - voiceManager: RealtimeManager 오브젝트
/// - audioPlayer: AudioPlayer 오브젝트
/// - chatText: ScrollView > Content > ChatText
/// - statusText: ChatPanel > StatusText
/// - micButton: MicButton
/// </summary>
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

    private void Start()
    {
        _micButtonText = micButton.GetComponentInChildren<TMP_Text>();

        // 버튼 클릭
        micButton.onClick.AddListener(OnMicButtonClicked);

        // 이벤트 구독
        voiceManager.OnConnected += () =>
        {
            statusText.text = "연결됨 - 말하세요";
            statusText.color = Color.green;
        };

        voiceManager.OnDisconnected += (reason) =>
        {
            statusText.text = "연결 끊김";
            statusText.color = Color.red;
            SetMicUI(false);
        };

        voiceManager.OnSpeechDetected += () =>
        {
            statusText.text = "듣는 중...";
            statusText.color = Color.yellow;
        };

        voiceManager.OnTranscript += (transcript) =>
        {
            AppendChat($"<color=#88ccff>나: {transcript}</color>\n");
            statusText.text = "응답 중...";
            statusText.color = new Color(1f, 0.6f, 0f);
        };

        voiceManager.OnStreamingText += (delta) =>
        {
            // 실시간 텍스트 스트리밍 - 현재 AI 응답에 이어붙이기
            chatText.text += delta;
            ScrollToBottom();
        };

        voiceManager.OnAIResponse += (fullText) =>
        {
            // 응답 완료 - 전체 텍스트로 교체 (스트리밍 중 누적된 것 정리)
            // 스트리밍으로 이미 표시됐으므로 줄바꿈만 추가
            _chatLog = chatText.text + "\n\n";
            chatText.text = _chatLog;
            statusText.text = "말하세요";
            statusText.color = Color.green;
            ScrollToBottom();
        };

        voiceManager.OnAudioDelta += (audioData) =>
        {
            audioPlayer.EnqueueAudio(audioData);
        };

        voiceManager.OnError += (code, msg) =>
        {
            statusText.text = $"오류: {msg}";
            statusText.color = Color.red;
            Debug.LogError($"[AIChatController] {code}: {msg}");
        };

        // 초기 상태
        statusText.text = "마이크 버튼을 눌러 시작";
        statusText.color = Color.gray;
        chatText.text = "";
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
            catch (System.Exception ex)
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
