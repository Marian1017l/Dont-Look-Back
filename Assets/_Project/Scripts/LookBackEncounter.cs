using UnityEngine;

// Encuentro de terror. Va en un cubo con Collider marcado "Is Trigger": al entrar el jugador
// suena el aviso y comienza la amenaza. Dos modos:
//  - NoLook (pasos detrás): si el jugador gira la cámara más de maxAngle, lo atacan.
//  - Freeze (respiración detrás): si el jugador se mueve más de moveTolerance, lo atacan.
// Si hay PlayerHealth en la escena el ataque quita salud; si no, usa las vidas del prototipo.
public class LookBackEncounter : MonoBehaviour
{
    public enum Mode { NoLook, Freeze }

    public Mode mode = Mode.NoLook;
    public AudioSource stepsAudio;       // sonido del aviso (pasos o respiración)
    public Collider safeZone;            // opcional: al entrar aquí el encuentro termina sin castigo
    public Jumpscare jumpscare;          // opcional: susto al atraparte
    public float warningTime = 1.5f;     // tiempo entre entrar al trigger y que suene el aviso
    public float duration = 6f;          // cuánto dura la amenaza
    public float soundDistance = 2f;     // a qué distancia detrás del jugador suena
    [Range(30, 180)]
    public float maxAngle = 120f;        // NoLook: giro permitido antes de perder
    public float graceTime = 0.8f;       // Freeze: tiempo para reaccionar y detenerse
    public float moveTolerance = 0.4f;   // Freeze: metros que se pueden desplazar sin perder
    public bool onlyOnce = true;         // si es false, se repite cada vez que el jugador entra
    public bool showHint = true;         // texto de ayuda en pantalla (apagado en niveles reales)

    enum State { Idle, Warning, Active, Done }
    State state = State.Idle;
    float timer;
    bool armed;
    Vector3 savedForward;
    Vector3 startPos;
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
            if (timer <= 0) StartThreat();
            return;
        }

        // Zona segura: llegar a ella termina el encuentro
        if (safeZone != null && safeZone.bounds.Contains(player.position)) { End(); return; }

        if (Broken()) { Caught(); return; }
        if (timer <= 0) End();
    }

    bool Broken()
    {
        if (mode == Mode.NoLook)
            return Vector3.Angle(savedForward, Flat(cam.forward)) > maxAngle;

        // Freeze: tras el tiempo de reacción se fija la posición y no se puede salir de ella
        if (duration - timer < graceTime) return false;
        if (!armed) { armed = true; startPos = player.position; return false; }
        Vector3 moved = player.position - startPos;
        moved.y = 0;
        return moved.magnitude > moveTolerance;
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
        savedForward = Flat(cam.forward);   // hacia donde miraba al empezar
        if (stepsAudio != null)
        {
            stepsAudio.transform.position = cam.position - savedForward * soundDistance;   // detrás del jugador
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
        if (!showHint || (state != State.Warning && state != State.Active)) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 30, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = state == State.Warning ? Color.gray : new Color(1f, 0.4f, 0.4f);
        string active = mode == Mode.Freeze ? "RESPIRACIÓN DETRÁS DE TI. NO TE MUEVAS" : "PASOS DETRÁS DE TI. NO MIRES ATRÁS";
        GUI.Label(new Rect(0, 60, Screen.width, 50), state == State.Warning ? "...algo se acerca..." : active, style);
    }
}
