using UnityEngine;
using UnityEngine.SceneManagement;
public class menu : MonoBehaviour
{
    public void LoadScene(string menu)
    {
        SceneManager.LoadScene(menu);
    }

}