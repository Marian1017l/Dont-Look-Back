using System.Collections;
using UnityEngine;

// Susto: un objeto (el demonio en los niveles reales) aparece de golpe frente a la cámara un instante.
// El objeto debe empezar desactivado. Se llama con Play() desde el encuentro.
// Play(false) es la versión corta (primer golpe); Play(true) la completa (golpe mortal).
public class Jumpscare : MonoBehaviour
{
    public GameObject scareObject;       // el modelo del susto (Demon_APose en los niveles; el prototipo usa un objeto temporal)
    public AudioSource scareSound;       // opcional: grito
    public float distance = 1.5f;        // qué tan cerca de la cámara aparece
    public float duration = 0.9f;
    [Tooltip("Modelos de cuerpo entero con el pivote en los pies: se colocan sobre el suelo, no a la altura de los ojos")]
    public bool standOnGround = false;
    [Tooltip("Metros extra de donde parte el salto: aparece más lejos y se lanza hasta 'distance'")]
    public float lungeDistance = 0f;
    public float lungeTime = 0.12f;      // segundos que dura el salto

    bool playing;

    // Muestra el demonio quieto en una posición (por ejemplo delante del jugador durante un encuentro) mirando hacia 'facing'.
    // No hace nada si el susto está en curso. Hide() lo quita; Play() lo reutiliza para el ataque.
    public void Show(Vector3 position, Vector3 facing)
    {
        if (playing) return;
        Transform t = scareObject.transform;
        t.position = position;
        facing.y = position.y;   // solo gira sobre el eje vertical
        t.LookAt(facing);
        scareObject.SetActive(true);
    }

    public void Hide()
    {
        if (!playing) scareObject.SetActive(false);
    }

    public void Play(bool full = true)
    {
        if (playing) return;   // nunca se repite mientras está en curso
        StartCoroutine(Scare(full ? duration : duration * 0.5f));
    }

    IEnumerator Scare(float time)
    {
        playing = true;
        Transform cam = Camera.main.transform;
        GameObject player = standOnGround ? GameObject.FindWithTag("Player") : null;
        Vector3 forward = Flat(cam.forward);
        float floorY = player != null ? player.transform.position.y : cam.position.y - 1.6f;   // el pivote del Player está en los pies

        Place(cam, forward, floorY, distance + lungeDistance);
        scareObject.SetActive(true);
        if (scareSound != null) scareSound.Play();

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float d = Mathf.Lerp(distance + lungeDistance, distance, lungeTime > 0f ? Mathf.Clamp01(t / lungeTime) : 1f);
            Place(cam, forward, floorY, d);
            yield return null;
        }

        scareObject.SetActive(false);
        playing = false;
    }

    // Lo coloca frente a la cámara y mirándola. De cuerpo entero: sobre el suelo y sin inclinarse.
    void Place(Transform cam, Vector3 forward, float floorY, float d)
    {
        Transform t = scareObject.transform;
        if (standOnGround)
        {
            Vector3 pos = cam.position + forward * d;
            pos.y = floorY;
            t.position = pos;
            Vector3 look = cam.position;
            look.y = floorY;
            t.LookAt(look);
        }
        else
        {
            t.position = cam.position + cam.forward * d;
            t.LookAt(cam);
        }
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
