using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused = false;
    public GameObject PauseUI;
    PlayerActions menuActions;
    public static bool menu = false;

    void Update()
    {
        if(menu)
        {
            Debug.Log("HI");
            if(IsPaused){
                Resume();
            }
            else
            {
                Pause();
            }
            menu = false;
        }
    }

    public void Resume() 
    {
        PauseUI.SetActive(false);
        Time.timeScale = 1f;
        IsPaused = false;
    }

    public void Pause()
    {
        PauseUI.SetActive(true);
        Time.timeScale = 0f;
        IsPaused = true;
    }

    public void Reset()
    {
        // TODO: Create Constants
        Time.timeScale = 1f;
        IsPaused = false;
        SceneManager.LoadScene(0);
        Debug.Log("Went to Menu!");
    }

    public void Quit()
    {
        Application.Quit();
        Debug.Log("Quit!");
    }
}
