using System.Collections;
using UnityEngine;

// Susto del demonio (Demon_APose en los niveles). El objeto debe empezar desactivado.
//  - Show()/Hide(): el demonio espera quieto en un punto (modos Confront y Watch). Aparece entre parpadeos de la linterna,
//    se agita con tics y se acerca muy despacio; al terminar el encuentro se apaga entre parpadeos, sin moverse de su sitio.
//  - Play(): el ataque. Echa a correr desde lejos por el suelo (con el balanceo del cuerpo y la cámara temblando) y llega a la cara.
//    Play(false) es la versión corta (primer golpe); Play(true) la completa (golpe mortal).
public class Jumpscare : MonoBehaviour
{
    public GameObject scareObject;       // el modelo del susto (Demon_APose en los niveles; el prototipo usa un objeto temporal)
    public AudioSource scareSound;       // opcional: grito
    public float distance = 1f;          // qué tan cerca de la cámara termina
    public float duration = 0.9f;
    [Tooltip("Modelos de cuerpo entero: se apoyan en el suelo (la base del modelo, sin importar dónde esté su pivote), no a la altura de los ojos")]
    public bool standOnGround = false;
    [Tooltip("Altura en metros a la que se ajusta el modelo al aparecer (así no depende de la escala del FBX). 0 = no cambia la escala")]
    public float targetHeight = 0f;
    [Tooltip("Transform situado en el centro de la cabeza del demonio: al final de la carrera la cara se coloca delante de la cámara")]
    public Transform headPoint;

    [Header("Ataque")]
    [Tooltip("Metros delante de la cámara desde donde arranca la carrera")]
    public float runFromDistance = 6f;
    [Tooltip("Segundos que dura la carrera (el resto del susto se queda sobre la cara)")]
    public float runTime = 0.45f;
    [Tooltip("Metros que sube y baja el cuerpo al correr")]
    public float runBob = 0.06f;
    [Tooltip("Metros de temblor de la cámara durante el ataque")]
    public float cameraShake = 0.035f;

    [Header("Espera (Show / Hide)")]
    [Tooltip("Cada cuántos segundos se acerca un paso. El modelo no tiene animación de andar, así que avanza en la oscuridad: la linterna falla, desaparece un instante y reaparece más cerca")]
    public float approachEvery = 4f;
    [Tooltip("Metros que se acerca en cada paso")]
    public float approachStep = 0.9f;
    [Tooltip("Metros máximos que puede acercarse mientras espera")]
    public float creepMax = 2f;
    [Tooltip("Grados de agitación sutil de la cabeza y el cuerpo mientras espera")]
    public float twitchDegrees = 2f;

    bool playing;
    bool fitted;
    Renderer[] renderers;
    bool[] rendererWasOn;
    Vector3 baseScale;
    Rigidbody scareBody;
    bool wasKinematic;
    bool usedGravity;

    // Estado de espera
    bool shown;
    float shownAt;
    Vector3 basePos, watchPoint;
    Quaternion baseRot;
    Coroutine flicker;
    int approachIndex;
    Flashlight flashlight;

    // ---------------------------------------------------------------- Sitio libre de paredes

    const float BodyRadius = 0.4f;   // el demonio ocupa una columna de 0,8 m de ancho y 2 m de alto

    // Metros libres desde 'eye' en la dirección 'dir' para una columna del tamaño del demonio (con margen), hasta 'desired'.
    // Se barre todo el cuerpo (no un solo rayo) para que no quede metido en una pared ni en un mueble.
    public static float ClearDistance(Vector3 eye, float floorY, Vector3 dir, float desired)
    {
        Vector3 p1 = new Vector3(eye.x, floorY + 0.5f, eye.z), p2 = new Vector3(eye.x, floorY + 2.0f, eye.z);
        RaycastHit[] hits = Physics.CapsuleCastAll(p1, p2, BodyRadius, dir, desired + 0.1f, ~0, QueryTriggerInteraction.Ignore);
        float best = desired;
        foreach (RaycastHit h in hits)
        {
            if (h.distance <= 0f) continue;                                    // ya se solapaba con el jugador al empezar
            if (h.collider.GetComponentInParent<PlayerController>() != null) continue;
            if (h.collider.bounds.size.y < 0.5f) continue;                     // objetos bajos del suelo no cuentan
            best = Mathf.Min(best, h.distance - 0.1f);
        }
        return Mathf.Max(0f, best);
    }

