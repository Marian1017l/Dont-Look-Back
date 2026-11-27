using UnityEngine;

// Encuentro de terror. Va en un cubo con Collider marcado "Is Trigger": al entrar el jugador
// suena el aviso y comienza la amenaza. Tres modos:
//  - NoLook (pasos detrás): si el jugador gira la cámara más de maxAngle, lo atacan.
//  - Freeze (respiración detrás): si el jugador se mueve más de moveTolerance, lo atacan.
//  - Confront (respiración detrás + el demonio aparece DELANTE): mirar al demonio es seguro, pero girar más de maxAngle
//    (mirar atrás) o moverse más de moveTolerance lo atrae. Es la combinación de los dos modos anteriores.
//  - Watch (el demonio aparece delante): el jugador SÍ puede moverse, pero debe mantener al demonio dentro de su campo de visión
//    (watchViewAngle, 120° en total). Si lo pierde de vista más de watchGrace segundos, lo atacan.
// Si hay PlayerHealth en la escena el ataque quita salud; si no, usa las vidas del prototipo.
public class LookBackEncounter : MonoBehaviour
{
    public enum Mode { NoLook, Freeze, Confront, Watch }

    public Mode mode = Mode.NoLook;
    public AudioSource stepsAudio;       // sonido del aviso (pasos o respiración)
    public Collider safeZone;            // opcional: al entrar aquí el encuentro termina sin castigo
    public Jumpscare jumpscare;          // opcional: susto al atraparte
    public float warningTime = 1.5f;     // tiempo entre entrar al trigger y que suene el aviso
    [Tooltip("Segundos extra de tensión antes de que aparezca la amenaza: la respiración sube poco a poco y la linterna falla cada vez más")]
    public float buildUpTime = 4f;
    public float duration = 6f;          // cuánto dura la amenaza
    public float soundDistance = 2f;     // a qué distancia detrás del jugador suena
    [Range(30, 180)]
    public float maxAngle = 120f;        // NoLook: giro permitido antes de perder
    public float graceTime = 0.8f;       // Freeze: tiempo para reaccionar y detenerse
    public float moveTolerance = 0.4f;   // Freeze y Confront: metros que se pueden desplazar sin perder
    public float demonDistance = 5f;     // Confront: a cuántos metros del jugador aparece el demonio (se acorta si hay una pared)
    public enum DemonSide { Ahead, Behind }
    [Tooltip("Confront: dónde aparece el demonio. Ahead = delante (mirarlo es seguro). Behind = detrás (verlo al girar activa el susto)")]
    public DemonSide demonSide = DemonSide.Ahead;
    [Tooltip("Confront: moverse más de moveTolerance activa el susto")]
    public bool punishMovement = true;
    [Tooltip("Confront: girar la cámara más de maxAngle (mirar atrás) activa el susto")]
    public bool punishLookBack = true;
    [Header("Modo Watch")]
    [Range(40, 180)]
    [Tooltip("Campo de visión en el que hay que mantener al demonio (total: 120 = 60° a cada lado del centro de la cámara)")]
    public float watchViewAngle = 120f;
    [Tooltip("Segundos que se puede perder de vista al demonio antes del susto (evita morir por un tirón del ratón)")]
    public float watchGrace = 0.35f;
    [Header("General")]
    public bool onlyOnce = true;         // si es false, se repite cada vez que el jugador entra
    public bool showHint = true;         // texto de ayuda en pantalla (apagado en niveles reales)

    // true mientras algún encuentro está en aviso o activo: el inventario no se puede abrir (no hay "pausa" para esquivar la regla)
    public static bool AnyThreat => threats > 0;
    static int threats;

