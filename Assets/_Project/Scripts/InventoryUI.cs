using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// Inventario del jugador. Se abre con TAB o I y tiene dos pestañas:
//  - Objetos: rejilla con lo que llevas (linterna, llaves...) y su descripción.
//  - Documentos: todo lo que has leído; se puede releer.
// Además muestra la salud y la batería, y un aviso en una esquina cada vez que recoges algo.
// El juego NO se pausa (es terror): por eso no se puede abrir con una amenaza activa, y se cierra si te golpean.
// Se crea solo al iniciar el juego; no hay que ponerlo en la escena. Los datos viven en Inventory.
public class InventoryUI : MonoBehaviour
{
    static InventoryUI instance;
    static bool tipShown;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap()
    {
        if (instance != null) return;
        GameObject go = new GameObject("InventoryUI");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<InventoryUI>();
    }

    enum Tab { Items, Documents }
    struct Entry { public string id; public ItemInfo info; }

    const float PanelW = 1500f, PanelH = 860f;
    const int Cols = 4, Rows = 3, DocRows = 7;

    // Paleta: verdes de hospital desaturados, beige envejecido y rojo solo para el peligro
    static readonly Color PanelColor = new Color(0.052f, 0.066f, 0.062f, 0.98f);
    static readonly Color HeaderColor = new Color(0.085f, 0.108f, 0.1f, 1f);
    static readonly Color SlotColor = new Color(0.1f, 0.125f, 0.115f, 1f);
    static readonly Color SlotHover = new Color(0.14f, 0.175f, 0.16f, 1f);
    static readonly Color SlotEmpty = new Color(0.065f, 0.08f, 0.075f, 1f);
    static readonly Color DetailColor = new Color(0.04f, 0.05f, 0.047f, 0.95f);
    static readonly Color Accent = new Color(0.5f, 0.72f, 0.6f, 1f);
    static readonly Color AccentFill = new Color(0.14f, 0.23f, 0.19f, 1f);
    static readonly Color Bright = new Color(0.93f, 0.9f, 0.78f, 1f);
    static readonly Color Beige = new Color(0.82f, 0.78f, 0.66f, 1f);
    static readonly Color Dim = new Color(0.58f, 0.58f, 0.52f, 1f);
    static readonly Color Faint = new Color(0.38f, 0.4f, 0.37f, 1f);
    static readonly Color Line = new Color(0.62f, 0.58f, 0.46f, 0.42f);
    static readonly Color Danger = new Color(0.78f, 0.17f, 0.12f, 1f);

    bool open;
    Tab tab;
    int selectedItem, selectedDoc, docScroll;
    float openedTime, healthAtOpen;
    readonly List<Entry> entries = new List<Entry>();
    Flashlight flashlight;
    PlayerController player;

    // Aviso de esquina
    bool toastPending;
    float toastAt, toastStart = -99f;
    string toastLabel, toastName, toastHint;
    Texture2D toastIcon;
    Color toastAccent = Color.white;

    // Dibujo
    GUIStyle label;
    Font typewriter, handwriting;
    bool fontsLoaded;
    float s, alpha = 1f;
    Vector2 origin;

    // ---------------------------------------------------------------- Ciclo de vida

    void OnEnable()
    {
        Inventory.ItemAdded += OnItemAdded;
        Inventory.DocumentAdded += OnDocumentAdded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        Inventory.ItemAdded -= OnItemAdded;
        Inventory.DocumentAdded -= OnDocumentAdded;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (open) { open = false; UIState.ClosePanel(); }
    }

