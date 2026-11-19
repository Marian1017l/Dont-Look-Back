using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BuildKit;

// Menú "Don't Look Back > Crear Nivel 2 (Servicios)".
// Arma Assets/_Project/Scenes/Level02_Service.unity: las entrañas industriales del hospital (escaleras de bajada,
// mantenimiento, sala eléctrica, aislamiento, archivo restringido y un montacargas hacia el nivel inferior).
// El plano se define con rectángulos de celdas de 2 m (Fill); las paredes salen solas en los bordes entre zonas.
// Este archivo: plano, arquitectura, materiales, jugador, atmósfera y salida.
// Level02Builder.Dressing.cs: objetos, instalaciones, iluminación y documentos.
// Se puede ejecutar de nuevo para regenerar la escena (sobrescribe el archivo).
public static partial class Level02Builder
{
    const string ScenePath = "Assets/_Project/Scenes/Level02_Service.unity";
    const string FH = "Assets/FunctionalHorrorProps/Prefabs/";
    const string FHMat = "Assets/FunctionalHorrorProps/Materials/";
    const string SP = "Assets/Survival Props/prefabs/";
    const string AS = "Assets/Abandoned_Asylum/Prefabs/";
    const string MatDir = "Assets/_Project/Art/Materials/Level02/";
    const string AmbientClip = "Assets/Sounds/jorisvermeer-horror-background-atmosphere-030-394196.mp3";

    const float U = 2f;          // lado de una celda del plano (m)
    const float WallH = 3.2f;    // alto de un piso (medido del módulo de pared de FunctionalHorrorProps)
    const int Cols = 28, Rows = 16;
    const float StepRise = 0.2f, StepRun = 0.5f;

    // ---------------------------------------------------------------- Plano
    //  A  escaleras (4x12 m, doble alto)      T  tránsito (4x6)            B  mantenimiento (12x8 + L de 4x6)
    //  W  almacén antiguo (8x6)               C  sala eléctrica (12x8)     E  archivo restringido (8x8)
    //  G  zona final: montacargas (6x8)       h  pasillo de aislamiento    i j k l m  celdas de aislamiento (4x4)
    //  f  pasillo de servicio 1 (B -> C)      g  pasillo de servicio 2 (C -> aislamiento)   n  pasillo de servicio 3 (aislamiento -> archivo)
    static readonly char[,] grid = new char[Rows, Cols];

