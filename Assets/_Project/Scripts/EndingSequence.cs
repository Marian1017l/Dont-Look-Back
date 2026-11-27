using UnityEngine;
using UnityEngine.InputSystem;

// Final del juego: el jugador sale del hospital al amanecer y camina sin mirar atrás. Es una secuencia guiada (sin control del jugador):
//  1. Fundido desde negro, la cámara avanza por el camino mientras la noche da paso al alba (luz, niebla y fondo cambian).
//  2. Subtítulos sueltos. Hacia el final empieza a sonar una respiración detrás de la cámara.
//  3. Corte a negro con el sonido en seco, y créditos. Al terminar (o con ESC) se vuelve al menú.
// Va en la escena Ending (la crea Don't Look Back > Crear menú principal y final).
public class EndingSequence : MonoBehaviour
{
    [System.Serializable]
    public struct Subtitle { public float time; public float duration; [TextArea] public string text; }

    public Transform cam;                     // la cámara que avanza (se busca como Camera.main si está vacía)
    public Light sun;                         // luz direccional: luna al principio, sol bajo al final
    public AudioSource ambience;              // ambiente continuo
    public AudioSource breathing;             // respiración que aparece detrás de la cámara hacia el final
    public Vector3 startPoint = new Vector3(0f, 1.7f, 0f);
    public Vector3 endPoint = new Vector3(0f, 1.7f, 44f);
    public float walkTime = 38f;              // segundos de caminata
    public float breathingStart = 27f;        // cuándo empieza a sonar la respiración
    public float cutTime = 41f;               // corte a negro (un poco después de que la cámara se detenga)
    public float creditsSpeed = 70f;          // píxeles por segundo (a 1080p)

    public Subtitle[] subtitles =
    {
        new Subtitle { time = 4f,  duration = 4.5f, text = "La puerta cedió." },
        new Subtitle { time = 11f, duration = 5f,   text = "Afuera, el aire no huele a nada." },
        new Subtitle { time = 19f, duration = 5f,   text = "Por fin amanece." },
        new Subtitle { time = 28f, duration = 4.5f, text = "Detrás de ti, algo respira." },
        new Subtitle { time = 34f, duration = 6f,   text = "No mires atrás." },
    };

    static readonly Color NightFog = new Color(0.015f, 0.02f, 0.035f), DawnFog = new Color(0.52f, 0.47f, 0.42f);
    static readonly Color NightLight = new Color(0.45f, 0.55f, 0.9f), DawnLight = new Color(1f, 0.72f, 0.45f);
    static readonly Color NightAmbient = new Color(0.02f, 0.025f, 0.04f), DawnAmbient = new Color(0.26f, 0.25f, 0.25f);

