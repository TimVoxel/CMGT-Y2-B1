using UnityEngine;

public class PictureBoard : MonoBehaviour
{
    public Texture[] pictures;
    public float secondsBetweenPictures = 10f;

    private Renderer boardRenderer;
    private int currentPicture;

    void Start()
    {
        boardRenderer = GetComponent<Renderer>();

        if (pictures == null || pictures.Length == 0)
        {
            Debug.LogError("No pictures assigned to " + gameObject.name);
            return;
        }

        ShowPicture();
        InvokeRepeating(nameof(NextPicture), secondsBetweenPictures, secondsBetweenPictures);
    }

    void ShowPicture()
    {
        Material material = boardRenderer.material;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", pictures[currentPicture]);
        else
            material.mainTexture = pictures[currentPicture];
    }

    void NextPicture()
    {
        currentPicture = (currentPicture + 1) % pictures.Length;
        ShowPicture();
    }
}