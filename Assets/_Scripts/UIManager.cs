using UnityEngine.SceneManagement;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    public void clickStart(string sceneName)
    {
        SceneManager.LoadScene(sceneName);
    }
}