    enum Phase { Walk, Black, Credits }
    Phase phase = Phase.Walk;
    float t, phaseStart, creditsScroll;
    Camera camera;
    GUIStyle subStyle;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        Time.timeScale = 1f;
        camera = Camera.main;
        if (cam == null && camera != null) cam = camera.transform;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        Apply(0f);
        if (ambience != null) ambience.Play();
        if (breathing != null) { breathing.volume = 0f; breathing.loop = true; }
    }

    void Update()
    {
        t += Time.deltaTime;
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { GameFlow.BackToMenu(); enabled = false; return; }

        if (phase == Phase.Walk)
        {
            float u = Mathf.Clamp01(t / walkTime);
            Apply(u);
            MoveCamera(u);
            UpdateBreathing();
            if (t >= cutTime) EnterBlack();
        }
        else if (phase == Phase.Black)
        {
            if (t - phaseStart > 1.8f) { phase = Phase.Credits; phaseStart = t; }
        }
        else
        {
            creditsScroll += Time.deltaTime * creditsSpeed * (Screen.height / 1080f);
            float s = Screen.height / 1080f;
            if (creditsScroll > CreditsRoll.TotalHeight(s) + Screen.height * 0.9f) { GameFlow.BackToMenu(); enabled = false; }
        }
    }

    void EnterBlack()
    {
        phase = Phase.Black;
        phaseStart = t;
        if (breathing != null) breathing.Stop();   // el sonido se corta en seco
        if (ambience != null) ambience.volume = 0f;
    }

    // Ruido y luz: de noche a amanecer. La curva retrasa el cambio fuerte para que el principio sea oscuro de verdad.
    void Apply(float u)
    {
        float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.1f, 1f, u));
        Color fog = Color.Lerp(NightFog, DawnFog, k);
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = Mathf.Lerp(0.032f, 0.013f, k);
        RenderSettings.ambientLight = Color.Lerp(NightAmbient, DawnAmbient, k);
        if (camera != null) { camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = fog; }
        if (sun != null)
        {
            sun.color = Color.Lerp(NightLight, DawnLight, k);
            sun.intensity = Mathf.Lerp(0.3f, 1.25f, k);
            sun.transform.rotation = Quaternion.Euler(Mathf.Lerp(30f, 7f, k), 180f, 0f);   // mira hacia la cámara: el sol sale al frente
        }
    }

    void MoveCamera(float u)
    {
        if (cam == null) return;
        float ease = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(u * 1.04f));       // arranca y se detiene con suavidad
        Vector3 p = Vector3.Lerp(startPoint, endPoint, ease);
        p.x += Mathf.Sin(ease * 5f) * 0.9f;                                   // el camino no es recto
        float speed = u < 0.98f ? 1f : 0f;
        p.y += Mathf.Sin(t * 5.2f) * 0.035f * speed;                          // balanceo al andar
        cam.position = p;
        float yaw = Mathf.Cos(ease * 5f) * 4f + Mathf.Sin(t * 0.6f) * 0.8f;   // mira siguiendo la curva, con un leve temblor
        float pitch = Mathf.Lerp(3f, -2f, u) + Mathf.Sin(t * 2.6f) * 0.5f * speed;
        cam.rotation = Quaternion.Euler(pitch, yaw, 0f);
    }

    void UpdateBreathing()
    {
        if (breathing == null || cam == null || t < breathingStart) return;
        if (!breathing.isPlaying) breathing.Play();
        breathing.transform.position = cam.position - cam.forward * 1.3f;   // siempre justo detrás
        breathing.volume = Mathf.MoveTowards(breathing.volume, 1f, Time.deltaTime / 6f);
    }

    // ---------------------------------------------------------------- Dibujo

    void OnGUI()
    {
        GUI.depth = 5;
        float W = Screen.width, H = Screen.height, s = H / 1080f;
        if (Event.current.type != EventType.Repaint) return;

        if (phase == Phase.Walk)
        {
            // Fundido inicial desde negro y fundido de los subtítulos
            float fadeIn = 1f - Mathf.Clamp01(t / 3.5f);
            if (fadeIn > 0f) { GUI.color = new Color(0, 0, 0, fadeIn); GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture); }
            DrawSubtitles(W, H, s);
        }
        else
        {
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            if (phase == Phase.Credits)
            {
                float a = Mathf.Clamp01((t - phaseStart) / 1.5f);
                CreditsRoll.Draw(new Rect(W * 0.2f, H * 0.06f, W * 0.6f, H * 0.88f), creditsScroll, s, a);
                DrawSkipHint(W, H, s, a);
            }
        }
        GUI.color = Color.white;
    }

    void DrawSubtitles(float W, float H, float s)
    {
        if (subStyle == null) subStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic };
        subStyle.fontSize = Mathf.RoundToInt(34f * s);
        foreach (Subtitle sub in subtitles)
        {
            float local = t - sub.time;
            if (local < 0f || local > sub.duration) continue;
            float a = Mathf.Clamp01(Mathf.Min(local, sub.duration - local) / 0.8f);   // entra y sale suave
            bool last = sub.text == "No mires atrás.";
            subStyle.normal.textColor = last ? new Color(0.95f, 0.55f, 0.5f, a) : new Color(0.95f, 0.93f, 0.86f, a);
            HudOverlay.ShadowLabel(new Rect(0, H * 0.78f, W, 60f * s), sub.text, subStyle);
        }
    }

    void DrawSkipHint(float W, float H, float s, float a)
    {
        GUIStyle hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(16f * s) };
        hint.normal.textColor = new Color(0.8f, 0.78f, 0.7f, 0.4f * a);
        GUI.Label(new Rect(0, H - 44f * s, W, 30f * s), "ESC  ·  Volver al menú", hint);
    }
}
