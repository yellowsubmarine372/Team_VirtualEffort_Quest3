using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public void LoadTextChatScene()
    {
        SceneManager.LoadScene("NPCChat");
    }

    public void LoadVoiceChatScene()
    {
        SceneManager.LoadScene("NPCVoice");
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
