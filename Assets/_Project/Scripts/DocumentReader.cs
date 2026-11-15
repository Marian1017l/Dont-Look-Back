using UnityEngine;
using UnityEngine.InputSystem;

// Lector de documentos: muestra una hoja de papel (textura de PaperTexture) sobre el juego oscurecido,
// con la tipografía y el membrete del tipo de documento. Se usa con DocumentReader.Show(...).
// Se crea solo la primera vez que se necesita; no hay que ponerlo en la escena.
// Las tipografías se buscan entre las del sistema (máquina de escribir y manuscrita) y, si no existen, usa la de Unity.
public class DocumentReader : MonoBehaviour
{
    static DocumentReader instance;

    string title, body, reference, date, author;
    DocumentStyle style;
    bool showing, fontsLoaded;
    int openedFrame;
    float openedTime;
    Font typewriter, handwriting;

    public static void Show(string title, string body, DocumentStyle style = DocumentStyle.PatientNote, string reference = "", string date = "", string author = "")
    {
        if (instance == null) instance = new GameObject("DocumentReader").AddComponent<DocumentReader>();
        instance.Open(title, body, style, reference, date, author);
    }

    void Open(string newTitle, string newBody, DocumentStyle newStyle, string newReference, string newDate, string newAuthor)
    {
        title = newTitle;
        body = newBody;
        style = newStyle;
        reference = newReference;
        date = newDate;
        author = newAuthor;
        showing = true;
        openedFrame = Time.frameCount;   // evita cerrarlo con la misma pulsación de E que lo abrió
        openedTime = Time.unscaledTime;
        UIState.OpenPanel();
    }

    void Close()
    {
        showing = false;
        UIState.ClosePanel();
    }