    enum State { Idle, Warning, Active, Done }
    State state = State.Idle;
    float timer;
    float audioVolume = 1f;
    bool demonShown;        // el demonio ya está en su sitio (si no cabe, espera a que haya hueco libre)
    bool demonSkipped;      // no cupo en ningún sitio: esta vez el encuentro sigue sin demonio visible
    float demonWait;
    float nextGlitch;
    Flashlight flashlight;
    bool armed;
    Vector3 savedForward;
    Vector3 startPos;
    Vector3 demonPos;
    float lostSight;
    Transform player;
    Transform cam;

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Idle || !other.CompareTag("Player")) return;
        player = other.transform;
        cam = Camera.main.transform;
        state = State.Warning;
        timer = warningTime + buildUpTime;
        nextGlitch = 0f;
        if (stepsAudio != null) audioVolume = stepsAudio.volume;
        if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        threats++;
    }

    void OnDestroy()
    {
        if (state == State.Warning || state == State.Active) threats = Mathf.Max(0, threats - 1);   // la escena se recarga en pleno encuentro
    }

    void Update()
    {
        if (state == State.Idle || state == State.Done) return;
        timer -= Time.deltaTime;

        if (state == State.Warning)
        {
            if (timer <= 0) StartThreat();
            else BuildUp();
            return;
        }

        // Zona segura: llegar a ella termina el encuentro
        if (safeZone != null && safeZone.bounds.Contains(player.position)) { End(); return; }

        // Si al empezar el jugador estaba pegado a una pared, el demonio espera a que haya hueco (hasta 3 s; después aparece donde mejor quepa)
        if (UsesDemon() && !demonShown)
        {
            demonWait += Time.deltaTime;
            demonShown = TryShowDemon(demonWait < 3f);
        }

        if (Broken()) { Caught(); return; }
        if (timer <= 0) End();
    }

    // Últimos segundos antes de que aparezca la amenaza: la respiración empieza muy baja detrás del jugador y sube,
    // y la linterna falla cada vez con más frecuencia. El jugador siente que algo se acerca antes de verlo.
    void BuildUp()
    {
        if (buildUpTime <= 0f || timer > buildUpTime) return;
        float build = Mathf.Clamp01(1f - timer / buildUpTime);   // 0 al empezar la tensión, 1 justo antes de aparecer
        if (stepsAudio != null)
        {
            stepsAudio.transform.position = cam.position - Flat(cam.forward) * soundDistance;
            stepsAudio.volume = audioVolume * build * build;
            if (!stepsAudio.isPlaying) stepsAudio.Play();
        }
        nextGlitch -= Time.deltaTime;
        if (flashlight != null && nextGlitch <= 0f)
        {
            flashlight.Glitch(0.15f + 0.2f * build);
            nextGlitch = Mathf.Lerp(1.8f, 0.4f, build) + Random.Range(0f, 0.4f);
        }
    }

    bool Broken()
    {
        // Mirar atrás: ángulo horizontal entre hacia dónde miraba al empezar y hacia dónde mira ahora
        if (mode == Mode.Watch) return demonShown && !demonSkipped && LostDemon();
        bool checkLook = mode == Mode.NoLook || (mode == Mode.Confront && punishLookBack);
        bool checkMove = mode == Mode.Freeze || (mode == Mode.Confront && punishMovement);
        if (checkLook && Vector3.Angle(savedForward, Flat(cam.forward)) > maxAngle) return true;
        if (!checkMove) return false;

        // Freeze y Confront: tras el tiempo de reacción se fija la posición y no se puede salir de ella
        if (duration - timer < graceTime) return false;
        if (!armed) { armed = true; startPos = player.position; return false; }
        Vector3 moved = player.position - startPos;
        moved.y = 0;
        return moved.magnitude > moveTolerance;
    }

    // Watch: ángulo entre hacia dónde mira la cámara y la dirección al demonio (en horizontal)
    bool LostDemon()
    {
        Vector3 toDemon = demonPos - cam.position;
        toDemon.y = 0;
        if (toDemon.sqrMagnitude < 0.01f) return false;
        bool visible = Vector3.Angle(Flat(cam.forward), toDemon.normalized) <= watchViewAngle * 0.5f;
        lostSight = visible ? 0f : lostSight + Time.deltaTime;
        return lostSight > watchGrace;
    }

    void Caught()
    {
        End();
        if (PlayerHealth.Instance != null)
        {
            if (jumpscare != null) jumpscare.Play(PlayerHealth.Instance.IsLethalHit);
            PlayerHealth.Instance.TakeHit();
        }
        else
        {
            if (jumpscare != null) jumpscare.Play();
            if (GameManager.Instance != null) GameManager.Instance.LoseLife();
        }
    }

    void StartThreat()
    {
        state = State.Active;
        timer = duration;
        armed = false;
        lostSight = 0f;
        savedForward = Flat(cam.forward);   // hacia donde miraba al empezar
        if (stepsAudio != null)
        {
            stepsAudio.transform.position = cam.position - savedForward * soundDistance;   // detrás del jugador
            stepsAudio.volume = audioVolume;
            if (!stepsAudio.isPlaying) stepsAudio.Play();
        }
        demonShown = false;
        demonSkipped = false;
        demonWait = 0f;
        if (UsesDemon()) demonShown = TryShowDemon(true);
    }

    bool UsesDemon() => (mode == Mode.Confront || mode == Mode.Watch) && jumpscare != null;

    // El demonio aparece quieto delante (o detrás) del jugador, mirándolo, en un sitio libre de paredes y muebles:
    // si la pared está cerca se queda justo delante de ella; si no cabe, prueba un poco hacia los lados (siempre dentro del campo de visión).
    // strict = true: si no hay un hueco de 2,5 m devuelve false y se reintenta más tarde.
    bool TryShowDemon(bool strict)
    {
        Vector3 dir = demonSide == DemonSide.Behind ? -savedForward : savedForward;
        bool ok = Jumpscare.FindSpot(cam.position, player.position.y, dir, demonDistance, 2.5f, 40f, out Vector3 spotDir, out float dist);
        if (!ok && strict) return false;
        // Sin hueco ideal tras esperar: se acepta menos espacio (1,5 m) y más giro (90°). Si ni así cabe, no aparece (nunca dentro de una pared).
        if (!ok) ok = Jumpscare.FindSpot(cam.position, player.position.y, dir, demonDistance, 1.5f, 90f, out spotDir, out dist);
        if (!ok) { demonSkipped = true; return true; }
        Vector3 pos = player.position + spotDir * dist;   // el pivote del Player está en los pies
        demonPos = pos;
        jumpscare.Show(pos, player.position);
        return true;
    }

    void End()
    {
        if (stepsAudio != null) { stepsAudio.Stop(); stepsAudio.volume = audioVolume; }
        if ((mode == Mode.Confront || mode == Mode.Watch) && jumpscare != null) jumpscare.Hide();
        threats = Mathf.Max(0, threats - 1);
        state = onlyOnce ? State.Done : State.Idle;
    }

    Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }   // ignora mirar arriba o abajo

    void OnGUI()
    {
        if (!showHint || (state != State.Warning && state != State.Active)) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = state == State.Warning ? Color.gray : new Color(1f, 0.4f, 0.4f);
        string active = mode == Mode.Freeze ? "RESPIRACIÓN DETRÁS DE TI. NO TE MUEVAS"
                      : mode == Mode.Watch ? "EL DEMONIO ESTÁ AHÍ. NO LO PIERDAS DE VISTA"
                      : mode == Mode.Confront ? (demonSide == DemonSide.Behind ? "ALGO ESTÁ DETRÁS DE TI. NO MIRES ATRÁS" : "ALGO TE MIRA DE FRENTE. NO TE MUEVAS NI MIRES ATRÁS")
                      : "PASOS DETRÁS DE TI. NO MIRES ATRÁS";
        GUI.Label(new Rect(0, 60, Screen.width, 50), state == State.Warning ? "...algo se acerca..." : active, style);
    }
}
