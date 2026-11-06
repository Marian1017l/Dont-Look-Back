using UnityEngine;

// Encuentro de "no mires atrás". Va en un cubo con Collider marcado "Is Trigger":
// al entrar el jugador suenan los pasos y, si gira la cámara más de maxAngle, pierde una vida.
// Reemplaza a FootstepsEvent (que se activaba al azar).
public class LookBackEncounter : MonoBehaviour
{
    public AudioSource stepsAudio;       // opcional: sonido de pasos
    public Collider safeZone;            // opcional: al entrar aquí el encuentro termina sin castigo
    public Jumpscare jumpscare;          // opcional: susto al atraparte
    public float warningTime = 1.5f;     // tiempo entre entrar al trigger y que suenen los pasos
    public float duration = 6f;          // cuánto dura la amenaza
    [Range(30, 180)]
    public float maxAngle = 120f;        // tolerancia de mirada: giro permitido antes de perder
    public bool onlyOnce = true;         // si es false, se repite cada vez que el jugador entra

    enum State { Idle, Warning, Active, Done }
    State state = State.Idle;
    float timer;
    Vector3 savedForward;
    Transform player;
    Transform cam;

    void OnTriggerEnter(Collider other)
    {
        if (state != State.Idle || !other.CompareTag("Player")) return;
        player = other.transform;
        cam = Camera.main.transform;
        state = State.Warning;
        timer = warningTime;
    }

    void Update()
    {
        if (state == State.Idle || state == State.Done) return;
        timer -= Time.deltaTime;

        if (state == State.Warning)
        {
            if (timer <= 0) StartSteps();
            return;
        }

        // Zona segura: llegar a ella termina el encuentro
        if (safeZone != null && safeZone.bounds.Contains(player.position)) { End(); return; }

        if (Vector3.Angle(savedForward, Flat(cam.forward)) > maxAngle)
        {
            End();
            if (jumpscare != null) jumpscare.Play();
            GameManager.Instance.LoseLife();
        }
        else if (timer <= 0) End();
    }

    void StartSteps()
    {
        state = State.Active;
        timer = duration;
        savedForward = Flat(cam.forward);   // hacia donde miraba al empezar
        if (stepsAudio != null)
        {
            stepsAudio.transform.position = cam.position - savedForward * 2f;   // detrás del jugador
            stepsAudio.Play();
        }
    }

    void End()
    {
        if (stepsAudio != null) stepsAudio.Stop();
        state = onlyOnce ? State.Done : State.Idle;
    }

    Vector3 Flat(Vector3 v) { v.y = 0; return v.normalized; }   // ignora mirar arriba o abajo

    void OnGUI()
    {
        if (state != State.Warning && state != State.Active) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = state == State.Warning ? Color.gray : new Color(1f, 0.4f, 0.4f);
        string text = state == State.Warning ? "...algo se acerca..." : "PASOS DETRÁS DE TI. NO MIRES ATRÁS";
        GUI.Label(new Rect(0, 60, Screen.width, 50), text, style);
    }
}