    void Update()
    {
        if (!showing || Time.frameCount == openedFrame || Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void LoadFonts()
    {
        if (fontsLoaded) return;
        fontsLoaded = true;
        typewriter = Find("Courier New", "Consolas", "Lucida Console");
        handwriting = Find("Ink Free", "Segoe Script", "Segoe Print", "Bradley Hand ITC", "Comic Sans MS");
    }

    public static Font Find(params string[] names)
    {
        string[] installed = Font.GetOSInstalledFontNames();
        foreach (string n in names)
            if (System.Array.IndexOf(installed, n) >= 0) return Font.CreateDynamicFontFromOSFont(n, 32);
        return null;   // sin la tipografía: se usa la de Unity
    }

    void OnGUI()
    {
        if (!showing) return;
        LoadFonts();

        float t = Mathf.Clamp01((Time.unscaledTime - openedTime) / 0.25f);
        float ease = 1f - (1f - t) * (1f - t);

        GUI.color = new Color(0f, 0f, 0f, 0.8f * ease);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

        float ph = Screen.height * 0.88f, pw = ph * PaperTexture.Width / PaperTexture.Height;
        float px = (Screen.width - pw) / 2f, py = (Screen.height - ph) / 2f;
        Texture2D paper = PaperTexture.Get(style);

        Matrix4x4 saved = GUI.matrix;
        Vector2 pivot = new Vector2(Screen.width / 2f, Screen.height / 2f);
        GUIUtility.ScaleAroundPivot(Vector2.one * Mathf.Lerp(0.94f, 1f, ease), pivot);
        GUIUtility.RotateAroundPivot(style == DocumentStyle.PatientNote ? -1.8f : style == DocumentStyle.StaffReport ? 0.5f : 0f, pivot);

        // Sombra (la misma hoja en negro, desplazada) y hoja
        GUI.color = new Color(0f, 0f, 0f, 0.55f * ease);
        GUI.DrawTexture(new Rect(px + pw * 0.014f, py + ph * 0.016f, pw, ph), paper);
        GUI.color = new Color(1f, 1f, 1f, ease);
        GUI.DrawTexture(new Rect(px, py, pw, ph), paper);

        DrawContent(px, py, pw, ph, ease);

        GUI.matrix = saved;
        GUI.color = Color.white;
        GUIStyle hint = Style(null, Mathf.RoundToInt(Screen.height * 0.022f), FontStyle.Normal, TextAnchor.MiddleCenter, new Color(0.8f, 0.8f, 0.78f, 0.6f * ease));
        GUI.Label(new Rect(0, Screen.height - Screen.height * 0.05f, Screen.width, Screen.height * 0.04f), "E  ·  cerrar", hint);

        Event e = Event.current;
        if (e.type == EventType.MouseDown && Time.frameCount != openedFrame) Close();
    }

    void DrawContent(float px, float py, float pw, float ph, float ease)
    {
        bool patient = style == DocumentStyle.PatientNote;
        Font font = patient ? handwriting : typewriter;
        Color ink = patient ? new Color(0.1f, 0.1f, 0.2f) : new Color(0.1f, 0.09f, 0.08f);
        ink.a = 0.93f * ease;
        float left = px + pw * 0.12f, width = pw * 0.76f;

        float bodyTop;
        if (style == DocumentStyle.MedicalRecord)
        {
            Label(left, py + ph * 0.045f, width, ph * 0.04f, "INSTITUTO PSIQUIÁTRICO MONTE ALTO", font, ph * 0.026f, FontStyle.Bold, TextAnchor.MiddleCenter, ink);
            Label(left, py + ph * 0.085f, width, ph * 0.03f, "EXPEDIENTE CLÍNICO · SERVICIO DE PSIQUIATRÍA", font, ph * 0.017f, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
            Rule(left, py + ph * 0.125f, width, 2f, ink);
            if (!string.IsNullOrEmpty(reference)) Label(left, py + ph * 0.14f, width, ph * 0.03f, "N.º " + reference, font, ph * 0.019f, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            if (!string.IsNullOrEmpty(date)) Label(left, py + ph * 0.14f, width, ph * 0.03f, "Fecha: " + date, font, ph * 0.019f, FontStyle.Normal, TextAnchor.MiddleRight, ink);
            Label(left, py + ph * 0.18f, width, ph * 0.045f, title.ToUpper(), font, ph * 0.024f, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            Rule(left, py + ph * 0.235f, width, 1f, ink);
            Stamp(px + pw * 0.74f, py + ph * 0.2f, pw, ph, ease);   // a la derecha del título, sin tapar el membrete
            bodyTop = 0.26f;
        }
        else if (style == DocumentStyle.StaffReport)
        {
            Label(left, py + ph * 0.05f, width, ph * 0.035f, "INSTITUTO PSIQUIÁTRICO MONTE ALTO", font, ph * 0.02f, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            Rule(left, py + ph * 0.095f, width, 1.5f, ink);
            string line = (string.IsNullOrEmpty(reference) ? "" : "Ref. " + reference) + (string.IsNullOrEmpty(date) ? "" : "   " + date);
            Label(left, py + ph * 0.105f, width, ph * 0.03f, line, font, ph * 0.017f, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            Label(left, py + ph * 0.105f, width, ph * 0.03f, "INFORME INTERNO", font, ph * 0.017f, FontStyle.Bold, TextAnchor.MiddleRight, ink);
            Label(left, py + ph * 0.15f, width, ph * 0.05f, title.ToUpper(), font, ph * 0.026f, FontStyle.Bold, TextAnchor.MiddleLeft, ink);
            Rule(left, py + ph * 0.205f, width, 1f, ink);
            bodyTop = 0.23f;
        }
        else
        {
            if (!string.IsNullOrEmpty(date)) Label(left, py + ph * 0.06f, width, ph * 0.035f, date, font, ph * 0.024f, FontStyle.Normal, TextAnchor.MiddleRight, ink);
            Label(left, py + ph * 0.085f, width, ph * 0.06f, title, font, ph * 0.036f, FontStyle.Normal, TextAnchor.MiddleLeft, ink);
            Rule(left, py + ph * 0.147f, width * 0.7f, 2.5f, ink);
            Rule(left + 6f, py + ph * 0.153f, width * 0.55f, 1.2f, ink);
            bodyTop = 0.18f;
        }

        // Cuerpo: se reduce la letra hasta que todo el texto cabe en la hoja (sin scroll)
        Rect area = new Rect(left, py + ph * bodyTop, width, ph * (0.9f - bodyTop));
        int size = Mathf.RoundToInt(ph * (patient ? 0.034f : 0.024f));
        GUIStyle s = Style(font, size, FontStyle.Normal, TextAnchor.UpperLeft, ink);
        s.wordWrap = true;
        int min = Mathf.RoundToInt(ph * 0.016f);
        while (size > min && s.CalcHeight(new GUIContent(body), area.width) > area.height) s.fontSize = --size;
        GUI.color = Color.white;
        GUI.Label(area, body, s);

        // Pie: firma
        if (!string.IsNullOrEmpty(author) && !patient)
        {
            Rule(px + pw * 0.44f, py + ph * 0.925f, pw * 0.44f, 1f, ink);
            Label(px + pw * 0.44f, py + ph * 0.93f, pw * 0.44f, ph * 0.035f, author, font, ph * 0.015f, FontStyle.Normal, TextAnchor.MiddleCenter, ink);
        }
    }

    // Sello rojo desvaído, torcido, sobre el membrete
    void Stamp(float x, float y, float pw, float ph, float ease)
    {
        Matrix4x4 m = GUI.matrix;
        GUIUtility.RotateAroundPivot(-14f, new Vector2(x, y));
        Color red = new Color(0.6f, 0.1f, 0.08f, 0.6f * ease);
        float w = pw * 0.3f, h = ph * 0.05f;
        Rect r = new Rect(x - w / 2f, y - h / 2f, w, h);
        Rule(r.x, r.y, r.width, 2f, red);
        Rule(r.x, r.yMax, r.width, 2f, red);
        GUI.color = red;
        GUI.DrawTexture(new Rect(r.x, r.y, 2f, r.height), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(r.xMax, r.y, 2f, r.height), Texture2D.whiteTexture);
        Label(r.x, r.y, r.width, r.height, "CONFIDENCIAL", typewriter, ph * 0.02f, FontStyle.Bold, TextAnchor.MiddleCenter, red);
        GUI.color = Color.white;
        GUI.matrix = m;
    }

    static void Rule(float x, float y, float w, float thickness, Color color)
    {
        GUI.color = color;
        GUI.DrawTexture(new Rect(x, y, w, Mathf.Max(thickness, Screen.height * 0.0024f)), Texture2D.whiteTexture);   // grosor mínimo: las líneas finas se cortan con la hoja inclinada
        GUI.color = Color.white;
    }

    static void Label(float x, float y, float w, float h, string text, Font font, float size, FontStyle fontStyle, TextAnchor anchor, Color color)
    {
        GUI.color = Color.white;
        GUI.Label(new Rect(x, y, w, h), text, Style(font, Mathf.RoundToInt(size), fontStyle, anchor, color));
    }

    static GUIStyle Style(Font font, int size, FontStyle fontStyle, TextAnchor anchor, Color color)
    {
        GUIStyle s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fontStyle, alignment = anchor, richText = false };
        if (font != null) s.font = font;
        // El cursor queda libre sobre el texto: sin esto IMGUI usa el color "hover" (casi blanco) bajo el puntero
        s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = color;
        s.onNormal.textColor = s.onHover.textColor = s.onActive.textColor = s.onFocused.textColor = color;
        return s;
    }

    void OnDisable()
    {
        if (showing) UIState.ClosePanel();
    }
}
