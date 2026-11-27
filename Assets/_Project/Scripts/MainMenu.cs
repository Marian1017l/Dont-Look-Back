using UnityEngine;
using UnityEngine.InputSystem;

// Pantalla de título: logo que parpadea como una luz defectuosa, niebla que se mueve y menú Jugar / Créditos / Salir.
// Se maneja con ratón o con las flechas + Enter. Va en la escena MainMenu (la crea Don't Look Back > Crear menú principal y final).
public class MainMenu : MonoBehaviour
{
    public Texture2D logo;
    public AudioSource music;                 // ambiente de fondo; baja de volumen al pulsar Jugar

    // Zona útil del logo dentro de su PNG (tiene mucho margen transparente): x, y, ancho, alto en UV
    static readonly Rect LogoUv = new Rect(0.22f, 0.20f, 0.56f, 0.60f);
    static readonly string[] Items = { "JUGAR", "CRÉDITOS", "SALIR" };
    static readonly Color Gold = new Color(0.98f, 0.82f, 0.5f);

    enum View { Main, Credits }
    View view = View.Main;
    int selected;
    float t, creditsScroll;
    bool starting;
    float musicVolume;

    // Parpadeo del logo
    float nextFlicker = 2.5f, flickerUntil, flickerStep, logoAlpha = 1f;

    Texture2D fog, vignette;
    GUIStyle itemStyle, hintStyle;

