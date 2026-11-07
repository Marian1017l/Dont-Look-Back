using System.Collections;
using UnityEngine;

// Susto: un objeto (por ahora temporal) aparece de golpe frente a la cámara un instante.
// El objeto debe empezar desactivado. Se llama con Play() desde el encuentro.
// Play(false) es la versión corta (primer golpe); Play(true) la completa (golpe mortal).
public class Jumpscare : MonoBehaviour
{
    public GameObject scareObject;       // temporal; luego será el modelo del fantasma
    public AudioSource scareSound;       // opcional: grito
    public float distance = 1.5f;        // qué tan cerca de la cámara aparece
    public float duration = 0.9f;

    bool playing;

    public void Play(bool full = true)
    {
        if (playing) return;   // nunca se repite mientras está en curso
        StartCoroutine(Scare(full ? duration : duration * 0.5f));
    }

    IEnumerator Scare(float time)
    {
        playing = true;
        Transform cam = Camera.main.transform;
        scareObject.transform.position = cam.position + cam.forward * distance;
        scareObject.transform.LookAt(cam);
        scareObject.SetActive(true);
        if (scareSound != null) scareSound.Play();

        yield return new WaitForSeconds(time);
        scareObject.SetActive(false);
        playing = false;
    }
}
