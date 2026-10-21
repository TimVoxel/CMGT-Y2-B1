using UnityEngine;

public class LampMove : MonoBehaviour
{
    public AudioSource music;

    public float rotationAmount = 25f;
    public float sensitivity = 300f;
    public float smooth = 8f;
    public float speed = 4f;

    private float[] samples = new float[64];
    private Quaternion startingRotation;
    private float musicAmount;

    void Start()
    {
        startingRotation = transform.rotation;
    }

    void Update()
    {
        if (music == null)
            return;

        music.GetSpectrumData(
            samples,
            0,
            FFTWindow.BlackmanHarris
        );

        float bass = 0f;

        for (int i = 0; i < 8; i++)
        {
            bass += samples[i];
        }

        bass /= 8f;

        float target = Mathf.Clamp01(bass * sensitivity);

        musicAmount = Mathf.Lerp(
            musicAmount,
            target,
            Time.deltaTime * smooth
        );

        float angle =
            Mathf.Sin(Time.time * speed)
            * musicAmount
            * rotationAmount;


        transform.rotation =
            Quaternion.AngleAxis(angle, Vector3.right)
            * startingRotation;
    }
}