    void Start()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        Time.timeScale = 1f;
        if (music != null) musicVolume = music.volume;
        fog = MakeFog(256);
        vignette = MakeVignette(128);
    }

    void Update()
    {
        t += Time.unscaledDeltaTime;
        UpdateFlicker();
        if (starting && music != null) music.volume = Mathf.MoveTowards(music.volume, 0f, Time.unscaledDeltaTime * musicVolume / 0.9f);
        if (starting || Keyboard.current == null) return;
        Keyboard k = Keyboard.current;

        if (view == View.Credits)
        {
            creditsScroll += Time.unscaledDeltaTime * 70f * (Screen.height / 1080f);
            if (creditsScroll > CreditsRoll.TotalHeight(Screen.height / 1080f) + Screen.height) creditsScroll = 0f;
            if (k.escapeKey.wasPressedThisFrame || k.backspaceKey.wasPressedThisFrame) view = View.Main;
            return;
        }
        if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) selected = (selected + 1) % Items.Length;
        if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) selected = (selected + Items.Length - 1) % Items.Length;
        if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) Activate(selected);
    }

    void UpdateFlicker()
    {
        if (t >= nextFlicker)
        {
            flickerUntil = t + Random.Range(0.3f, 0.7f);
            nextFlicker = t + Random.Range(3.5f, 8f);
        }
        if (t < flickerUntil)
        {
            if (t >= flickerStep) { logoAlpha = Random.value > 0.45f ? 1f : Random.Range(0.08f, 0.5f); flickerStep = t + 0.05f; }
        }
        else logoAlpha = Mathf.MoveTowards(logoAlpha, 1f, Time.unscaledDeltaTime * 6f);
    }

    void Activate(int index)
    {
        if (index == 0) { starting = true; GameFlow.StartNewGame(); }
        else if (index == 1) { view = View.Credits; creditsScroll = 0f; }
        else
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    // ---------------------------------------------------------------- Dibujo

    void OnGUI()
    {
        GUI.depth = 10;
        float W = Screen.width, H = Screen.height, s = H / 1080f;
        Rect full = new Rect(0, 0, W, H);
        Event e = Event.current;

        if (e.type == EventType.Repaint)
        {
            GUI.color = new Color(0.02f, 0.025f, 0.03f, 1f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
            DrawFog(full);
            if (view == View.Main) DrawLogo(W, H);
            else DrawCredits(W, H, s);
            GUI.color = Color.white;
            GUI.DrawTexture(full, vignette);   // bordes oscuros
        }
        if (view == View.Main) DrawMenu(W, H, s, e);
        else if (e.type == EventType.Repaint) DrawHint("ESC  ·  Volver", W, H, s);
    }

    void DrawFog(Rect full)
    {
        float scroll = t * 0.012f;
        GUI.color = new Color(0.55f, 0.62f, 0.58f, 0.13f);
        GUI.DrawTextureWithTexCoords(full, fog, new Rect(scroll, scroll * 0.4f, 2.2f, 1.3f));
        GUI.color = new Color(0.7f, 0.66f, 0.6f, 0.08f);
        GUI.DrawTextureWithTexCoords(full, fog, new Rect(-scroll * 0.7f + 0.3f, scroll * 0.2f + 0.5f, 3.4f, 2.0f));
        GUI.color = Color.white;
    }

    void DrawLogo(float W, float H)
    {
        if (logo == null) return;
        float h = H * 0.66f, w = h * (LogoUv.width / LogoUv.height);
        h *= 1f + 0.008f * Mathf.Sin(t * 0.7f);   // respira apenas
        w = h * (LogoUv.width / LogoUv.height);
        Rect r = new Rect(W * 0.30f - w / 2f, H * 0.5f - h / 2f, w, h);
        // Resplandor rojizo detrás, como luz de emergencia que se cuela
        Rect g = new Rect(r.center.x - w * 0.54f, r.center.y - h * 0.54f, w * 1.08f, h * 1.08f);
        GUI.color = new Color(0.8f, 0.1f, 0.06f, 0.16f * logoAlpha);
        GUI.DrawTextureWithTexCoords(g, logo, LogoUv);
        GUI.color = new Color(1f, 1f, 1f, 0.97f * logoAlpha);
        GUI.DrawTextureWithTexCoords(r, logo, LogoUv);
        GUI.color = Color.white;
    }

    void DrawMenu(float W, float H, float s, Event e)
    {
        if (itemStyle == null)
        {
            itemStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
            hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        itemStyle.fontSize = Mathf.RoundToInt(44f * s);

        float x = W * 0.62f, rowH = 86f * s, y0 = H * 0.5f - rowH * 1.5f;
        for (int i = 0; i < Items.Length; i++)
        {
            Rect row = new Rect(x - 30f * s, y0 + i * rowH, W * 0.3f, rowH);
            if (!starting && view == View.Main)
            {
                if (e.type == EventType.MouseMove || e.type == EventType.Repaint)
                    if (row.Contains(e.mousePosition) && selected != i && (e.type == EventType.MouseMove || Mouse.current != null && Mouse.current.delta.ReadValue().sqrMagnitude > 0f)) selected = i;
                if (e.type == EventType.MouseDown && e.button == 0 && row.Contains(e.mousePosition)) { selected = i; Activate(i); e.Use(); return; }
            }
            if (e.type != EventType.Repaint) continue;

            bool on = i == selected;
            float shift = on ? 14f * s : 0f;
            if (on)
            {
                GUI.color = new Color(Gold.r, Gold.g, Gold.b, 0.85f);
                GUI.DrawTexture(new Rect(x - 30f * s, row.y + rowH * 0.25f, 3f * s, rowH * 0.5f), Texture2D.whiteTexture);   // marcador
                GUI.color = new Color(1f, 1f, 1f, 0.05f);
                GUI.DrawTexture(new Rect(x - 30f * s, row.y + rowH * 0.1f, row.width, rowH * 0.8f), Texture2D.whiteTexture);   // resalte tenue
            }
            itemStyle.normal.textColor = on ? Gold : new Color(0.7f, 0.68f, 0.62f, 0.85f);
            GUI.color = Color.white;
            HudOverlay.ShadowLabel(new Rect(x + shift, row.y, row.width, rowH), Items[i], itemStyle);
        }
        if (e.type == EventType.Repaint) DrawHint("↑ ↓  elegir   ·   ENTER  confirmar", W, H, s);
    }

    void DrawCredits(float W, float H, float s)
    {
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
        GUI.color = Color.white;
        CreditsRoll.Draw(new Rect(W * 0.2f, H * 0.08f, W * 0.6f, H * 0.84f), creditsScroll, s, 1f);
    }

    void DrawHint(string text, float W, float H, float s)
    {
        if (hintStyle == null) hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        hintStyle.fontSize = Mathf.RoundToInt(18f * s);
        hintStyle.normal.textColor = new Color(0.8f, 0.78f, 0.7f, 0.55f);
        GUI.color = Color.white;
        HudOverlay.ShadowLabel(new Rect(0, H - 60f * s, W, 40f * s), text, hintStyle);
    }

    // ---------------------------------------------------------------- Texturas generadas por código

    // Ruido de nubes que se repite sin costuras (mezcla de cuatro desplazamientos)
    static Texture2D MakeFog(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (float)x / size, fy = (float)y / size;
                float v = (Tile(x, y, size) * (1f - fx) * (1f - fy) + Tile(x + size, y, size) * fx * (1f - fy)
                         + Tile(x, y + size, size) * (1f - fx) * fy + Tile(x + size, y + size, size) * fx * fy);
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(v * v * 1.6f));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static float Tile(int x, int y, int size)
    {
        float f = 4f / size, amp = 0.6f, sum = 0f;
        for (int o = 0; o < 3; o++)
        {
            sum += Mathf.PerlinNoise(x * f + 11f, y * f + 7f) * amp;
            f *= 2f; amp *= 0.5f;
        }
        return sum / 1.05f;
    }

    static Texture2D MakeVignette(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x / (size - 1f) - 0.5f) * 2f, dy = (y / (size - 1f) - 0.5f) * 2f;
                float d = Mathf.Sqrt(dx * dx + dy * dy) / 1.414f;
                tex.SetPixel(x, y, new Color(0f, 0f, 0f, Mathf.SmoothStep(0.15f, 1f, d) * 0.9f));
            }
        tex.Apply();
        return tex;
    }
}
