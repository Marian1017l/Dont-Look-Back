using UnityEngine;

// HUD fijo de los niveles: arriba a la izquierda las barras de vida (corazón) y de linterna, abajo a la derecha
// un recordatorio visual del inventario (TAB). Solo es visual: el inventario se abre con la tecla, no con el ratón.
// Además ofrece ayudas de dibujo (tecla, texto con sombra, aviso) que usan también los avisos de interacción.
// Va en un objeto vacío dentro de _Systems; los iconos se asignan en el Inspector.
public class HudOverlay : MonoBehaviour
{
    public Texture2D heartIcon;
    public Texture2D flashlightIcon;
    public PlayerHealth health;               // se busca solo si está vacío
    public Flashlight flashlight;             // se busca solo si está vacío
    public string inventoryKey = "TAB";

    // Zona útil de cada icono dentro de su imagen (los PNG tienen mucho margen transparente): x, y, ancho, alto en UV
    static readonly Rect HeartUv = new Rect(0.27f, 0.22f, 0.46f, 0.58f);
    static readonly Rect LightUv = new Rect(0.15f, 0.32f, 0.72f, 0.36f);

    static readonly Color Gold = new Color(0.95f, 0.78f, 0.45f);

    float shownHealth = 1f, ghostHealth = 1f, shownCharge = 1f;
    float beatPhase;
    GUIStyle keyStyle, labelStyle;

    void Start()
    {
        if (health == null) health = PlayerHealth.Instance != null ? PlayerHealth.Instance : FindFirstObjectByType<PlayerHealth>();
        if (flashlight == null) flashlight = FindFirstObjectByType<Flashlight>();
        if (health != null) shownHealth = ghostHealth = health.Health / health.maxHealth;
        if (flashlight != null) shownCharge = flashlight.Charge;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        if (health != null)
        {
            float target = Mathf.Clamp01(health.Health / health.maxHealth);
            shownHealth = Mathf.MoveTowards(shownHealth, target, dt * 1.2f);
            // La "estela" clara se queda atrás al perder vida y alcanza a la barra al curar
            ghostHealth = target >= ghostHealth ? target : Mathf.MoveTowards(ghostHealth, target, dt * 0.2f);
            float bpm = Mathf.Lerp(55f, 140f, 1f - target);
            beatPhase += dt * bpm / 60f * Mathf.PI * 2f;
        }
        if (flashlight != null) shownCharge = Mathf.MoveTowards(shownCharge, flashlight.Charge, dt * 0.5f);
    }

    void OnGUI()
    {
        GUI.depth = 1;   // por debajo de la apertura de ojos y de los fundidos
        if (Event.current.type != EventType.Repaint) return;
        if (UIState.PanelOpen || UIState.IntroPlaying) return;
        if (health != null && health.IsDead) return;
        BuildStyles();

        float s = Screen.height / 1080f;
        float x = 34f * s, y = 30f * s;
        if (health != null) { DrawHealthRow(x, y, s); y += 70f * s; }
        if (flashlight != null && flashlight.hasFlashlight) DrawFlashlightRow(x, y, s);
        DrawInventoryReminder(s);
        GUI.color = Color.white;
    }

    // ---------------------------------------------------------------- Filas