    // Elige hacia dónde y a qué distancia colocar al demonio: prefiere la dirección pedida; si hay una pared demasiado cerca
    // prueba ligeros giros a ambos lados (hasta maxAngle). Devuelve false si no hay ningún sitio con 'minDist' libre
    // (entonces dir/dist son lo mejor que hay, con al menos 0,8 m).
    public static bool FindSpot(Vector3 eye, float floorY, Vector3 baseDir, float desired, float minDist, float maxAngle, out Vector3 dir, out float dist)
    {
        baseDir.y = 0f;
        baseDir = baseDir.sqrMagnitude > 0.0001f ? baseDir.normalized : Vector3.forward;
        float[] angles = { 0f, 20f, -20f, 40f, -40f, 60f, -60f, 90f, -90f };
        float best = -1f;
        Vector3 bestDir = baseDir;
        foreach (float a in angles)
        {
            if (Mathf.Abs(a) > maxAngle) continue;
            Vector3 d = Quaternion.Euler(0f, a, 0f) * baseDir;
            float clear = ClearDistance(eye, floorY, d, desired);
            if (clear >= minDist) { dir = d; dist = Mathf.Min(desired, clear); return true; }
            if (clear > best) { best = clear; bestDir = d; }
        }
        dir = bestDir;
        dist = Mathf.Max(0.8f, best);
        return false;
    }

    // ---------------------------------------------------------------- Espera: aparece, se agita y desaparece

    // Muestra el demonio quieto en una posición mirando hacia 'facing' (el jugador). No hace nada si el ataque está en curso.
    public void Show(Vector3 position, Vector3 facing)
    {
        if (playing) return;
        bool wasVisible = scareObject.activeSelf;
        StopFlicker();
        scareObject.SetActive(true);
        Fit();
        PlaceAt(position, facing);
        basePos = scareObject.transform.position;
        baseRot = scareObject.transform.rotation;
        watchPoint = facing;
        shownAt = Time.time;
        approachIndex = 0;
        shown = true;
        if (!wasVisible) flicker = StartCoroutine(Flicker(new[] { 0.05f, 0.14f, 0.08f, 0.07f }, true));
    }

    public void Hide()
    {
        if (playing) return;
        shown = false;
        if (!scareObject.activeSelf) return;
        StopFlicker();
        flicker = StartCoroutine(Flicker(new[] { 0.14f, 0.08f, 0.10f, 0.06f, 0.18f }, false));
    }

    // Alterna visible/invisible con los tiempos dados. appearing = true termina visible; false termina apagado del todo.
    // Mientras tanto la linterna del jugador falla, para que el cambio ocurra "en la oscuridad".
    IEnumerator Flicker(float[] steps, bool appearing)
    {
        GlitchFlashlight(0.6f);
        bool on = !appearing;   // al aparecer arranca invisible; al desaparecer, visible
        for (int i = 0; i < steps.Length; i++)
        {
            on = !on;
            SetRenderers(on);
            yield return new WaitForSeconds(steps[i]);
        }
        if (appearing) SetRenderers(true);
        else scareObject.SetActive(false);
        flicker = null;
        if (!appearing) SetRenderers(true);
    }

    // Apagón breve: la linterna falla y el demonio se apaga un instante (en ese momento LateUpdate lo adelanta)
    IEnumerator Step()
    {
        GlitchFlashlight(0.5f);
        SetRenderers(false);
        yield return new WaitForSeconds(0.28f);
        SetRenderers(true);
        flicker = null;
    }

    void StopFlicker()
    {
        if (flicker != null) { StopCoroutine(flicker); flicker = null; }
        if (renderers != null) SetRenderers(true);
    }

    void GlitchFlashlight(float seconds)
    {
        if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        if (flashlight != null) flashlight.Glitch(seconds);
    }

    // Mientras espera: se acerca muy despacio y sufre tics (giros bruscos de cabeza y cuerpo, a rachas)
    void LateUpdate()
    {
        if (!shown || playing || !scareObject.activeSelf) return;
        float t = Time.time - shownAt;
        Vector3 toward = watchPoint - basePos;
        toward.y = 0f;
        toward = toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.zero;

        // Cada 'approachEvery' segundos da un paso adelante, tapado por un parpadeo de la linterna
        int step = approachEvery > 0f ? Mathf.FloorToInt(t / approachEvery) : 0;
        if (step > approachIndex && flicker == null && creepMax > approachStep * approachIndex)
        {
            approachIndex = step;
            flicker = StartCoroutine(Step());
        }
        float room = Mathf.Max(0f, Vector3.Distance(new Vector3(watchPoint.x, basePos.y, watchPoint.z), basePos) - 1.8f);   // se queda a 1,8 m como mínimo
        Vector3 pos = basePos + toward * Mathf.Min(approachStep * approachIndex, creepMax, room);

        // Ruido suave siempre, y de vez en cuando un tic más fuerte y seco
        float tic = Mathf.PerlinNoise(t * 2.5f, 7.3f) > 0.78f ? 1f : 0.25f;
        float pitch = (Mathf.PerlinNoise(t * 3f, 1.7f) - 0.5f) * 2f * twitchDegrees * tic;
        float yaw = (Mathf.PerlinNoise(t * 3f, 4.1f) - 0.5f) * 2f * twitchDegrees * 1.5f * tic;
        float roll = (Mathf.PerlinNoise(t * 2f, 9.9f) - 0.5f) * 2f * twitchDegrees * 0.6f * tic;
        scareObject.transform.SetPositionAndRotation(pos, baseRot * Quaternion.Euler(pitch, yaw, roll));
    }

