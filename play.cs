using UnityEngine;
using UnityEngine.SceneManagement;

public class play : MonoBehaviour
{


    public void LoadScene(string scenename)
    {
        SceneManager.LoadScene(scenename);
    }
}
