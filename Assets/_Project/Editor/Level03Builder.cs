using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BuildKit;

// Menú "Don't Look Back > Crear Nivel 3 (Ala de Investigación)".
// Arma Assets/_Project/Scenes/Level03_ResearchWing.unity: el ala donde se estudió al paciente 214 (nivel inferior al que baja el
// montacargas del Nivel 2). Recorrido: montacargas -> recepción -> corredor de observación -> sala de proyección -> cámara de pruebas
// -> laboratorio -> sala de dibujos (opcional) -> dirección -> vestíbulo S-9.
//
// AUTÓNOMO: no usa ni modifica Level02Builder. El sistema de plano en celdas de 2 m (aristas -> módulos de pared) está copiado y
// adaptado de Level02Builder a propósito, para que tocar el Nivel 3 nunca cambie el resultado del Nivel 2. Utilidades compartidas: BuildKit.
//
// Este archivo: plano, arquitectura, materiales, jugador, atmósfera y audio.
// Level03Builder.Dressing.cs: mobiliario, rótulos, dibujos y detalles.  .Lighting.cs: luces.  .Gameplay.cs: objetos, documentos, puertas y salida.
// Se puede ejecutar de nuevo para regenerar la escena (sobrescribe el archivo).
public static partial class Level03Builder
{
    const string ScenePath = "Assets/_Project/Scenes/Level03_ResearchWing.unity";
    const string FH = "Assets/FunctionalHorrorProps/Prefabs/";
    const string FHMat = "Assets/FunctionalHorrorProps/Materials/";
    const string SP = "Assets/Survival Props/prefabs/";
    const string AS = "Assets/Abandoned_Asylum/Prefabs/";
    const string MatDir = "Assets/_Project/Art/Materials/Level03/";
    const string AmbientClip = "Assets/Sounds/jorisvermeer-horror-background-atmosphere-030-394196.mp3";

    const float U = 2f;          // lado de una celda del plano (m)
    const float WallH = 3.2f;    // alto de un piso (módulo de pared de FunctionalHorrorProps)
    const int Cols = 18, Rows = 14;

    // ---------------------------------------------------------------- Plano (norte = fila 0)
    //  M  llegada del montacargas (6x4)        R  recepción de investigación (8x6)     O  corredor de observación (en L)
    //  P  sala de proyección (6x6)             C  cámara de pruebas (8x8, doble alto, con galería)   L  laboratorio / cámara fría (8x6)
    //  D  sala de dibujos del paciente 214 (6x6)   X  dirección de investigación (10x8)   V  vestíbulo de S-9 (4x6)
    static readonly char[,] grid = new char[Rows, Cols];

