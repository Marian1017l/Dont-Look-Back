using System.Collections;
using UnityEngine;

// Susto de prueba: un objeto (por ahora un cubo) aparece de golpe frente a la cámara un instante.
// El objeto debe empezar desactivado. Se llama con Play() desde el encuentro.
public class Jumpscare : MonoBehaviour
{
    public GameObject scareObject;       // cubo temporal; luego será el modelo del fantasma
    public AudioSource scareSound;       // opcional: grito
    public float distance = 1.5f;        // qué tan cerca de la cámara aparece
    public float duration = 0.9f;

    public void Play() => StartCoroutine(Scare());

    IEnumerator Scare()
    {
        Transform cam = Camera.main.transform;
        scareObject.transform.position = cam.position + cam.forward * distance;
        scareObject.transform.LookAt(cam);
        scareObject.SetActive(true);
        if (scareSound != null) scareSound.Play();

        yield return new WaitForSeconds(duration);
        scareObject.SetActive(false);
    }
}