    // ---------------------------------------------------------------- Ataque

    public void Play(bool full = true)
    {
        if (playing) return;   // nunca se repite mientras está en curso
        StartCoroutine(Scare(full ? duration : duration * 0.5f));
    }

    IEnumerator Scare(float time)
    {
        playing = true;
        shown = false;
        StopFlicker();
        Transform cam = Camera.main.transform;
        GameObject player = standOnGround ? GameObject.FindWithTag("Player") : null;
        Vector3 forward = Flat(cam.forward);
        float floorY = player != null ? player.transform.position.y : cam.position.y - 1.6f;   // el pivote del Player está en los pies

        scareBody = scareObject.GetComponentInChildren<Rigidbody>();
        if (scareBody != null)
        {
            wasKinematic = scareBody.isKinematic;
            usedGravity = scareBody.useGravity;
            scareBody.isKinematic = true;
            scareBody.useGravity = false;
            scareBody.linearVelocity = Vector3.zero;
            scareBody.angularVelocity = Vector3.zero;
        }

        scareObject.SetActive(true);
        Fit();
        if (scareSound != null) scareSound.Play();
        GlitchFlashlight(time);

        // Desde dónde arranca la carrera y hasta dónde llega: se acorta si hay una pared delante (el demonio no la atraviesa)
        Vector3 runDir = forward;
        float startD = runFromDistance, endD = distance;
        if (standOnGround)
        {
            FindSpot(cam.position, floorY, forward, runFromDistance, 2.5f, 50f, out runDir, out startD);
            endD = Mathf.Min(distance, Mathf.Max(0.7f, startD - 0.3f));
        }

        Vector3 camLocal = cam.localPosition;
        float run = Mathf.Max(0.05f, Mathf.Min(runTime, time * 0.65f));
        for (float t = 0f; t < time; t += Time.deltaTime)
        {
            float p = Mathf.Clamp01(t / run);                 // 0 = arranca, 1 = ya está encima
            float d = Mathf.Lerp(startD, endD, p * p);   // acelera: sale despacio y llega a toda velocidad

            if (standOnGround)
            {
                float bob = Mathf.Abs(Mathf.Sin(t * 16f)) * runBob * (1f - p);   // pasos: el cuerpo sube y baja
                Vector3 ground = new Vector3(cam.position.x, floorY + bob, cam.position.z) + runDir * d;
                PlaceAt(ground, cam.position);
                if (headPoint != null)
                {
                    // Solo al final de la carrera la cara se acerca al centro de la cámara (de forma gradual, sin saltar)
                    float w = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.55f, 1f, p));
                    if (w > 0f)
                    {
                        Vector3 gp = scareObject.transform.position;
                        Quaternion gr = scareObject.transform.rotation;
                        PlaceHeadAt(cam, d);
                        scareObject.transform.SetPositionAndRotation(Vector3.Lerp(gp, scareObject.transform.position, w), Quaternion.Slerp(gr, scareObject.transform.rotation, w));
                    }
                }
            }
            else if (headPoint != null) PlaceHeadAt(cam, d);
            else
            {
                scareObject.transform.position = cam.position + cam.forward * d;
                scareObject.transform.LookAt(cam);
            }

            // Cámara temblando, cada vez más al acercarse
            cam.localPosition = camLocal + Random.insideUnitSphere * cameraShake * Mathf.Clamp01(p + 0.3f);
            yield return null;
        }

        cam.localPosition = camLocal;
        scareObject.SetActive(false);
        if (scareBody != null)
        {
            scareBody.isKinematic = wasKinematic;
            scareBody.useGravity = usedGravity;
        }
        playing = false;
    }

    void PlaceHeadAt(Transform cam, float d)
    {
        Transform t = scareObject.transform;
        t.LookAt(cam);
        t.position += cam.position + cam.forward * d - headPoint.position;
    }

    // ---------------------------------------------------------------- Utilidades

    // Ajusta la escala una sola vez (el objeto debe estar activo para medirlo)
    void Fit()
    {
        if (fitted) return;
        fitted = true;
        renderers = scareObject.GetComponentsInChildren<Renderer>(true);
        rendererWasOn = new bool[renderers.Length];
        for (int i = 0; i < renderers.Length; i++) rendererWasOn[i] = renderers[i].enabled;
        baseScale = scareObject.transform.localScale;
        foreach (Animator an in scareObject.GetComponentsInChildren<Animator>(true)) an.applyRootMotion = false;   // la animación no mueve al demonio: lo mueve este script
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

    // Enciende o apaga todos los Renderer del modelo (respeta los que ya venían apagados en el prefab)
    void SetRenderers(bool on)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null) renderers[i].enabled = on && rendererWasOn[i];
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