    static void DefineMap()
    {
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) grid[r, c] = ' ';
        Fill('A', 0, 0, 1, 5);
        Fill('T', 0, 6, 1, 8);
        Fill('B', 2, 6, 7, 9);
        Fill('B', 2, 10, 3, 12);
        Fill('f', 8, 7, 12, 7);
        Fill('f', 10, 8, 10, 8);
        Fill('W', 9, 9, 12, 11);
        Fill('C', 13, 5, 18, 8);
        Fill('g', 15, 9, 15, 12);
        Fill('h', 9, 13, 22, 13);
        Fill('i', 10, 14, 11, 15);
        Fill('j', 12, 14, 13, 15);
        Fill('k', 14, 14, 15, 15);
        Fill('l', 16, 14, 17, 15);
        Fill('m', 18, 14, 19, 15);
        Fill('n', 22, 9, 22, 12);
        Fill('E', 21, 5, 24, 8);
        Fill('G', 25, 5, 27, 8);
    }

    static void Fill(char ch, int c0, int r0, int c1, int r1)
    {
        for (int r = r0; r <= r1; r++) for (int c = c0; c <= c1; c++) grid[r, c] = ch;
    }

    // Huecos de paso de 2 m (sin pared). c, r y lado (N/E/S/W) de la celda. La fila 0 es el norte.
    static readonly (int c, int r, char d)[] Gaps =
    {
        (0, 5, 'S'), (1, 5, 'S'),   // escaleras -> tránsito (4 m abiertos)
        (1, 7, 'E'),                // tránsito -> mantenimiento
        (7, 7, 'E'),                // mantenimiento -> pasillo 1
        (10, 8, 'S'),               // pasillo 1 -> almacén antiguo
        (12, 7, 'E'),               // pasillo 1 -> sala eléctrica
        (15, 8, 'S'),               // sala eléctrica -> pasillo 2
        (15, 12, 'S'),              // pasillo 2 -> aislamiento (reja que se abre al restaurar la energía)
        (22, 12, 'S'),              // aislamiento -> pasillo 3
        (22, 8, 'S'),               // pasillo 3 -> archivo (reja con tarjeta)
        (24, 6, 'E'),               // archivo -> zona final
    };

    // Puertas de celda: módulo de pared con hueco de puerta (4 m), ocupa esta arista y la contigua hacia el este/sur
    static readonly (int c, int r, char d)[] DoorwayModules =
    {
        (10, 13, 'S'), (12, 13, 'S'), (14, 13, 'S'), (16, 13, 'S'), (18, 13, 'S'),
    };

    static bool IsRoom(char c) => c != ' ' && c != 'f' && c != 'g' && c != 'n' && c != 'h';
    static bool IsTall(char c) => c == 'A' || c == 'G';    // doble alto (6,4 m)
    static float HeightOf(char c) => IsTall(c) ? WallH * 2f : WallH;

    static char At(int c, int r) => (r < 0 || r >= Rows || c < 0 || c >= Cols) ? ' ' : grid[r, c];
    static float XC(int c) => c * U + U / 2f;
    static float ZC(int r) => (Rows - 1 - r) * U + U / 2f;
    static Vector3 CellCenter(int c, int r) => new Vector3(XC(c), 0, ZC(r));

    // ---------------------------------------------------------------- Estilos por zona (paleta del nivel)

    struct Zone
    {
        public string name;
        public Color plaster, enamel, floor, ceiling;
        public float wet;   // brillo extra: humedad
        public Zone(string n, Color p, Color e, Color f, Color c, float w) { name = n; plaster = p; enamel = e; floor = f; ceiling = c; wet = w; }
    }

    static readonly Zone ZStairs = new Zone("Stairs", new Color(0.62f, 0.67f, 0.66f), new Color(0.40f, 0.52f, 0.49f), new Color(0.52f, 0.58f, 0.6f), new Color(0.38f, 0.44f, 0.46f), 0.15f);
    static readonly Zone ZTransit = new Zone("Transit", new Color(0.50f, 0.57f, 0.58f), new Color(0.32f, 0.45f, 0.44f), new Color(0.45f, 0.52f, 0.55f), new Color(0.34f, 0.4f, 0.42f), 0.2f);
    static readonly Zone ZService = new Zone("Service", new Color(0.40f, 0.48f, 0.51f), new Color(0.27f, 0.40f, 0.42f), new Color(0.40f, 0.46f, 0.5f), new Color(0.3f, 0.36f, 0.4f), 0.3f);
    static readonly Zone ZElectric = new Zone("Electric", new Color(0.37f, 0.43f, 0.45f), new Color(0.30f, 0.38f, 0.35f), new Color(0.36f, 0.42f, 0.46f), new Color(0.28f, 0.33f, 0.36f), 0.3f);
    static readonly Zone ZIsolation = new Zone("Isolation", new Color(0.46f, 0.52f, 0.52f), new Color(0.33f, 0.44f, 0.44f), new Color(0.4f, 0.47f, 0.5f), new Color(0.3f, 0.36f, 0.38f), 0.35f);
    static readonly Zone ZArchive = new Zone("Archive", new Color(0.52f, 0.5f, 0.44f), new Color(0.36f, 0.41f, 0.35f), new Color(0.45f, 0.45f, 0.42f), new Color(0.34f, 0.36f, 0.35f), 0.15f);
    static readonly Zone ZFinal = new Zone("Final", new Color(0.31f, 0.37f, 0.39f), new Color(0.23f, 0.31f, 0.31f), new Color(0.32f, 0.38f, 0.42f), new Color(0.24f, 0.29f, 0.32f), 0.4f);

    static Zone StyleOf(char c)
    {
        switch (c)
        {
            case 'A': return ZStairs;
            case 'T': return ZTransit;
            case 'C': return ZElectric;
            case 'E': return ZArchive;
            case 'G': return ZFinal;
            case 'h': case 'i': case 'j': case 'k': case 'l': case 'm': return ZIsolation;
            default: return ZService;
        }
    }

    // ---------------------------------------------------------------- Jerarquía y estado de la construcción

    static Transform environment, architecture, floors, walls, ceilings, pillars, stairsGroup;
    static Transform roomsParent, corridorsParent, propsRoot, lightingRoot, gameplayRoot, audioRoot, postRoot;
    static Transform pickups, keysGroup, documentsGroup, doorsGroup, interactablesGroup, exitGroup;
    static readonly Dictionary<string, Transform> groups = new Dictionary<string, Transform>();
    static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
    static readonly Dictionary<string, Mesh> boxMeshes = new Dictionary<string, Mesh>();

    class Edge
    {
        public int c, r; public bool v;        // v = pared vertical (arista este de la celda); si no, arista sur
        public char a, b;                      // celdas a ambos lados (' ' = exterior)
        public bool gap, doorStart, doorSecond;
        public bool flip;                      // la cara sucia del módulo mira hacia el lado correcto
        public bool tall;
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

    [MenuItem("Don't Look Back/Crear Nivel 2 (Servicios)")]
    public static void Build() => BuildLevel(true);

    // interactive = false lo usa el asistente (sin diálogos); aborta si hay otra escena sin guardar
    public static void BuildLevel(bool interactive)
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Level02Builder: detén el juego antes de crear el nivel.");
            return;
        }
        if (interactive) { if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return; }
        else if (EditorSceneManager.GetActiveScene().isDirty) { Debug.LogError("Level02Builder: hay cambios sin guardar en la escena abierta."); return; }

        MaterialConverter.ConvertAll();   // los packs traen materiales de otro shader que se ven rosados en URP
        EnsureFolder("Assets/_Project", "Scenes");
        EnsureFolder("Assets/_Project", "Settings");
        EnsureFolder("Assets/_Project/Art", "Materials");
        AssetDatabase.DeleteAsset(MatDir.TrimEnd('/'));   // los materiales del nivel se regeneran siempre
        EnsureFolder("Assets/_Project/Art/Materials", "Level02");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        groups.Clear(); materials.Clear(); boxMeshes.Clear(); edges.Clear(); edgeMap.Clear();
        DefineMap();

        environment = T("Environment");
        architecture = T("Architecture", environment);
        floors = T("Floors", architecture);
        walls = T("Walls", architecture);
        ceilings = T("Ceilings", architecture);
        pillars = T("Pillars", architecture);
        stairsGroup = T("Stairs", architecture);
        propsRoot = T("Props");
        lightingRoot = T("Lighting");
        gameplayRoot = T("Gameplay");
        audioRoot = T("Audio");
        postRoot = T("PostProcessing");

        BuildEdges();
        BuildWalls();
        BuildPillars();
        BuildFloorsAndCeilings();
        BuildStairs();
        BuildAtmosphere();
        BuildPlayer();
        BuildAudio();
        BuildUI();
        BuildDressing();   // Level02Builder.Dressing.cs

        foreach (Transform t in architecture.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;
        TidyHierarchy();

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Nivel 2 creado en " + ScenePath + " (" + Cols * U + " x " + Rows * U + " m).");
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
            else Debug.LogWarning("Level02Builder: el hueco (" + g.c + "," + g.r + "," + g.d + ") no está entre dos zonas.");
        }
        foreach (var d in DoorwayModules)
        {
            var key = Canon(d.c, d.r, d.d);
            var key2 = (key.Item1 + (key.Item3 ? 0 : 1), key.Item2 + (key.Item3 ? 1 : 0), key.Item3);
            if (edgeMap.TryGetValue(key, out Edge e1) && edgeMap.TryGetValue(key2, out Edge e2)) { e1.doorStart = true; e2.doorSecond = true; }
            else Debug.LogWarning("Level02Builder: la puerta de celda (" + d.c + "," + d.r + "," + d.d + ") no encaja.");
        }
    }

    static void AddEdge(int c, int r, bool v, char a, char b)
    {
        // La cara sucia del módulo mira hacia la habitación (o hacia la celda que existe si el otro lado es exterior)
        bool faceB = (IsRoom(b) && !IsRoom(a)) || a == ' ';
        char faceCh = faceB ? b : a;
        var e = new Edge
        {
            c = c, r = r, v = v, a = a, b = b,
            flip = v ^ faceB,
            tall = IsTall(a) || IsTall(b),
            zone = StyleOf(faceCh),
        };
        edges.Add(e);
        edgeMap[(c, r, v)] = e;
    }

    static void BuildWalls()
    {
        // Horizontales agrupadas por línea (r) y ordenadas por c; verticales por columna (c) y ordenadas por r
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
                if (e.doorSecond || e.gap) continue;
                if (e.doorStart)
                {
                    Edge f = (i + 1 < n && Next(e, line[i + 1])) ? line[i + 1] : null;
                    PlaceDoorway(e, f);
                    continue;
                }
                if (i + 1 < n && Mergeable(e, line[i + 1], false)) { PlaceWall("hospital_wall_solid", e, line[i + 1], 0, true); i++; }
                else PlaceWall("hospital_wall_solid", e, null, 0, true);
            }

            for (int i = 0; i < n; i++)   // segundo piso de pared en las zonas de doble alto (también sobre los huecos)
            {
                Edge e = line[i];
                if (!e.tall) continue;
                if (i + 1 < n && Mergeable(e, line[i + 1], true)) { PlaceWall("hospital_wall_solid", e, line[i + 1], 1, false); i++; }
                else PlaceWall("hospital_wall_solid", e, null, 1, false);
            }

            foreach (Edge e in line) if (e.gap && !e.tall) PlaceLintel(e);
        }
    }

    static bool Next(Edge e, Edge f) => f.v == e.v && (e.v ? (f.c == e.c && f.r == e.r + 1) : (f.r == e.r && f.c == e.c + 1));

    static bool Mergeable(Edge e, Edge f, bool tier2)
    {
        if (!Next(e, f)) return false;
        if (f.flip != e.flip || f.zone.name != e.zone.name) return false;
        if (tier2) return f.tall;
        return !f.gap && !f.doorStart && !f.doorSecond;
    }

    static void PlaceWall(string prefab, Edge e, Edge f, int tier, bool collider)
    {
        GameObject wall = Spawn(FH + prefab + ".prefab", walls);
        if (wall == null) return;
        Vector3 mid = f == null ? e.Mid : (e.Mid + f.Mid) / 2f;
        wall.name = (tier == 1 ? "WallUpper_" : "Wall_") + e.c + "_" + e.r + (e.v ? "_E" : "_S");
        wall.transform.rotation = Quaternion.Euler(0, (e.v ? 90f : 0f) + (e.flip ? 180f : 0f), 0);
        wall.transform.localScale = new Vector3(f == null ? 0.5f : 1f, 1f, 1f);   // una arista mide 2 m; el módulo, 4 m
        SnapXZ(wall, mid.x, mid.z);
        SnapY(wall, tier * WallH, false);
        Retint(wall, e.zone);

        if (!collider) return;
        Bounds b = GetBounds(wall);
        BoxCollider box = wall.AddComponent<BoxCollider>();
        box.center = wall.transform.InverseTransformPoint(b.center);
        box.size = new Vector3(4f, WallH, 0.3f);   // en el espacio local del módulo (la escala ya acorta los de 2 m)
    }

    // Módulo con hueco de puerta (1,36 x 2,35 m) centrado entre dos aristas. La puerta del hueco se coloca en la ambientación.
    static void PlaceDoorway(Edge e, Edge f)
    {
        GameObject wall = Spawn(FH + "hospital_wall_doorway.prefab", walls);
        if (wall == null) return;
        Vector3 mid = f == null ? e.Mid : (e.Mid + f.Mid) / 2f;
        wall.name = "Doorway_" + e.c + "_" + e.r + (e.v ? "_E" : "_S");
        wall.transform.rotation = Quaternion.Euler(0, (e.v ? 90f : 0f) + (e.flip ? 180f : 0f), 0);
        SnapXZ(wall, mid.x, mid.z);
        SnapY(wall, 0f, false);
        Retint(wall, e.zone);

        // Tres colisiones (jamba izquierda, jamba derecha y dintel) para que el hueco sea el único paso
        Bounds b = GetBounds(wall);
        Vector3 center = wall.transform.InverseTransformPoint(b.center);
        const float gapW = 1.36f, gapH = 2.35f, side = (4f - gapW) / 2f;
        foreach (float sign in new[] { -1f, 1f })
        {
            BoxCollider jamb = wall.AddComponent<BoxCollider>();
            jamb.center = center + new Vector3(sign * (gapW / 2f + side / 2f), 0, 0);
            jamb.size = new Vector3(side, WallH, 0.3f);
        }
        BoxCollider lintel = wall.AddComponent<BoxCollider>();
        lintel.center = center + new Vector3(0, (gapH + WallH) / 2f - WallH / 2f, 0);
        lintel.size = new Vector3(gapW, WallH - gapH, 0.3f);
    }

    // Viga sobre los huecos de 2 m: da forma de puerta y evita ver la parte alta de la pared vacía
    static void PlaceLintel(Edge e)
    {
        Vector3 size = e.v ? new Vector3(0.34f, 0.8f, U) : new Vector3(U, 0.8f, 0.34f);
        Box("Lintel_" + e.c + "_" + e.r + (e.v ? "_E" : "_S"), walls, new Vector3(e.Mid.x, WallH - 0.4f, e.Mid.z), size, ConcreteMat(e.zone, "Lintel", e.zone.ceiling * 0.9f), false);
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

        foreach (Vector2Int p in all)
        {
            hCount.TryGetValue(p, out int h);
            vCount.TryGetValue(p, out int v);
            gapH.TryGetValue(p, out int gh);
            gapV.TryGetValue(p, out int gv);
            bool straight = (h == 2 && v == 0 && gh == 0 && gv == 0) || (v == 2 && h == 0 && gh == 0 && gv == 0);
            bool betweenGaps = h == 0 && v == 0 && (gh == 2 || gv == 2);   // vértice en medio de un paso de 4 m: sin pilar
            if (straight || betweenGaps) continue;
            float height = tall.Contains(p) ? WallH * 2f : WallH;
            Box("Pillar_" + p.x + "_" + p.y, pillars, new Vector3(p.x * U, height / 2f, (Rows - p.y) * U), new Vector3(0.36f, height, 0.36f), ConcreteMat(ZService, "Pillar", ZService.ceiling), true);
        }
    }

    // Vértices de una arista (en cuadrícula de celdas: x = columna, y = fila hacia el sur)
    static void TouchVertices(Edge e, Dictionary<Vector2Int, int> h, Dictionary<Vector2Int, int> v, HashSet<Vector2Int> tall)
    {
        Vector2Int p1 = e.v ? new Vector2Int(e.c + 1, e.r) : new Vector2Int(e.c, e.r + 1);
        Vector2Int p2 = new Vector2Int(e.c + 1, e.r + 1);
        if (e.tall) { tall.Add(p1); tall.Add(p2); }
        var dict = e.v ? v : h;
        dict[p1] = dict.TryGetValue(p1, out int a) ? a + 1 : 1;
        dict[p2] = dict.TryGetValue(p2, out int b) ? b + 1 : 1;
    }

    // ---------------------------------------------------------------- Suelos, techos y escaleras

    // Alto del suelo de una celda de las escaleras (las filas 1-2 y 4-5 son rampas de peldaños)
    static float StairFloor(int r) => r == 0 ? WallH : r == 3 ? WallH / 2f : 0f;

    static void BuildFloorsAndCeilings()
    {
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                char ch = At(c, r);
                if (ch == ' ') continue;
                Zone z = StyleOf(ch);
                Vector3 p = CellCenter(c, r);

                bool clinic = ch == 'T' || ch == 'E' || ch == 'A';
                if (!(ch == 'A' && r != 0 && r != 3 && r != 6))   // las celdas de peldaños llevan su propio bloque
                {
                    if (ch == 'A')
                    {
                        float top = StairFloor(r);
                        Box("Floor_" + c + "_" + r, floors, new Vector3(p.x, top / 2f, p.z), new Vector3(U, top, U), FloorMat(z, clinic), true);
                    }
                    else Box("Floor_" + c + "_" + r, floors, new Vector3(p.x, -0.1f, p.z), new Vector3(U, 0.2f, U), FloorMat(z, clinic), false);
                }
                Box("Ceiling_" + c + "_" + r, ceilings, new Vector3(p.x, HeightOf(ch) + 0.1f, p.z), new Vector3(U, 0.2f, U), ConcreteMat(z, "Ceiling", z.ceiling), false);
            }

        // Un único collider para todo el suelo plano
        GameObject col = new GameObject("FloorCollider");
        col.transform.SetParent(floors);
        BoxCollider box = col.AddComponent<BoxCollider>();
        col.transform.position = new Vector3(Cols * U / 2f, -0.5f, Rows * U / 2f);
        box.size = new Vector3(Cols * U, 1f, Rows * U);
    }

    static Material FloorMat(Zone z, bool clinic)
    {
        string src = clinic ? "hospital_floor_slab_Worn_resilient_clinic_floor" : "hospital_floor_slab_Exposed_slab_concrete";
        return Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + src + ".mat"), z, "Floor", z.floor, 0.25f + z.wet);
    }

    // Hormigón del pack (con sus mapas) tintado para techos, vigas y pilares
    static Material ConcreteMat(Zone z, string role, Color tint) =>
        Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + "hospital_floor_slab_Exposed_slab_concrete.mat"), z, role, tint, 0.12f);

    // TEMP: peldaños de bloque con proporciones reales (contrahuella 20 cm, huella 50 cm). Reemplazar por un modelo de escalera.
    static void BuildStairs()
    {
        float width = 2 * U - 0.3f;   // ancho libre entre paredes
        float x = U;                  // centro de las columnas 0-1
        Material mat = FloorMat(ZStairs, false);
        BuildFlight("Flight1", 1, WallH, width, x, mat);
        BuildFlight("Flight2", 4, WallH / 2f, width, x, mat);
    }

    static void BuildFlight(string name, int firstRow, float startY, float width, float x, Material mat)
    {
        float zNorth = (Rows - firstRow) * U;   // borde norte de la primera fila de la rampa
        for (int i = 0; i < 8; i++)
        {
            float top = startY - StepRise * (i + 1);
            if (top < 0.05f) continue;
            float z = zNorth - StepRun * i - StepRun / 2f;
            Box("TEMP_Step_" + name + "_" + (i + 1).ToString("00"), stairsGroup, new Vector3(x, top / 2f, z), new Vector3(width, top, StepRun), mat, true);
        }
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

        const float uvPerMeter = 0.25f;   // una repetición de textura cada 4 m (como el módulo de suelo del pack)
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

    // Copia del material de un pack con un tinte de la paleta del nivel (conserva texturas, normales y demás mapas PBR)
    static Material Tinted(Material src, Zone zone, string role, Color tint, float smoothness, float metallic = -1f)
    {
        if (src == null) return Mat(role + "_" + zone.name, tint, smoothness, "Fallback");
        string key = "L2_" + role + "_" + zone.name + "_" + src.name;
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
        string key = "L2_" + role + "_" + name;
        if (materials.TryGetValue(key, out Material have)) return have;
        Shader sh = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(sh) { name = key };
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", metallic);
        AssetDatabase.CreateAsset(m, MatDir + key + ".mat");
        materials[key] = m;
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
                bool enamel = n.Contains("enamel") || n.Contains("green");
                mats[i] = Tinted(mats[i], zone, "Wall", enamel ? zone.enamel : zone.plaster, Mathf.Clamp01(0.3f + zone.wet * 0.4f));
            }
            r.sharedMaterials = mats;
        }
    }

    // ---------------------------------------------------------------- Atmósfera

    static void BuildAtmosphere()
    {
        // Más oscuro que el Nivel 1 (ambiente 0,17-0,23; luminancia media medida 0,42 frente a ~0,23 aquí): la forma de las salas se reconoce sin luces, pero el contraste lo ponen las lámparas
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.105f, 0.126f, 0.15f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.07f);
        RenderSettings.fogDensity = 0.014f;

        VolumeProfile profile = NewProfile("Assets/_Project/Settings/Level02_Volume.asset");
        AddEffect<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        Vignette vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.32f);
        vignette.smoothness.Override(0.55f);
        FilmGrain grain = AddEffect<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.22f);
        ColorAdjustments color = AddEffect<ColorAdjustments>(profile);
        color.saturation.Override(-10f);
        color.contrast.Override(14f);
        color.postExposure.Override(0.78f);
        WhiteBalance wb = AddEffect<WhiteBalance>(profile);
        wb.temperature.Override(-12f);   // gradación fría: azul petróleo
        wb.tint.Override(-4f);
        Bloom bloom = AddEffect<Bloom>(profile);
        bloom.intensity.Override(0.35f);
        bloom.threshold.Override(1f);
        EditorUtility.SetDirty(profile);

        Transform global = T("Global_Volume", postRoot);
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

    // ---------------------------------------------------------------- Jugador y audio

    static Transform playerTransform;

    static void BuildPlayer()
    {
        GameObject player = new GameObject("Player");
        player.transform.SetParent(gameplayRoot, false);
        player.tag = "Player";
        playerTransform = player.transform;
        // Empieza arriba de las escaleras, mirando hacia abajo (sur)
        player.transform.position = new Vector3(XC(0) + U / 2f, WallH + 0.05f, ZC(0));
        player.transform.rotation = Quaternion.Euler(0, 180f, 0);
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

    static Flashlight playerFlashlight;
    static PlayerHealth playerHealth;

    static void BuildAudio()
    {
        GameObject go = new GameObject("Ambience_Service");
        go.transform.SetParent(T("Ambient_Audio", audioRoot), false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientClip);
        src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        src.volume = 0.14f;
    }

    // Interfaz de diálogos: el mismo Canvas y EventSystem del Nivel 1 (prefabs en Prefabs/UI). HUD, inventario y lector de
    // documentos se dibujan por IMGUI desde sus scripts y no necesitan Canvas. El panel arranca oculto: lo activará el sistema de diálogos.
    static void BuildUI()
    {
        Transform ui = T("UI");
        GameObject canvas = Spawn("Assets/_Project/Prefabs/UI/Canvas_Dialogo.prefab", ui);
        Spawn("Assets/_Project/Prefabs/UI/EventSystem.prefab", ui);
        if (canvas == null) return;
        canvas.name = "Canvas";
        Transform panel = canvas.transform.Find("Panel");
        if (panel != null) panel.gameObject.SetActive(false);
    }

    // Ordena las raíces de la escena
    static void TidyHierarchy()
    {
        string[] order = { "Environment", "Props", "Lighting", "Gameplay", "Audio", "PostProcessing", "UI" };
        for (int i = 0; i < order.Length; i++)
        {
            GameObject go = GameObject.Find(order[i]);
            if (go != null) go.transform.SetSiblingIndex(i);
        }
    }
}
