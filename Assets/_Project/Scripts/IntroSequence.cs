using UnityEngine;

// Inicio del Nivel 1: el jugador "abre los ojos" (párpados que se abren a trompicones, con la vista aún oscura)
// y después aparece una tarjeta con los controles que desaparece sola cuando ya los ha usado.
// Va en un objeto vacío de la escena. Si el nivel se reinicia tras morir, solo hace un parpadeo corto y no repite el tutorial.
public class IntroSequence : MonoBehaviour
{
    public Transform player;                  // para detectar que se ha movido (se busca por tag si está vacío)
    public float blackTime = 1.6f;            // negro total al empezar
    public float openingTime = 6f;            // duración de los parpadeos hasta tener los ojos abiertos
    public bool showTutorial = true;

    static bool playedBefore;                 // sobrevive a la recarga de escena

    // Partida nueva desde el menú: la apertura de ojos vuelve a ser la completa
    public static void ResetForNewGame() => playedBefore = false;

    enum Phase { Eyes, Tutorial, Done }
    Phase phase = Phase.Eyes;
    float t;
    float openingTotal;
    float tutorialTime;
    Vector3 startPos;
    float startYaw;
    bool moved, looked;
    float doneAt = -1f;
    Texture2D solid, softEdge;
    GUIStyle title, body, key, small;

    void Start()
    {
        if (playedBefore) { blackTime = 0.3f; openingTime = 1.6f; showTutorial = false; }
        playedBefore = true;
        openingTotal = blackTime + openingTime;
        if (player == null) { GameObject p = GameObject.FindWithTag("Player"); if (p != null) player = p.transform; }
        UIState.IntroPlaying = true;

        solid = new Texture2D(1, 1);
        solid.SetPixel(0, 0, Color.white);
        solid.Apply();
        // Borde suave del párpado: opaco arriba, transparente abajo
        softEdge = new Texture2D(1, 64) { wrapMode = TextureWrapMode.Clamp };
        for (int i = 0; i < 64; i++) softEdge.SetPixel(0, i, new Color(0, 0, 0, 1f - Mathf.SmoothStep(0f, 1f, i / 63f)));
        softEdge.Apply();
    }

    void OnDestroy()
    {
        UIState.IntroPlaying = false;
    }

    void Update()
    {
        t += Time.deltaTime;
        if (phase == Phase.Eyes && t >= openingTotal) BeginTutorial();
        else if (phase == Phase.Tutorial) UpdateTutorial();
    }

    void BeginTutorial()
    {
        UIState.IntroPlaying = false;
        if (!showTutorial) { phase = Phase.Done; return; }
        phase = Phase.Tutorial;
        tutorialTime = 0f;
        if (player != null) { startPos = player.position; startYaw = player.eulerAngles.y; }
    }

    void UpdateTutorial()
    {
        tutorialTime += Time.deltaTime;
        if (player != null)
        {
            Vector3 d = player.position - startPos;
            d.y = 0;
            if (d.magnitude > 1.5f) moved = true;
            if (Mathf.Abs(Mathf.DeltaAngle(startYaw, player.eulerAngles.y)) > 35f) looked = true;
        }
        // Se oculta 3 s después de haber probado movimiento y cámara (o tras 30 s como máximo)
        if (doneAt < 0f && ((moved && looked && tutorialTime > 6f) || tutorialTime > 30f)) doneAt = tutorialTime + 3f;
        if (doneAt >= 0f && tutorialTime > doneAt + 1f) phase = Phase.Done;
    }

    void OnGUI()
    {
        if (phase == Phase.Done || Event.current.type != EventType.Repaint) return;
        if (phase == Phase.Eyes) DrawEyes();
        else DrawTutorial();
    }

    // ---------------------------------------------------------------- Párpados