    static void DefineMap()
    {
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) grid[r, c] = ' ';
        Fill('M', 15, 11, 17, 12);
        Fill('R', 11, 11, 14, 13);
        Fill('O', 9, 7, 14, 7);
        Fill('O', 14, 6, 14, 10);
        Fill('P', 13, 3, 15, 5);
        Fill('C', 9, 3, 12, 6);
        Fill('L', 5, 4, 8, 6);
        Fill('D', 2, 4, 4, 6);
        Fill('X', 3, 0, 7, 3);
        Fill('V', 8, 0, 9, 2);
    }

    static void Fill(char ch, int c0, int r0, int c1, int r1)
    {
        for (int r = r0; r <= r1; r++) for (int c = c0; c <= c1; c++) grid[r, c] = ch;
    }

    // Huecos de paso de 2 m (sin pared). Celda (c,r) y lado (N/E/S/W)
    static readonly (int c, int r, char d)[] Gaps =
    {
        (14, 11, 'E'),   // recepción -> llegada del montacargas
        (14, 10, 'S'),   // corredor -> recepción
        (14, 5, 'S'),    // corredor -> sala de proyección
        (12, 4, 'E'),    // cámara de pruebas <-> sala de proyección
        (8, 5, 'E'),     // laboratorio <-> cámara de pruebas  (puerta con tarjeta)
        (4, 5, 'E'),     // sala de dibujos <-> laboratorio
        (6, 3, 'S'),     // dirección <-> laboratorio  (puerta con llave de Dirección)
        (7, 1, 'E'),     // dirección -> vestíbulo S-9
    };

    // Ventanas de observación: módulo de 4 m que ocupa esta arista y la contigua hacia el este/sur
    static readonly (int c, int r, char d)[] WindowModules =
    {
        (9, 6, 'S'), (11, 6, 'S'),   // corredor -> cámara de pruebas
    };

    static bool IsRoom(char c) => c != ' ' && c != 'O';
    static bool IsTall(char c) => c == 'C';    // doble alto (6,4 m)
    static float HeightOf(char c) => IsTall(c) ? WallH * 2f : WallH;

    static char At(int c, int r) => (r < 0 || r >= Rows || c < 0 || c >= Cols) ? ' ' : grid[r, c];
    static float XC(int c) => c * U + U / 2f;
    static float ZC(int r) => (Rows - 1 - r) * U + U / 2f;
    static Vector3 CellCenter(int c, int r) => new Vector3(XC(c), 0, ZC(r));

    static string RoomName(char c)
    {
        switch (c)
        {
            case 'M': return "ElevatorLanding";
            case 'R': return "Reception";
            case 'O': return "ObservationCorridor";
            case 'P': return "ProjectionRoom";
            case 'C': return "TestChamber";
            case 'L': return "ColdLab";
            case 'D': return "DrawingRoom";
            case 'X': return "DirectorOffice";
            case 'V': return "VestibuleS9";
            default: return "Other";
        }
    }

    // ---------------------------------------------------------------- Estilos por zona (paleta cálida y deteriorada)

    struct Zone
    {
        public string name;
        public Color plaster, enamel, floor, ceiling;
        public float wet;      // brillo extra: humedad
        public bool clinicFloor;
        public Zone(string n, Color p, Color e, Color f, Color c, float w, bool clinic = false) { name = n; plaster = p; enamel = e; floor = f; ceiling = c; wet = w; clinicFloor = clinic; }
    }

    static readonly Zone ZLanding = new Zone("Landing", new Color(0.66f, 0.55f, 0.42f), new Color(0.60f, 0.40f, 0.26f), new Color(0.50f, 0.44f, 0.38f), new Color(0.46f, 0.40f, 0.34f), 0.2f);
    static readonly Zone ZReception = new Zone("Reception", new Color(0.74f, 0.62f, 0.46f), new Color(0.62f, 0.40f, 0.24f), new Color(0.52f, 0.43f, 0.34f), new Color(0.52f, 0.45f, 0.38f), 0.18f);
    static readonly Zone ZCorridor = new Zone("Corridor", new Color(0.62f, 0.52f, 0.40f), new Color(0.52f, 0.34f, 0.22f), new Color(0.44f, 0.38f, 0.32f), new Color(0.40f, 0.35f, 0.30f), 0.3f);
    static readonly Zone ZProjection = new Zone("Projection", new Color(0.54f, 0.43f, 0.35f), new Color(0.46f, 0.26f, 0.20f), new Color(0.38f, 0.30f, 0.27f), new Color(0.32f, 0.27f, 0.25f), 0.15f);
    static readonly Zone ZChamber = new Zone("Chamber", new Color(0.70f, 0.62f, 0.50f), new Color(0.56f, 0.44f, 0.30f), new Color(0.50f, 0.42f, 0.34f), new Color(0.42f, 0.38f, 0.33f), 0.4f);
    static readonly Zone ZLab = new Zone("Lab", new Color(0.76f, 0.72f, 0.60f), new Color(0.50f, 0.50f, 0.38f), new Color(0.58f, 0.55f, 0.48f), new Color(0.50f, 0.48f, 0.42f), 0.5f, true);
    static readonly Zone ZDrawing = new Zone("Drawing", new Color(0.60f, 0.52f, 0.42f), new Color(0.46f, 0.34f, 0.26f), new Color(0.38f, 0.32f, 0.27f), new Color(0.34f, 0.30f, 0.27f), 0.15f);
    static readonly Zone ZOffice = new Zone("Office", new Color(0.62f, 0.46f, 0.34f), new Color(0.36f, 0.21f, 0.12f), new Color(0.34f, 0.24f, 0.18f), new Color(0.36f, 0.30f, 0.26f), 0.35f);
    static readonly Zone ZVestibule = new Zone("Vestibule", new Color(0.50f, 0.38f, 0.32f), new Color(0.38f, 0.22f, 0.18f), new Color(0.32f, 0.26f, 0.24f), new Color(0.30f, 0.25f, 0.24f), 0.25f);

    static Zone StyleOf(char c)
    {
        switch (c)
        {
            case 'M': return ZLanding;
            case 'R': return ZReception;
            case 'O': return ZCorridor;
            case 'P': return ZProjection;
            case 'C': return ZChamber;
            case 'L': return ZLab;
            case 'D': return ZDrawing;
            case 'X': return ZOffice;
            default: return ZVestibule;
        }
    }

    // ---------------------------------------------------------------- Jerarquía y estado de la construcción

    static Transform root, environment, architecture, floors, walls, ceilings, windowsGroup, structural, stairsGroup;
    static Transform propsRoot, lightingRoot, gameplayRoot, audioRoot, postRoot;
    static Transform interactablesGroup, keysGroup, documentsGroup, doorsGroup, triggersGroup, exitGroup;
    static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> boxMeshes = new Dictionary<string, Mesh>();

    class Edge
    {
        public int c, r; public bool v;        // v = pared vertical (arista este de la celda); si no, arista sur
        public char a, b;                      // celdas a ambos lados (' ' = exterior)
        public char face;                      // habitación hacia la que mira la cara sucia
        public bool gap, doorStart, doorSecond, windowStart, windowSecond;
        public bool flip, tall;
        public Zone zone;
        public Vector3 Mid => v ? new Vector3((c + 1) * U, 0, ZC(r)) : new Vector3(XC(c), 0, (Rows - 1 - r) * U);
    }
    static readonly List<Edge> edges = new List<Edge>();
    static readonly Dictionary<(int, int, bool), Edge> edgeMap = new Dictionary<(int, int, bool), Edge>();

    static (int, int, bool) Canon(int c, int r, char d)
    {
        switch (d)
        {
            case 'S': return (c, r, false);
            case 'E': return (c, r, true);
            case 'N': return (c, r - 1, false);
            default: return (c - 1, r, true);
        }
    }

    // Lazy: crea el grupo la primera vez que se pide (así nunca queda un grupo vacío por costumbre)
    static Transform Lazy(string name, Transform parent)
    {
        string key = (parent != null ? parent.GetHashCode().ToString() : "") + "/" + name;
        if (groups.TryGetValue(key, out Transform t) && t != null) return t;
        t = T(name, parent);
        groups[key] = t;
        return t;
    }

    [MenuItem("Don't Look Back/Crear Nivel 3 (Ala de Investigación)")]
    public static void Build() => BuildLevel(true);

    // interactive = false lo usa el asistente (sin diálogos); aborta si hay otra escena sin guardar
    public static void BuildLevel(bool interactive)
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Level03Builder: detén el juego antes de crear el nivel.");
            return;
        }
        if (interactive) { if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; }
        else if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("Level03Builder: hay cambios sin guardar en la escena abierta."); return; }

        EnsureFolder("Assets/_Project", "Scenes");
        EnsureFolder("Assets/_Project", "Settings");
        EnsureFolder("Assets/_Project/Art", "Materials");
        AssetDatabase.DeleteAsset(MatDir.TrimEnd('/'));   // los materiales del nivel se regeneran siempre (los de los packs no se tocan)
        EnsureFolder("Assets/_Project/Art/Materials", "Level03");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        groups.Clear(); materials.Clear(); boxMeshes.Clear(); edges.Clear(); edgeMap.Clear();
        DefineMap();

        root = T("Level03_ResearchWing");
        environment = T("Environment", root);
        architecture = T("Architecture", environment);
        floors = T("Floors", architecture);
        walls = T("Walls", architecture);
        ceilings = T("Ceilings", architecture);
        propsRoot = T("Props", root);
        lightingRoot = T("Lighting", root);
        gameplayRoot = T("Gameplay", root);
        audioRoot = T("Audio", root);
        postRoot = T("PostProcessing", root);
        // Stairs, Windows y StructuralDetails nacen la primera vez que se usan (ver Lazy)
        windowsGroup = null; structural = null; stairsGroup = null;

        BuildEdges();
        BuildWalls();
        BuildPillars();
        BuildFloorsAndCeilings();
        BuildStairsAndGallery();
        BuildAtmosphere();
        BuildPlayer();
        BuildAudio();
        BuildDressing();   // Level03Builder.Dressing.cs (llama a Gameplay y Lighting)

        foreach (Transform t in architecture.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Nivel 3 creado en " + ScenePath + " (" + Cols * U + " x " + Rows * U + " m).");
    }

    // ---------------------------------------------------------------- Aristas y paredes

    static void BuildEdges()
    {
        for (int r = -1; r < Rows; r++)
            for (int c = -1; c < Cols; c++)
            {
                char a = At(c, r);
                char e = At(c + 1, r), s = At(c, r + 1);
                if (a != e && (a != ' ' || e != ' ')) AddEdge(c, r, true, a, e);
                if (a != s && (a != ' ' || s != ' ')) AddEdge(c, r, false, a, s);
            }

        foreach (var g in Gaps)
        {
            if (edgeMap.TryGetValue(Canon(g.c, g.r, g.d), out Edge e)) e.gap = true;
            else Debug.LogWarning("Level03Builder: el hueco (" + g.c + "," + g.r + "," + g.d + ") no está entre dos zonas.");
        }
        foreach (var w in WindowModules)
        {
            var key = Canon(w.c, w.r, w.d);
            var key2 = (key.Item1 + (key.Item3 ? 0 : 1), key.Item2 + (key.Item3 ? 1 : 0), key.Item3);
            if (edgeMap.TryGetValue(key, out Edge e1) && edgeMap.TryGetValue(key2, out Edge e2)) { e1.windowStart = true; e2.windowSecond = true; }
            else Debug.LogWarning("Level03Builder: la ventana (" + w.c + "," + w.r + "," + w.d + ") no encaja.");
        }
    }

    static void AddEdge(int c, int r, bool v, char a, char b)
    {
        // La cara sucia del módulo mira hacia la habitación (o hacia la celda que existe si el otro lado es exterior)
        bool faceB = (IsRoom(b) && !IsRoom(a)) || a == ' ';
        char faceCh = faceB ? b : a;
        var e = new Edge
        {
            c = c, r = r, v = v, a = a, b = b, face = faceCh,
            flip = v ^ faceB,
            tall = IsTall(a) || IsTall(b),
            zone = StyleOf(faceCh),
        };
        edges.Add(e);
        edgeMap[(c, r, v)] = e;
    }

    static void BuildWalls()
    {
        var lines = new Dictionary<(bool, int), List<Edge>>();
        foreach (Edge e in edges)
        {
            var key = (e.v, e.v ? e.c : e.r);
            if (!lines.ContainsKey(key)) lines[key] = new List<Edge>();
            lines[key].Add(e);
        }

        foreach (var kv in lines)
        {
            List<Edge> line = kv.Value;
            line.Sort((x, y) => (x.v ? x.r : x.c).CompareTo(y.v ? y.r : y.c));
            int n = line.Count;

            for (int i = 0; i < n; i++)   // planta baja
            {
                Edge e = line[i];
                if (e.windowSecond || e.gap) continue;
                if (e.windowStart)
                {
                    Edge f = (i + 1 < n && Next(e, line[i + 1])) ? line[i + 1] : null;
                    PlaceWindow(e, f);
                    continue;
                }
                if (i + 1 < n && Mergeable(e, line[i + 1], false)) { PlaceWall("hospital_wall_solid", e, line[i + 1], 0, true, BothRooms(e)); i++; }
                else PlaceWall("hospital_wall_solid", e, null, 0, true, BothRooms(e));
            }

            for (int i = 0; i < n; i++)   // segundo piso de pared en la cámara de pruebas (también sobre los huecos y las ventanas)
            {
                Edge e = line[i];
                if (!e.tall) continue;
                if (i + 1 < n && Mergeable(e, line[i + 1], true)) { PlaceWall("hospital_wall_solid", e, line[i + 1], 1, false); i++; }
                else PlaceWall("hospital_wall_solid", e, null, 1, false);
            }

            foreach (Edge e in line) if (e.gap) PlaceLintel(e);
        }
    }

    static bool Next(Edge e, Edge f) => f.v == e.v && (e.v ? (f.c == e.c && f.r == e.r + 1) : (f.r == e.r && f.c == e.c + 1));

    static bool Mergeable(Edge e, Edge f, bool tier2)
    {
        if (!Next(e, f)) return false;
        if (f.flip != e.flip || f.zone.name != e.zone.name || Other(f) != Other(e)) return false;
        if (tier2) return f.tall;
        return !f.gap && !f.windowStart && !f.windowSecond;
    }

    static Transform WallParent(Edge e) => Lazy(RoomName(e.face), walls);

    // Habitación del lado contrario a la cara sucia del módulo (para pintar también esa cara)
    static char Other(Edge e) => e.face == e.a ? e.b : e.a;
    static bool BothRooms(Edge e) => IsRoom(e.a) && IsRoom(e.b);

    static void PlaceWall(string prefab, Edge e, Edge f, int tier, bool collider, bool mirror = false)
    {
        GameObject wall = Spawn(FH + prefab + ".prefab", WallParent(e));
        if (wall == null) return;
        Vector3 mid = f == null ? e.Mid : (e.Mid + f.Mid) / 2f;
        wall.name = (tier == 1 ? "WallUpper_" : "Wall_") + e.c + "_" + e.r + (e.v ? "_E" : "_S");
        wall.transform.rotation = Quaternion.Euler(0, (e.v ? 90f : 0f) + (e.flip ? 180f : 0f), 0);
        wall.transform.localScale = new Vector3(f == null ? 0.5f : 1f, 1f, 1f);   // una arista mide 2 m; el módulo, 4 m
        SnapXZ(wall, mid.x, mid.z);
        SnapY(wall, tier * WallH, false);
        Retint(wall, e.zone);

        if (mirror)
        {
            // Pared entre dos salas: el módulo del pack solo tiene una cara sucia. Se añade un segundo módulo girado (sin collider) con la
            // paleta de la sala vecina, desplazado 4 mm hacia su lado para que no haya z-fighting. Así ambas caras tienen textura.
            char other = Other(e);
            bool faceIsB = e.face == e.b;
            Vector3 toFace = e.v ? (faceIsB ? Vector3.right : Vector3.left) : (faceIsB ? Vector3.back : Vector3.forward);
            GameObject back = Spawn(FH + prefab + ".prefab", Lazy(RoomName(other), walls));
            if (back != null)
            {
                back.name = wall.name + "_Back";
                back.transform.rotation = Quaternion.Euler(0, (e.v ? 90f : 0f) + (e.flip ? 0f : 180f), 0);
                back.transform.localScale = wall.transform.localScale;
                SnapXZ(back, mid.x - toFace.x * 0.004f, mid.z - toFace.z * 0.004f);
                SnapY(back, tier * WallH, false);
                Retint(back, StyleOf(other));
            }
        }

        if (!collider) return;
        Bounds b = GetBounds(wall);
        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.center = wall.transform.InverseTransformPoint(b.center);
        box.size = new Vector3(4f, WallH, 0.3f);
    }

    // Ventana de observación (módulo de 4 m con vano de vidrio)
    static void PlaceWindow(Edge e, Edge f)
    {
        Transform parent = windowsGroup != null ? windowsGroup : (windowsGroup = T("Windows", architecture));
        GameObject wall = Spawn(FH + "hospital_wall_window.prefab", parent);
        if (wall == null) return;
        Vector3 mid = f == null ? e.Mid : (e.Mid + f.Mid) / 2f;
        wall.name = "ObservationWindow_" + e.c + "_" + e.r;
        wall.transform.rotation = Quaternion.Euler(0, (e.v ? 90f : 0f) + (e.flip ? 180f : 0f), 0);
        SnapXZ(wall, mid.x, mid.z);
        SnapY(wall, 0f, false);
        Retint(wall, e.zone);
        Bounds b = GetBounds(wall);
        BoxCollider box = wall.AddComponent<BoxCollider>();   // el vidrio también se puede tocar: no se atraviesa
        box.center = wall.transform.InverseTransformPoint(b.center);
        box.size = new Vector3(4f, WallH, 0.3f);
    }

    // Viga sobre los huecos de 2 m: da forma de puerta y evita ver la parte alta de la pared vacía
    static void PlaceLintel(Edge e)
    {
        Vector3 size = e.v ? new Vector3(0.34f, 0.8f, U) : new Vector3(U, 0.8f, 0.34f);
        Box("Lintel_" + e.c + "_" + e.r + (e.v ? "_E" : "_S"), Lazy("StructuralDetails", architecture), new Vector3(e.Mid.x, WallH - 0.4f, e.Mid.z), size, ConcreteMat(e.zone, "Lintel", e.zone.ceiling * 0.9f), false);
    }

    // Pilares en esquinas, extremos y bordes de los huecos (no en tramos rectos ni entre dos huecos seguidos)
    static void BuildPillars()
    {
        var hCount = new Dictionary<Vector2Int, int>();
        var vCount = new Dictionary<Vector2Int, int>();
        var gapH = new Dictionary<Vector2Int, int>();
        var gapV = new Dictionary<Vector2Int, int>();
        var tall = new HashSet<Vector2Int>();
        foreach (Edge e in edges)
        {
            if (e.gap) TouchVertices(e, gapH, gapV, tall);
            else TouchVertices(e, hCount, vCount, tall);
        }

        var all = new HashSet<Vector2Int>(hCount.Keys);
        all.UnionWith(vCount.Keys);
        all.UnionWith(gapH.Keys);
        all.UnionWith(gapV.Keys);

        Transform parent = Lazy("StructuralDetails", architecture);
        foreach (Vector2Int p in all)
        {
            hCount.TryGetValue(p, out int h);
            vCount.TryGetValue(p, out int v);
            gapH.TryGetValue(p, out int gh);
            gapV.TryGetValue(p, out int gv);
            bool straight = (h == 2 && v == 0 && gh == 0 && gv == 0) || (v == 2 && h == 0 && gh == 0 && gv == 0);
            bool betweenGaps = h == 0 && v == 0 && (gh == 2 || gv == 2);
            if (straight || betweenGaps) continue;
            float height = tall.Contains(p) ? WallH * 2f : WallH;
            Box("Pillar_" + p.x + "_" + p.y, parent, new Vector3(p.x * U, height / 2f, (Rows - p.y) * U), new Vector3(0.36f, height, 0.36f), ConcreteMat(ZCorridor, "Pillar", ZCorridor.ceiling), true);
        }
    }

    static void TouchVertices(Edge e, Dictionary<Vector2Int, int> h, Dictionary<Vector2Int, int> v, HashSet<Vector2Int> tall)
    {
        Vector2Int p1 = e.v ? new Vector2Int(e.c + 1, e.r) : new Vector2Int(e.c, e.r + 1);
        Vector2Int p2 = new Vector2Int(e.c + 1, e.r + 1);
        if (e.tall) { tall.Add(p1); tall.Add(p2); }
        var dict = e.v ? v : h;
        dict[p1] = dict.TryGetValue(p1, out int a) ? a + 1 : 1;
        dict[p2] = dict.TryGetValue(p2, out int b) ? b + 1 : 1;
    }

    // ---------------------------------------------------------------- Suelos y techos

    static void BuildFloorsAndCeilings()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                char ch = At(c, r);
                if (ch == ' ') continue;
                Zone z = StyleOf(ch);
                Vector3 p = CellCenter(c, r);
                Transform fp = Lazy(RoomName(ch), floors), cp = Lazy(RoomName(ch), ceilings);
                Box("Floor_" + c + "_" + r, fp, new Vector3(p.x, -0.1f, p.z), new Vector3(U, 0.2f, U), FloorMat(z), false);
                Box("Ceiling_" + c + "_" + r, cp, new Vector3(p.x, HeightOf(ch) + 0.1f, p.z), new Vector3(U, 0.2f, U), ConcreteMat(z, "Ceiling", z.ceiling), false);
            }

        // Un único collider para todo el suelo plano
        GameObject col = new GameObject("FloorCollider");
        col.transform.SetParent(floors);
        BoxCollider box = col.AddComponent<BoxCollider>();
        col.transform.position = new Vector3(Cols * U / 2f, -0.5f, Rows * U / 2f);
        box.size = new Vector3(Cols * U, 1f, Rows * U);
    }

    static Material FloorMat(Zone z)
    {
        string src = z.clinicFloor ? "hospital_floor_slab_Worn_resilient_clinic_floor" : "hospital_floor_slab_Exposed_slab_concrete";
        return Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + src + ".mat"), z, "Floor", z.floor, 0.22f + z.wet * 0.8f);
    }

    static Material ConcreteMat(Zone z, string role, Color tint) =>
        Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + "hospital_floor_slab_Exposed_slab_concrete.mat"), z, role, tint, 0.12f);

    // ---------------------------------------------------------------- Escalera y galería de la cámara de pruebas
    // TEMP: peldaños de bloque (contrahuella 20 cm, huella 30 cm). Reemplazar por un modelo de escalera cuando haya uno.
    // La escalera sube de este a oeste por la fila norte de la cámara (x 24,8 -> 20) hasta la galería (x 18-20, a 3,2 m); delante hay un rellano de 1,2 m.

    static void BuildStairsAndGallery()
    {
        Zone z = ZChamber;
        Material mat = ConcreteMat(z, "Stairs", z.plaster * 0.95f);   // más claro que el suelo: los peldaños se leen sin linterna
        stairsGroup = T("Stairs", architecture);
        for (int i = 1; i <= 16; i++)
        {
            float top = 0.2f * i;
            float x1 = 24.8f - 0.3f * (i - 1), x0 = 24.8f - 0.3f * i;   // el rellano de entrada (x 24,8-26) queda libre: se sube desde el sur y luego hacia el oeste
            Box("TEMP_Step_" + i.ToString("00"), stairsGroup, new Vector3((x0 + x1) / 2f, top / 2f, 20.94f), new Vector3(x1 - x0, top, 1.88f), mat, true);
        }

        structural = Lazy("StructuralDetails", architecture);
        Box("Gallery_Platform", structural, new Vector3(19f, WallH - 0.1f, 18f), new Vector3(2f, 0.2f, 8f), ConcreteMat(z, "Gallery", z.floor), true);
        // Barandilla: pasamanos, travesaño y montantes (el collider es una sola pieza continua)
        Material metal = Mat("RailMetal", new Color(0.20f, 0.17f, 0.15f), 0.4f, "Metal", 0.6f);
        GameObject rail = Box("Gallery_Railing", structural, new Vector3(19.92f, WallH + 0.52f, 17f), new Vector3(0.06f, 0.06f, 6.0f), metal, false);
        Box("Gallery_Railing_Mid", structural, new Vector3(19.92f, WallH + 0.28f, 17f), new Vector3(0.04f, 0.04f, 6.0f), metal, false);
        for (int i = 0; i <= 6; i++) Box("Gallery_Post_" + i, structural, new Vector3(19.92f, WallH + 0.26f, 14.0f + i), new Vector3(0.05f, 0.52f, 0.05f), metal, false);
        BoxCollider rc = rail.AddComponent<BoxCollider>();
        rc.center = new Vector3(0, -0.25f, 0); rc.size = new Vector3(0.2f, 1.1f, 6.0f);
    }

    // ---------------------------------------------------------------- Mallas y materiales

    // Caja con UV proporcionales al tamaño real (así la textura no se estira en bloques de distinto tamaño)
    static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size, Material mat, bool collider)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if (collider) go.AddComponent<BoxCollider>().size = size;
        return go;
    }

    static Mesh BoxMesh(Vector3 size)
    {
        string key = size.x.ToString("F3") + "_" + size.y.ToString("F3") + "_" + size.z.ToString("F3");
        if (boxMeshes.TryGetValue(key, out Mesh cached)) return cached;

        const float uvPerMeter = 0.25f;
        Vector3 h = size / 2f;
        var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var norms = new List<Vector3>(); var tris = new List<int>();
        void Face(Vector3 n, Vector3 u, Vector3 v, float w, float hh)
        {
            int i0 = verts.Count;
            Vector3 o = n * (Mathf.Abs(n.x) * h.x + Mathf.Abs(n.y) * h.y + Mathf.Abs(n.z) * h.z);
            verts.Add(o - u * w / 2f - v * hh / 2f); verts.Add(o + u * w / 2f - v * hh / 2f);
            verts.Add(o + u * w / 2f + v * hh / 2f); verts.Add(o - u * w / 2f + v * hh / 2f);
            uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(w * uvPerMeter, 0)); uvs.Add(new Vector2(w * uvPerMeter, hh * uvPerMeter)); uvs.Add(new Vector2(0, hh * uvPerMeter));
            for (int k = 0; k < 4; k++) norms.Add(n);
            tris.AddRange(new[] { i0, i0 + 2, i0 + 1, i0, i0 + 3, i0 + 2 });
        }
        Face(Vector3.right, Vector3.forward, Vector3.up, size.z, size.y);
        Face(Vector3.left, Vector3.back, Vector3.up, size.z, size.y);
        Face(Vector3.up, Vector3.right, Vector3.forward, size.x, size.z);
        Face(Vector3.down, Vector3.right, Vector3.back, size.x, size.z);
        Face(Vector3.forward, Vector3.left, Vector3.up, size.x, size.y);
        Face(Vector3.back, Vector3.right, Vector3.up, size.x, size.y);

        Mesh m = new Mesh { name = "Box_" + key };
        m.SetVertices(verts); m.SetUVs(0, uvs); m.SetNormals(norms); m.SetTriangles(tris, 0);
        m.RecalculateTangents();
        m.RecalculateBounds();
        boxMeshes[key] = m;
        return m;
    }

    // Copia del material de un pack con un tinte de la paleta del nivel (conserva texturas, normales y demás mapas PBR).
    // El material original del pack no se modifica.
    static Material Tinted(Material src, Zone zone, string role, Color tint, float smoothness, float metallic = -1f)
    {
        if (src == null) return Mat(role + "_" + zone.name, tint, smoothness, "Fallback");
        string key = "L3_" + role + "_" + zone.name + "_" + src.name;
        if (materials.TryGetValue(key, out Material have)) return have;

        Material m = new Material(src) { name = key };
        Color baseColor = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : Color.white;
        m.SetColor("_BaseColor", new Color(baseColor.r * tint.r, baseColor.g * tint.g, baseColor.b * tint.b, baseColor.a));
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
        if (metallic >= 0f && m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(m, MatDir + key + ".mat");
        materials[key] = m;
        return m;
    }

    // Material liso propio del nivel (sin textura), para primitivas temporales
    static Material Mat(string name, Color color, float smoothness, string role, float metallic = 0f)
    {
        string key = "L3_" + role + "_" + name;
        if (materials.TryGetValue(key, out Material have)) return have;
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(m, MatDir + key + ".mat");
        materials[key] = m;
        return m;
    }

    // Decal / rótulo: textura con transparencia recortada, apoyada sobre pared o suelo
    static Material DecalMat(string key, Texture2D tex, Color tint, float smoothness = 0.1f)
    {
        string name = "L3_Decal_" + key;
        if (materials.TryGetValue(name, out Material have)) return have;
        Material m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
        m.SetTexture("_BaseMap", tex);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.35f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.renderQueue = 2450;
        AssetDatabase.CreateAsset(m, MatDir + name + ".mat");
        materials[name] = m;
        return m;
    }

    static void Retint(GameObject go, Zone zone)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string n = mats[i].name.ToLower();
                if (n.StartsWith("l3_")) continue;
                bool enamel = n.Contains("enamel") || n.Contains("green");
                mats[i] = Tinted(mats[i], zone, "Wall", enamel ? zone.enamel : zone.plaster, Mathf.Clamp01(0.3f + zone.wet * 0.4f));
            }
            r.sharedMaterials = mats;
        }
    }

    // ---------------------------------------------------------------- Atmósfera

    static void BuildAtmosphere()
    {
        // Más oscuro que el Nivel 2 (ambiente 0,105-0,15; luminancia ~0,23), pero con base cálida suficiente para reconocer
        // la arquitectura sin linterna. El contraste lo ponen las lámparas de cada sala. Se calibra con capturas desde la cámara del jugador.
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.085f, 0.070f, 0.058f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.06f, 0.045f, 0.035f);
        RenderSettings.fogDensity = 0.016f;

        VolumeProfile profile = NewProfile("Assets/_Project/Settings/Level03_Volume.asset");
        AddEffect<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        Vignette vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.36f);
        vignette.smoothness.Override(0.55f);
        FilmGrain grain = AddEffect<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.24f);
        ColorAdjustments color = AddEffect<ColorAdjustments>(profile);
        color.saturation.Override(-14f);
        color.contrast.Override(16f);
        color.postExposure.Override(0.1f);   // nivel más oscuro que el 2 (medido con la cámara del jugador, sin linterna): ver informe
        WhiteBalance wb = AddEffect<WhiteBalance>(profile);
        wb.temperature.Override(14f);   // gradación cálida: sepia y ámbar, opuesta al azul petróleo del Nivel 2
        wb.tint.Override(4f);
        Bloom bloom = AddEffect<Bloom>(profile);
        bloom.intensity.Override(0.4f);
        bloom.threshold.Override(1f);
        EditorUtility.SetDirty(profile);

        Transform global = T("GlobalVolume", postRoot);
        Volume v = global.gameObject.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;
    }

    static VolumeProfile NewProfile(string path)
    {
        if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) AssetDatabase.DeleteAsset(path);
        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        AssetDatabase.CreateAsset(profile, path);
        return profile;
    }

    static T AddEffect<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T effect = profile.Add<T>(true);
        AssetDatabase.AddObjectToAsset(effect, profile);
        return effect;
    }

    // Volúmenes locales: el laboratorio (blanco enfermizo, desaturado) y el vestíbulo de S-9 (más cerrado y rojizo)
    static void BuildLocalVolumes()
    {
        Transform parent = T("LocalVolumes", postRoot);

        VolumeProfile lab = NewProfile("Assets/_Project/Settings/Level03_Lab_Volume.asset");
        WhiteBalance wb = AddEffect<WhiteBalance>(lab);
        wb.temperature.Override(-22f);
        ColorAdjustments ca = AddEffect<ColorAdjustments>(lab);
        ca.saturation.Override(-22f);
        EditorUtility.SetDirty(lab);
        MakeLocalVolume("LabVolume", parent, lab, new Vector3(14f, 1.6f, 17f), new Vector3(7.6f, 3.2f, 5.6f));

        VolumeProfile s9 = NewProfile("Assets/_Project/Settings/Level03_S9_Volume.asset");
        Vignette v = AddEffect<Vignette>(s9);
        v.intensity.Override(0.5f);
        v.smoothness.Override(0.6f);
        ColorAdjustments ca2 = AddEffect<ColorAdjustments>(s9);
        ca2.postExposure.Override(0.05f);
        ca2.colorFilter.Override(new Color(1f, 0.93f, 0.88f));
        EditorUtility.SetDirty(s9);
        MakeLocalVolume("VestibuleS9Volume", parent, s9, new Vector3(18f, 1.6f, 25f), new Vector3(3.6f, 3.2f, 5.6f));
    }

    static void MakeLocalVolume(string name, Transform parent, VolumeProfile profile, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        Volume vol = go.AddComponent<Volume>();
        vol.isGlobal = false;
        vol.blendDistance = 1.5f;
        vol.sharedProfile = profile;
    }

    // ---------------------------------------------------------------- Jugador y audio

    static Flashlight playerFlashlight;
    static PlayerHealth playerHealth;

    static void BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(gameplayRoot, false);
        player.tag = "Player";
        // Aparece frente al montacargas que lo trajo (la cabina queda a su espalda) y mirando hacia la salida de la sala (recepción, al oeste)
        player.transform.position = new Vector3(31.9f, 0.05f, 4.9f);
        player.transform.rotation = Quaternion.Euler(0, 270f, 0);   // de espaldas a la cabina (las puertas del montacargas quedan atrás), mirando hacia la recepción
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f; cc.radius = 0.3f; cc.center = new Vector3(0, 0.9f, 0); cc.stepOffset = 0.3f;
        PlayerController controller = player.AddComponent<PlayerController>();
        controller.walkSpeed = 2.6f;
        PlayerHealth health = player.AddComponent<PlayerHealth>();
        health.controller = controller;

        GameObject cam = new GameObject("PlayerCamera");
        cam.tag = "MainCamera";
        cam.transform.SetParent(player.transform, false);
        cam.transform.localPosition = new Vector3(0, 1.6f, 0);
        Camera camera = cam.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.farClipPlane = 80f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cam.AddComponent<AudioListener>();
        cam.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = true;
        cam.AddComponent<PlayerInteract>();
        controller.playerCamera = cam.transform;

        GameObject spotGo = new GameObject("FlashlightSpot");
        spotGo.transform.SetParent(cam.transform, false);
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 25f; spot.spotAngle = 62f; spot.innerSpotAngle = 30f;
        spot.intensity = 12f;
        spot.color = new Color(1f, 0.95f, 0.82f);
        spot.shadows = LightShadows.Soft;

        Flashlight flashlight = cam.AddComponent<Flashlight>();
        flashlight.spot = spot;
        flashlight.hasFlashlight = true;
        flashlight.maxIntensity = 12f;
        playerFlashlight = flashlight;
        playerHealth = health;
    }

    static void BuildAudio()
    {
        GameObject go = new GameObject("Ambience_ResearchWing");
        go.transform.SetParent(T("AmbientAudio", audioRoot), false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientClip);
        src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        src.volume = 0.16f;
        src.pitch = 0.82f;   // el mismo ambiente que el Nivel 2, más grave: único clip disponible (TEMP hasta tener audio propio del nivel)
    }
}