    void DrawHealthRow(float x, float y, float s)
    {
        float ratio = Mathf.Clamp01(health.Health / health.maxHealth);
        bool low = ratio <= 0.5f;

        // El corazón late; más rápido y rojo cuanto menos vida
        float beat = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(beatPhase)), 8f) * 0.14f;
        float h = 58f * s * (1f + beat), w = h * (HeartUv.width / HeartUv.height) * 1f;
        Rect slot = new Rect(x, y, 70f * s, 58f * s);
        if (heartIcon != null)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.95f);
            GUI.DrawTextureWithTexCoords(new Rect(slot.center.x - w / 2f, slot.center.y - h / 2f, w, h), heartIcon, HeartUv);
        }

        Rect bar = new Rect(x + 82f * s, y + 23f * s, 250f * s, 12f * s);
        Color fill = low ? Color.Lerp(new Color(0.85f, 0.1f, 0.08f), new Color(0.6f, 0.05f, 0.05f), 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f))
                         : new Color(0.72f, 0.14f, 0.12f);
        DrawBar(bar, shownHealth, ghostHealth, fill, new Color(0.95f, 0.55f, 0.5f), s);
    }

    void DrawFlashlightRow(float x, float y, float s)
    {
        float charge = flashlight.Charge;
        bool low = charge < flashlight.lowCharge;
        float alpha = flashlight.IsOn ? 0.95f : 0.45f;   // atenuada si está apagada
        if (low && flashlight.IsOn) alpha *= 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 9f);

        float h = 28f * s, w = h * (LightUv.width / LightUv.height);
        Rect slot = new Rect(x, y, 70f * s, 58f * s);
        if (flashlightIcon != null)
        {
            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.DrawTextureWithTexCoords(new Rect(slot.center.x - w / 2f, slot.center.y - h / 2f, w, h), flashlightIcon, LightUv);
        }

        Rect bar = new Rect(x + 82f * s, y + 23f * s, 250f * s, 12f * s);
        Color fill = low ? new Color(0.85f, 0.2f, 0.12f) : new Color(0.9f, 0.82f, 0.55f);
        DrawBar(bar, shownCharge, -1f, fill, Color.clear, s);
    }

    // Barra con marco fino y fondo oscuro; 'ghost' (opcional) es la estela clara que queda al perder
    static void DrawBar(Rect r, float value, float ghost, Color fill, Color ghostColor, float s)
    {
        float b = Mathf.Max(1f, 1.5f * s);
        GUI.color = new Color(0.9f, 0.88f, 0.8f, 0.35f);
        GUI.DrawTexture(new Rect(r.x - b, r.y - b, r.width + 2f * b, r.height + 2f * b), Texture2D.whiteTexture);
        GUI.color = new Color(0.03f, 0.03f, 0.035f, 0.85f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        if (ghost > value)
        {
            GUI.color = new Color(ghostColor.r, ghostColor.g, ghostColor.b, 0.55f);
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * ghost, r.height), Texture2D.whiteTexture);
        }
        GUI.color = fill;
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * value, r.height), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 1f, 1f, 0.12f);   // brillo suave en la mitad superior
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * value, r.height * 0.4f), Texture2D.whiteTexture);
    }

    void DrawInventoryReminder(float s)
    {
        float w = 150f * s, h = 34f * s;
        float x = Screen.width - w - 34f * s, y = Screen.height - h - 30f * s;
        KeyCap(new Rect(x, y, 56f * s, h), inventoryKey, 0.8f, s);
        labelStyle.fontSize = Mathf.RoundToInt(17f * s);
        labelStyle.normal.textColor = new Color(0.9f, 0.88f, 0.8f, 0.7f);
        ShadowLabel(new Rect(x + 66f * s, y, w - 66f * s, h), "INVENTARIO", labelStyle);
    }

    // ---------------------------------------------------------------- Ayudas de dibujo (también para los avisos de interacción)

    static GUIStyle sKey, sLabel;

    void BuildStyles()
    {
        if (keyStyle != null) return;
        keyStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
    }

    // Tecla dibujada como una pastilla con marco: "E", "TAB"...
    public static void KeyCap(Rect r, string key, float alpha, float s)
    {
        if (sKey == null) sKey = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        float b = Mathf.Max(1f, 1.5f * s);
        Color c = GUI.color;
        GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f * alpha);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(0.05f, 0.05f, 0.055f, 0.92f * alpha);
        GUI.DrawTexture(new Rect(r.x + b, r.y + b, r.width - 2f * b, r.height - 2f * b), Texture2D.whiteTexture);
        sKey.fontSize = Mathf.RoundToInt(r.height * 0.5f);
        sKey.normal.textColor = new Color(Gold.r, Gold.g, Gold.b, alpha);
        GUI.color = Color.white;
        GUI.Label(r, key, sKey);
        GUI.color = c;
    }

    // Texto con sombra para que se lea sobre cualquier fondo
    public static void ShadowLabel(Rect r, string text, GUIStyle style)
    {
        Color keep = style.normal.textColor, c = GUI.color;
        style.normal.textColor = new Color(0f, 0f, 0f, keep.a * 0.85f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        style.normal.textColor = keep;
        GUI.Label(r, text, style);
        GUI.color = c;
    }

    // Aviso centrado en una franja oscura: mensajes como "Está cerrado" o "La linterna ya está llena"
    public static void Toast(float centerY, string text, float alpha)
    {
        if (alpha <= 0f) return;
        if (sLabel == null) sLabel = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        float s = Screen.height / 1080f;
        sLabel.fontSize = Mathf.RoundToInt(24f * s);
        sLabel.normal.textColor = new Color(0.95f, 0.93f, 0.86f, alpha);
        float w = sLabel.CalcSize(new GUIContent(text)).x + 60f * s, h = 46f * s;
        Rect r = new Rect((Screen.width - w) / 2f, centerY - h / 2f, w, h);
        Color c = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f * alpha);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.5f * alpha);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width, Mathf.Max(1f, 1.5f * s)), Texture2D.whiteTexture);   // filo dorado superior
        GUI.color = Color.white;
        ShadowLabel(r, text, sLabel);
        GUI.color = c;
    }
}