    void DrawEyes()
    {
        float W = Screen.width, H = Screen.height;
        float open = OpenAmount();                    // 0 = cerrado, 1 = abierto
        float lid = Mathf.Lerp(H * 0.5f, 0f, open);   // altura que cubre cada párpado
        float soft = H * 0.18f;                       // borde difuso del párpado

        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(0, 0, W, lid), solid);
        GUI.DrawTexture(new Rect(0, H - lid, W, lid), solid);
        if (open > 0f && open < 1f)
        {
            GUI.color = Color.white;
            // La textura es opaca en su parte baja: el párpado superior la usa girada (opaca arriba) y el inferior tal cual (opaca abajo)
            GUI.DrawTextureWithTexCoords(new Rect(0, lid, W, soft), softEdge, new Rect(0, 1, 1, -1));
            GUI.DrawTexture(new Rect(0, H - lid - soft, W, soft), softEdge);
        }
        // Velo oscuro que se aclara: la vista tarda en acostumbrarse
        float veil = Mathf.Clamp01(1f - Mathf.InverseLerp(blackTime, openingTotal, t)) * 0.85f;
        GUI.color = new Color(0.02f, 0.025f, 0.03f, veil);
        GUI.DrawTexture(new Rect(0, 0, W, H), solid);
        GUI.color = Color.white;
    }

    // Curva de apertura con parpadeos: se abre un poco, se cierra, se abre más, se cierra y por fin se abre del todo
    float OpenAmount()
    {
        if (t < blackTime) return 0f;
        float u = Mathf.Clamp01((t - blackTime) / openingTime);
        float[] keys = { 0f, 0.12f, 0.22f, 0.40f, 0.52f, 0.72f, 0.80f, 1f };
        float[] vals = { 0f, 0.28f, 0.04f, 0.55f, 0.18f, 0.80f, 0.62f, 1f };
        for (int i = 1; i < keys.Length; i++)
            if (u <= keys[i])
                return Mathf.SmoothStep(vals[i - 1], vals[i], Mathf.InverseLerp(keys[i - 1], keys[i], u));
        return 1f;
    }

    // ---------------------------------------------------------------- Tutorial

    void DrawTutorial()
    {
        float a = Mathf.Clamp01(tutorialTime / 1.2f);                                // aparece suave
        if (doneAt >= 0f) a *= 1f - Mathf.Clamp01((tutorialTime - doneAt) / 1f);     // se va suave
        if (a <= 0f) return;
        BuildStyles();

        float s = Screen.height / 1080f;
        float w = 440f * s, h = 330f * s;
        Rect box = new Rect(Screen.width - w - 40f * s, Screen.height * 0.5f - h * 0.5f, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.62f * a);
        GUI.DrawTexture(box, solid);
        GUI.color = new Color(0.78f, 0.74f, 0.6f, 0.55f * a);
        GUI.DrawTexture(new Rect(box.x, box.y, 3f * s, box.height), solid);          // filo lateral
        GUI.color = new Color(1f, 1f, 1f, a);

        float x = box.x + 26f * s, y = box.y + 18f * s;
        GUI.Label(new Rect(x, y, w - 40f * s, 36f * s), "CONTROLES", Sized(title, 26, s));
        y += 46f * s;
        Row(ref y, x, s, w, "W A S D", "Moverte", moved);
        Row(ref y, x, s, w, "RATÓN", "Mirar alrededor", looked);
        Row(ref y, x, s, w, "E", "Interactuar y recoger", false);
        Row(ref y, x, s, w, "F", "Linterna", false);
        Row(ref y, x, s, w, "TAB", "Inventario y notas", false);
        y += 8f * s;
        GUI.Label(new Rect(x, y, w - 50f * s, 60f * s), "Si oyes respiración, no te muevas. Pase lo que pase, no mires atrás.", Sized(small, 17, s));
        GUI.color = Color.white;
    }

    void Row(ref float y, float x, float s, float w, string keyText, string text, bool done)
    {
        GUI.Label(new Rect(x, y, 130f * s, 30f * s), keyText, Sized(key, 19, s));
        GUI.Label(new Rect(x + 140f * s, y, w - 190f * s, 30f * s), (done ? "✓ " : "") + text, Sized(body, 19, s));
        y += 36f * s;
    }

    void BuildStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        title.normal.textColor = new Color(0.88f, 0.84f, 0.7f);
        key = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
        key.normal.textColor = new Color(0.95f, 0.78f, 0.45f);
        body = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        body.normal.textColor = new Color(0.9f, 0.9f, 0.88f);
        small = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = true, fontStyle = FontStyle.Italic };
        small.normal.textColor = new Color(0.85f, 0.55f, 0.5f);
    }

    static GUIStyle Sized(GUIStyle st, int size, float scale)
    {
        st.fontSize = Mathf.RoundToInt(size * scale);
        return st;
    }
}
