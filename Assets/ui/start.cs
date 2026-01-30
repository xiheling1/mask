using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class menu : MonoBehaviour
{
   public void StartGame()
   {
       UnityEngine.SceneManagement.SceneManager.LoadScene("Main Scenes");
    }

    public void QuitGame()
    {
        Application.Quit();
    }
}
