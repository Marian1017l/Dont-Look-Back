using System.Collections;
using UnityEngine;

// Susto: un objeto (el demonio en los niveles reales) aparece de golpe frente a la cámara un instante.
// El objeto debe empezar desactivado. Se llama con Play() desde el encuentro.
// Play(false) es la versión corta (primer golpe); Play(true) la completa (golpe mortal).
// Show()/Hide() lo dejan quieto en un punto (el demonio que espera delante del jugador en el modo Confront).
public class Jumpscare : MonoBehaviour
{
    public GameObject scareObject;       // el modelo del susto (Demon_APose en los niveles; el prototipo usa un objeto temporal)
    public AudioSource scareSound;       // opcional: grito
    public float distance = 1.5f;        // qué tan cerca de la cámara aparece
    public float duration = 0.9f;
    [Tooltip("Modelos de cuerpo entero: se apoyan en el suelo (la base del modelo, sin importar dónde esté su pivote), no a la altura de los ojos")]
    public bool standOnGround = false;
    [Tooltip("Altura en metros a la que se ajusta el modelo al aparecer (así no depende de la escala del FBX). 0 = no cambia la escala")]
    public float targetHeight = 0f;
    [Tooltip("Metros extra de donde parte el salto: aparece más lejos y se lanza hasta 'distance'")]
    public float lungeDistance = 0f;
    public float lungeTime = 0.12f;      // segundos que dura el salto

    bool playing;
    bool fitted;
    Renderer[] renderers;
    Vector3 baseScale;

    // Muestra el demonio quieto en una posición (por ejemplo delante del jugador durante un encuentro) mirando hacia 'facing'.
    // No hace nada si el susto está en curso. Hide() lo quita; Play() lo reutiliza para el ataque.
    public void Show(Vector3 position, Vector3 facing)
    {
        if (playing) return;
        scareObject.SetActive(true);
        Fit();
        PlaceAt(position, facing);
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

        scareObject.SetActive(true);
        Fit();
        if (scareSound != null) scareSound.Play();

        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float d = Mathf.Lerp(distance + lungeDistance, distance, lungeTime > 0f ? Mathf.Clamp01(t / lungeTime) : 1f);
            if (standOnGround) PlaceAt(new Vector3(cam.position.x, floorY, cam.position.z) + forward * d, cam.position);
            else
            {
                scareObject.transform.position = cam.position + cam.forward * d;
                scareObject.transform.LookAt(cam);
            }
            yield return null;
        }

        scareObject.SetActive(false);
        playing = false;
    }

    // Ajusta la escala una sola vez (el objeto debe estar activo para medirlo)
    void Fit()
    {
        if (fitted) return;
        fitted = true;
        renderers = scareObject.GetComponentsInChildren<Renderer>(true);
        baseScale = scareObject.transform.localScale;
        if (renderers.Length == 0)
        {
            Debug.LogWarning("Jumpscare: '" + scareObject.name + "' no tiene ningún Renderer, no se va a ver nada.", this);
            return;
        }
        if (targetHeight > 0f)
        {
            float h = WorldBounds().size.y;
            if (h > 0.01f) scareObject.transform.localScale = baseScale * (targetHeight / h);
        }
    }

    // Coloca el modelo con la base apoyada en pos.y y centrado en (pos.x, pos.z), mirando hacia 'look' (solo gira en vertical)
    void PlaceAt(Vector3 pos, Vector3 look)
    {
        Transform t = scareObject.transform;
        look.y = pos.y;
        t.position = pos;
        if ((look - pos).sqrMagnitude > 0.0001f) t.LookAt(look);
        if (!standOnGround || renderers == null || renderers.Length == 0) return;
        Bounds b = WorldBounds();   // el pivote del modelo puede estar en cualquier parte: se corrige con su caja real
        t.position += new Vector3(pos.x - b.center.x, pos.y - b.min.y, pos.z - b.center.z);
    }

    Bounds WorldBounds()
    {
        Bounds b = new Bounds(scareObject.transform.position, Vector3.zero);
        bool first = true;
        foreach (Renderer r in renderers)
        {
            if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static Vector3 Flat(Vector3 v)
    {
        v.y = 0;
        return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
    }
}
