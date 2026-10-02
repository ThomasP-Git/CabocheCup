using UnityEngine;
using UnityEngine.SceneManagement;
public class playjoueur : MonoBehaviour
{
    public void LoadScene(string joueur)
    {
        SceneManager.LoadScene(joueur);
    }

}