    void Start() => CheckTip();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (open) Close();
        toastPending = false;
        toastStart = -99f;
        CheckTip();
    }

    // Una sola vez por partida: avisa de que existe el inventario
    void CheckTip()
    {
        if (tipShown || FindFirstObjectByType<PlayerController>() == null) return;
        tipShown = true;
        QueueToast("CONSEJO", "Inventario", "TAB  ·  abrir y cerrar", null, Accent, 6f);
    }

    void OnItemAdded(string id)
    {
        ItemInfo info = Inventory.GetInfo(id);
        QueueToast("NUEVO OBJETO", info.name, "TAB  ·  abrir inventario", InventoryIcons.Get(info.icon), Accent, 0.1f);
    }

    void OnDocumentAdded(DocumentEntry d) =>
        QueueToast("DOCUMENTO GUARDADO", d.title, "TAB  ·  inventario  >  documentos", InventoryIcons.Document(d.style), Accent, 0.3f);

    void QueueToast(string newLabel, string newName, string newHint, Texture2D icon, Color accent, float delay)
    {
        toastLabel = newLabel;
        toastName = newName;
        toastHint = newHint;
        toastIcon = icon;
        toastAccent = accent;
        toastAt = Time.unscaledTime + delay;
        toastPending = true;
    }

    // ---------------------------------------------------------------- Entrada

    void Update()
    {
        // El aviso espera a que no haya otro panel encima (por ejemplo, el documento que acabas de recoger)
        if (toastPending && Time.unscaledTime >= toastAt && !UIState.PanelOpen)
        {
            toastPending = false;
            toastStart = Time.unscaledTime;
        }

        Keyboard kb = Keyboard.current;
        if (kb == null) return;
        bool toggle = kb.tabKey.wasPressedThisFrame || kb.iKey.wasPressedThisFrame;

        if (!open)
        {
            if (toggle) TryOpen();
            return;
        }

        PlayerHealth health = PlayerHealth.Instance;
        if (toggle || kb.escapeKey.wasPressedThisFrame || (health != null && (health.IsDead || health.Health < healthAtOpen)))
        {
            Close();   // te han golpeado: se cierra solo
            return;
        }
        if (LookBackEncounter.AnyThreat)
        {
            Close();
            QueueToast("ALGO TE ACECHA", "No es el momento", "", null, Danger, 0f);
            return;
        }

        if (kb.qKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame || kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame)
        {
            if (kb.digit1Key.wasPressedThisFrame) SwitchTab(Tab.Items);
            else if (kb.digit2Key.wasPressedThisFrame) SwitchTab(Tab.Documents);
            else SwitchTab(tab == Tab.Items ? Tab.Documents : Tab.Items);
        }
        if (kb.leftArrowKey.wasPressedThisFrame) Move(-1, 0);
        if (kb.rightArrowKey.wasPressedThisFrame) Move(1, 0);
        if (kb.upArrowKey.wasPressedThisFrame) Move(0, -1);
        if (kb.downArrowKey.wasPressedThisFrame) Move(0, 1);
        if (tab == Tab.Documents && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)) ReadSelected();
    }

    void TryOpen()
    {
        if (UIState.PanelOpen) return;
        PlayerHealth health = PlayerHealth.Instance;
        if (health != null && health.IsDead) return;
        player = FindFirstObjectByType<PlayerController>();
        if (player == null || !player.enabled) return;   // solo mientras controlas al jugador
        if (LookBackEncounter.AnyThreat)
        {
            QueueToast("ALGO TE ACECHA", "No es el momento", "", null, Danger, 0f);
            return;
        }

        flashlight = FindFirstObjectByType<Flashlight>();
        healthAtOpen = health != null ? health.Health : 0f;
        BuildEntries();
        tab = Tab.Items;
        selectedItem = selectedDoc = docScroll = 0;
        openedTime = Time.unscaledTime;
        open = true;
        UIState.OpenPanel();
    }

    void Close()
    {
        if (!open) return;
        open = false;
        UIState.ClosePanel();
    }

    void BuildEntries()
    {
        entries.Clear();
        if (flashlight != null && flashlight.hasFlashlight) entries.Add(new Entry { id = "linterna", info = Inventory.GetInfo("linterna") });
        foreach (string id in Inventory.Items) entries.Add(new Entry { id = id, info = Inventory.GetInfo(id) });
        foreach (KeyValuePair<string, int> s in Inventory.Stacks)
        {
            ItemInfo info = Inventory.GetInfo(s.Key);
            info.name += "  x" + s.Value;
            entries.Add(new Entry { id = s.Key, info = info });
        }
    }

    void SwitchTab(Tab newTab)
    {
        tab = newTab;
        docScroll = 0;
    }

    void Move(int dx, int dy)
    {
        if (tab == Tab.Items)
        {
            if (entries.Count == 0) return;
            selectedItem = Mathf.Clamp(selectedItem + dx + dy * Cols, 0, entries.Count - 1);
        }
        else
        {
            int count = Inventory.Documents.Count;
            if (count == 0) return;
            selectedDoc = Mathf.Clamp(selectedDoc + dx + dy, 0, count - 1);
            if (selectedDoc < docScroll) docScroll = selectedDoc;
            if (selectedDoc >= docScroll + DocRows) docScroll = selectedDoc - DocRows + 1;
        }
    }

    void ReadSelected()
    {
        if (selectedDoc < 0 || selectedDoc >= Inventory.Documents.Count) return;
        DocumentEntry d = Inventory.Documents[selectedDoc];
        Close();
        DocumentReader.Show(d.title, d.text, d.style, d.reference, d.date, d.author);   // el lector no vuelve a guardarlo
    }

    // ---------------------------------------------------------------- Dibujo: utilidades

    // Rectángulo en coordenadas del panel (1500 x 860) convertido a pantalla
    Rect R(float x, float y, float w, float h) => new Rect(origin.x + x * s, origin.y + y * s, w * s, h * s);

    Color A(Color c) { c.a *= alpha; return c; }

    void Fill(Rect r, Color c)
    {
        GUI.color = A(c);
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    void Frame(Rect r, Color c, float thickness)
    {
        float t = Mathf.Max(1f, thickness * s);
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    // Esquinas en "L" (marco de expediente)
    void Corners(Rect r, Color c, float length, float thickness)
    {
        float l = length * s, t = Mathf.Max(1f, thickness * s);
        Fill(new Rect(r.x, r.y, l, t), c); Fill(new Rect(r.x, r.y, t, l), c);
        Fill(new Rect(r.xMax - l, r.y, l, t), c); Fill(new Rect(r.xMax - t, r.y, t, l), c);
        Fill(new Rect(r.x, r.yMax - t, l, t), c); Fill(new Rect(r.x, r.yMax - l, t, l), c);
        Fill(new Rect(r.xMax - l, r.yMax - t, l, t), c); Fill(new Rect(r.xMax - t, r.yMax - l, t, l), c);
    }

    void Picture(Rect r, Texture2D tex, Color tint)
    {
        if (tex == null) return;
        GUI.color = A(tint);
        GUI.DrawTexture(r, tex, ScaleMode.ScaleToFit, true);
        GUI.color = Color.white;
    }

    // Rect en pantalla; "size" en unidades del panel
    void Text(Rect r, string text, float size, FontStyle fontStyle, TextAnchor anchor, Color color, Font font = null, bool wrap = false)
    {
        label.fontSize = Mathf.Max(8, Mathf.RoundToInt(size * s));
        label.fontStyle = fontStyle;
        label.alignment = anchor;
        label.wordWrap = wrap;
        label.clipping = TextClipping.Clip;
        label.richText = false;
        label.font = font;
        Color c = A(color);
        label.normal.textColor = label.hover.textColor = label.active.textColor = label.focused.textColor = c;
        label.onNormal.textColor = label.onHover.textColor = label.onActive.textColor = label.onFocused.textColor = c;
        GUI.color = Color.white;
        GUI.Label(r, text, label);
    }

    void Bar(Rect r, float value, Color color)
    {
        Fill(r, new Color(0f, 0f, 0f, 0.55f));
        Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(value), r.height), color);
        Frame(r, Line, 1f);
    }

    bool Clicked(Event e, Rect r)
    {
        if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
        e.Use();
        return true;
    }

    void LoadFonts()
    {
        if (fontsLoaded) return;
        fontsLoaded = true;
        typewriter = DocumentReader.Find("Courier New", "Consolas", "Lucida Console");
        handwriting = DocumentReader.Find("Ink Free", "Segoe Script", "Segoe Print", "Bradley Hand ITC", "Comic Sans MS");
    }

    // ---------------------------------------------------------------- Dibujo: pantalla

    void OnGUI()
    {
        if (label == null) label = new GUIStyle(GUI.skin.label);
        LoadFonts();
        Event e = Event.current;

        if (!open) { DrawToast(); return; }

        float t = Mathf.Clamp01((Time.unscaledTime - openedTime) / 0.22f);
        alpha = 1f - (1f - t) * (1f - t);
        s = Mathf.Min(Screen.width * 0.95f / PanelW, Screen.height * 0.92f / PanelH);
        origin = new Vector2((Screen.width - PanelW * s) / 2f, (Screen.height - PanelH * s) / 2f + (1f - alpha) * 24f * s);

        Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.84f));

        // Panel: sombra, fondo, grano de pared sucia, marco doble y esquinas
        Rect panel = R(0, 0, PanelW, PanelH);
        Fill(R(10, 14, PanelW, PanelH), new Color(0f, 0f, 0f, 0.6f));
        Fill(panel, PanelColor);
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(panel, InventoryIcons.Grain, new Rect(0, 0, panel.width / 128f, panel.height / 128f));
        GUI.color = Color.white;
        Fill(R(0, 0, PanelW, 100), HeaderColor);
        Frame(panel, Line, 1.5f);
        Frame(R(9, 9, PanelW - 18, PanelH - 18), new Color(Line.r, Line.g, Line.b, 0.2f), 1f);
        Corners(R(9, 9, PanelW - 18, PanelH - 18), Accent, 30f, 3f);

        // Cabecera
        Text(R(48, 18, 600, 56), "INVENTARIO", 46, FontStyle.Bold, TextAnchor.MiddleLeft, Bright, typewriter);
        Text(R(50, 68, 700, 26), "INSTITUTO PSIQUIÁTRICO MONTE ALTO  ·  PERTENENCIAS DEL PACIENTE", 15, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);
        Fill(R(40, 100, PanelW - 80, 1.5f), Line);
        DrawTabs(e);

        if (tab == Tab.Items) DrawItems(e);
        else DrawDocuments(e);

        DrawFooter();
    }

    void DrawTabs(Event e)
    {
        string[] names = { "OBJETOS", "DOCUMENTOS" };
        int[] counts = { entries.Count, Inventory.Documents.Count };
        for (int i = 0; i < 2; i++)
        {
            Rect r = R(982 + i * 240, 30, 230, 50);
            bool on = (int)tab == i, hover = r.Contains(e.mousePosition);
            Fill(r, on ? AccentFill : hover ? SlotHover : SlotEmpty);
            Frame(r, on ? Accent : Line, on ? 2f : 1f);
            Text(r, names[i] + "  " + counts[i], 20, FontStyle.Bold, TextAnchor.MiddleCenter, on ? Bright : Dim, typewriter);
            if (Clicked(e, r)) SwitchTab((Tab)i);
        }
    }

    // ---------------------------------------------------------------- Pestaña Objetos

    void DrawItems(Event e)
    {
        float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 3f);

        Text(R(54, 108, 500, 24), "LO QUE LLEVAS  ·  " + entries.Count + " / " + (Cols * Rows), 14, FontStyle.Normal, TextAnchor.MiddleLeft, Faint, typewriter);

        for (int i = 0; i < Cols * Rows; i++)
        {
            float x = 54 + (i % Cols) * 194, y = 134 + (i / Cols) * 212;
            Rect slot = R(x, y, 170, 190);
            bool has = i < entries.Count, hover = slot.Contains(e.mousePosition), selected = has && i == selectedItem;

            Fill(slot, !has ? SlotEmpty : (hover || selected) ? SlotHover : SlotColor);
            Frame(slot, has ? Line : new Color(Line.r, Line.g, Line.b, 0.18f), 1f);

            if (!has)
            {
                Text(slot, "—", 26, FontStyle.Normal, TextAnchor.MiddleCenter, new Color(Faint.r, Faint.g, Faint.b, 0.5f), typewriter);
                continue;
            }

            if (selected) Picture(R(x - 10, y - 4, 190, 170), InventoryIcons.Glow, new Color(Accent.r, Accent.g, Accent.b, 0.35f * pulse));
            Picture(R(x + 25, y + 14, 120, 120), InventoryIcons.Get(entries[i].info.icon), Color.white);
            Text(R(x + 6, y + 142, 158, 40), entries[i].info.name, 17, FontStyle.Bold, TextAnchor.MiddleCenter, selected ? Bright : Beige, typewriter, true);

            if (selected)
            {
                Frame(slot, new Color(Accent.r, Accent.g, Accent.b, pulse), 2f);
                Corners(slot, Bright, 12f, 2f);
            }
            if (Clicked(e, slot)) selectedItem = i;
        }

        DrawItemDetail();
    }

    void DrawItemDetail()
    {
        Rect box = R(850, 108, 610, 640);
        Fill(box, DetailColor);
        Frame(box, Line, 1f);

        if (entries.Count == 0)
        {
            Text(R(870, 330, 570, 50), "No llevas nada encima", 30, FontStyle.Bold, TextAnchor.MiddleCenter, Dim, typewriter);
            Text(R(910, 392, 490, 110), "Registra el hospital: cajones, archivadores, mesas. Lo que recojas aparecerá aquí.", 19, FontStyle.Normal, TextAnchor.UpperCenter, Faint, typewriter, true);
            return;
        }

        Entry en = entries[Mathf.Clamp(selectedItem, 0, entries.Count - 1)];
        Picture(R(1000, 130, 310, 310), InventoryIcons.Glow, new Color(Accent.r, Accent.g, Accent.b, 0.22f));
        Picture(R(1035, 165, 240, 240), InventoryIcons.Get(en.info.icon), Color.white);

        Text(R(870, 438, 570, 50), en.info.name, 36, FontStyle.Bold, TextAnchor.MiddleCenter, Bright, typewriter);
        Fill(R(940, 494, 430, 1.5f), Line);
        Text(R(902, 510, 506, 130), en.info.description, 20, FontStyle.Normal, TextAnchor.UpperLeft, Beige, typewriter, true);

        if (en.id == "linterna" && flashlight != null)
        {
            float charge = flashlight.Charge;
            Text(R(902, 652, 250, 22), "BATERÍA", 15, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);
            Text(R(1158, 652, 250, 22), Mathf.RoundToInt(charge * 100f) + " %", 15, FontStyle.Bold, TextAnchor.MiddleRight, Beige, typewriter);
            Bar(R(902, 677, 506, 14), charge, charge > flashlight.lowCharge ? new Color(0.85f, 0.82f, 0.6f) : Danger);
        }
        if (!string.IsNullOrEmpty(en.info.hint))
            Text(R(902, 704, 506, 30), en.info.hint, 17, FontStyle.Normal, TextAnchor.MiddleCenter, Accent, typewriter);
    }

    // ---------------------------------------------------------------- Pestaña Documentos

    void DrawDocuments(Event e)
    {
        IReadOnlyList<DocumentEntry> docs = Inventory.Documents;
        Text(R(54, 108, 600, 24), "HAS LEÍDO  ·  " + docs.Count, 14, FontStyle.Normal, TextAnchor.MiddleLeft, Faint, typewriter);

        int maxScroll = Mathf.Max(0, docs.Count - DocRows);
        docScroll = Mathf.Clamp(docScroll, 0, maxScroll);
        Rect listArea = R(54, 134, 752, 620);
        if (e.type == EventType.ScrollWheel && listArea.Contains(e.mousePosition))
        {
            docScroll = Mathf.Clamp(docScroll + (e.delta.y > 0f ? 1 : -1), 0, maxScroll);
            e.Use();
        }

        if (docs.Count == 0)
        {
            Fill(listArea, SlotEmpty);
            Frame(listArea, new Color(Line.r, Line.g, Line.b, 0.18f), 1f);
            Text(R(80, 330, 700, 50), "Aún no has leído ningún documento", 26, FontStyle.Bold, TextAnchor.MiddleCenter, Dim, typewriter);
            Text(R(140, 390, 580, 80), "Notas, informes y expedientes: cada uno que leas queda guardado aquí.", 18, FontStyle.Normal, TextAnchor.UpperCenter, Faint, typewriter, true);
            Rect empty = R(850, 108, 610, 640);
            Fill(empty, DetailColor);
            Frame(empty, Line, 1f);
            return;
        }

        selectedDoc = Mathf.Clamp(selectedDoc, 0, docs.Count - 1);
        for (int k = 0; k < DocRows && docScroll + k < docs.Count; k++)
        {
            int i = docScroll + k;
            DocumentEntry d = docs[i];
            float y = 134 + k * 90;
            Rect row = R(54, y, 752, 82);
            bool selected = i == selectedDoc, hover = row.Contains(e.mousePosition);

            Fill(row, selected ? AccentFill : hover ? SlotHover : SlotColor);
            Frame(row, selected ? Accent : Line, selected ? 2f : 1f);
            Picture(R(66, y + 8, 66, 66), InventoryIcons.Document(d.style), Color.white);
            Text(R(148, y + 8, 640, 36), d.title, 24, FontStyle.Bold, TextAnchor.MiddleLeft, selected ? Bright : Beige, typewriter);
            Text(R(148, y + 44, 640, 28), StyleName(d.style) + Meta(d), 15, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);

            if (Clicked(e, row))
            {
                bool again = i == selectedDoc && e.clickCount >= 2;
                selectedDoc = i;
                if (again) { ReadSelected(); return; }
            }
        }

        if (maxScroll > 0)   // barra de desplazamiento
        {
            Rect track = R(812, 134, 6, 620);
            Fill(track, new Color(0f, 0f, 0f, 0.5f));
            float h = track.height * DocRows / docs.Count;
            Fill(new Rect(track.x, track.y + (track.height - h) * docScroll / maxScroll, track.width, h), Line);
        }

        DrawDocumentDetail(e, docs[selectedDoc]);
    }

    void DrawDocumentDetail(Event e, DocumentEntry d)
    {
        Rect box = R(850, 108, 610, 640);
        Fill(box, DetailColor);
        Frame(box, Line, 1f);

        // Vista previa de la hoja (misma textura que el lector), con un trozo del texto
        Rect paper = R(878, 132, 349, 480);
        bool patient = d.style == DocumentStyle.PatientNote;
        Matrix4x4 saved = GUI.matrix;
        GUIUtility.RotateAroundPivot(patient ? -2f : d.style == DocumentStyle.StaffReport ? 0.8f : 0f, paper.center);
        Fill(new Rect(paper.x + 7 * s, paper.y + 9 * s, paper.width, paper.height), new Color(0f, 0f, 0f, 0.6f));
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTexture(paper, PaperTexture.Get(d.style));
        GUI.color = Color.white;

        Font font = patient ? handwriting : typewriter;
        Color ink = patient ? new Color(0.1f, 0.1f, 0.2f) : new Color(0.1f, 0.09f, 0.08f);
        float left = paper.x + paper.width * 0.12f, width = paper.width * 0.76f;
        Text(new Rect(left, paper.y + paper.height * 0.06f, width, paper.height * 0.09f), patient ? d.title : d.title.ToUpper(), patient ? 19 : 14, FontStyle.Bold, TextAnchor.MiddleLeft, ink, font, true);
        Text(new Rect(left, paper.y + paper.height * 0.17f, width, paper.height * 0.72f), d.text, patient ? 14 : 11, FontStyle.Normal, TextAnchor.UpperLeft, ink, font, true);
        GUI.matrix = saved;

        if (Clicked(e, paper)) { ReadSelected(); return; }

        // Ficha a la derecha de la hoja
        float y = 140;
        y = Field(y, "TIPO", StyleName(d.style));
        if (!string.IsNullOrEmpty(d.reference)) y = Field(y, "REFERENCIA", d.reference);
        if (!string.IsNullOrEmpty(d.date)) y = Field(y, "FECHA", d.date);
        if (!string.IsNullOrEmpty(d.author)) Field(y, "FIRMA", d.author);

        Rect button = R(878, 646, 552, 70);
        bool hover = button.Contains(e.mousePosition);
        Fill(button, hover ? new Color(0.2f, 0.32f, 0.26f) : AccentFill);
        Frame(button, Accent, 2f);
        Text(button, "LEER DOCUMENTO", 24, FontStyle.Bold, TextAnchor.MiddleCenter, Bright, typewriter);
        Text(R(878, 718, 552, 24), "clic en la hoja  ·  Enter", 13, FontStyle.Normal, TextAnchor.MiddleCenter, Faint, typewriter);
        if (Clicked(e, button)) ReadSelected();
    }

    float Field(float y, string name, string value)
    {
        Text(R(1250, y, 190, 20), name, 13, FontStyle.Normal, TextAnchor.MiddleLeft, Faint, typewriter);
        Text(R(1250, y + 20, 190, 56), value, 18, FontStyle.Bold, TextAnchor.UpperLeft, Beige, typewriter, true);
        return y + 82;
    }

    static string StyleName(DocumentStyle style) =>
        style == DocumentStyle.PatientNote ? "Nota manuscrita" : style == DocumentStyle.MedicalRecord ? "Expediente clínico" : "Informe interno";

    static string Meta(DocumentEntry d)
    {
        string meta = "";
        if (!string.IsNullOrEmpty(d.reference)) meta += "  ·  " + d.reference;
        if (!string.IsNullOrEmpty(d.date)) meta += "  ·  " + d.date;
        return meta;
    }

    // ---------------------------------------------------------------- Pie: salud, batería y teclas

    void DrawFooter()
    {
        Fill(R(40, 764, PanelW - 80, 1.5f), Line);

        PlayerHealth health = PlayerHealth.Instance;
        if (health != null)
        {
            float ratio = health.Health / health.maxHealth;
            string state = ratio >= 1f ? "ILESO" : ratio > 0.4f ? "HERIDO" : "CRÍTICO";
            Text(R(54, 778, 200, 22), "SALUD", 14, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);
            Text(R(254, 778, 150, 22), state, 14, FontStyle.Bold, TextAnchor.MiddleRight, ratio >= 1f ? Accent : Danger, typewriter);
            Bar(R(54, 806, 350, 16), ratio, Color.Lerp(Danger, new Color(0.45f, 0.7f, 0.55f), ratio));
        }

        if (flashlight != null && flashlight.hasFlashlight)
        {
            Text(R(450, 778, 200, 22), "LINTERNA", 14, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);
            Text(R(600, 778, 150, 22), Mathf.RoundToInt(flashlight.Charge * 100f) + " %", 14, FontStyle.Bold, TextAnchor.MiddleRight, Beige, typewriter);
            Bar(R(450, 806, 300, 16), flashlight.Charge, flashlight.Charge > flashlight.lowCharge ? new Color(0.85f, 0.82f, 0.6f) : Danger);
        }

        Text(R(800, 776, 660, 24), "TAB / I  cerrar     Q / E  cambiar pestaña", 14, FontStyle.Normal, TextAnchor.MiddleRight, Dim, typewriter);
        Text(R(800, 802, 660, 24), "← ↑ ↓ →  elegir     Enter  leer     clic  seleccionar", 14, FontStyle.Normal, TextAnchor.MiddleRight, Faint, typewriter);
    }

    // ---------------------------------------------------------------- Aviso de esquina

    void DrawToast()
    {
        float age = Time.unscaledTime - toastStart;
        if (age < 0f || age > 5f || string.IsNullOrEmpty(toastName)) return;

        float slide = Mathf.Clamp01(age / 0.35f);
        slide = 1f - (1f - slide) * (1f - slide);
        alpha = Mathf.Clamp01((5f - age) / 0.6f);
        s = Screen.height / 1080f;
        origin = new Vector2(Screen.width - 410f * s + (1f - slide) * 440f * s, 52f * s);

        Rect card = R(0, 0, 380, 100);
        Fill(R(5, 7, 380, 100), new Color(0f, 0f, 0f, 0.5f));
        Fill(card, new Color(0.055f, 0.07f, 0.065f, 0.96f));
        Frame(card, Line, 1f);
        Fill(R(0, 0, 5, 100), toastAccent);

        float textLeft = 22;
        if (toastIcon != null)
        {
            Picture(R(16, 14, 72, 72), toastIcon, Color.white);
            textLeft = 102;
        }
        Text(R(textLeft, 10, 366 - textLeft, 22), toastLabel, 14, FontStyle.Bold, TextAnchor.MiddleLeft, toastAccent, typewriter);
        Text(R(textLeft, 32, 366 - textLeft, 36), toastName, 24, FontStyle.Bold, TextAnchor.MiddleLeft, Bright, typewriter);
        Text(R(textLeft, 68, 366 - textLeft, 24), toastHint, 14, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, typewriter);
        alpha = 1f;
    }
}
