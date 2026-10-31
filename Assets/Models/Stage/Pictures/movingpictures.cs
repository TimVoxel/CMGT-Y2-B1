using UnityEngine;

public class movingpictures : MonoBehaviour
{
    [SerializeField] private Renderer[] screens;
    [SerializeField] private Texture2D[] pictures;
    [SerializeField] private float secondsPerPicture = 8f;

    private int pictureIndex = 0;
    private float timer = 0f;

    private void Start()
    {
        if (screens.Length == 0 || pictures.Length == 0)
        {
            Debug.LogError("Please assign 3 screens and at least 1 picture.");
            enabled = false;
            return;
        }

        DisplayPicture();
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= secondsPerPicture)
        {
            timer = 0f;
            pictureIndex = (pictureIndex + 1) % pictures.Length;
            DisplayPicture();
        }
    }

    private void DisplayPicture()
    {
        foreach (Renderer screen in screens)
        {
            if (screen != null)
            {
                screen.material.mainTexture = pictures[pictureIndex];
            }
        }
    }
}