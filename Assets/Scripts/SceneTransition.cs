using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransition : MonoBehaviour
{
    public void ReloadScene()
    {
        SceneManager.LoadScene(gameObject.scene.buildIndex);
    }
}
