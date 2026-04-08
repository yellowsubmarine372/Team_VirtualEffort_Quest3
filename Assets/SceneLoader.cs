using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadTextChatScene()
    {
        SceneManager.LoadScene("TextChatScene");
    }

    public void LoadVoiceChatScene()
    {
        SceneManager.LoadScene("VoiceChatScene");
    }

    public void LoadMultiPlayScene()
    {
        SceneManager.LoadScene("MultiPlayScene");
    }

    public void LoadMainScene()
    {
        SceneManager.LoadScene("MainScene");
    }
}
