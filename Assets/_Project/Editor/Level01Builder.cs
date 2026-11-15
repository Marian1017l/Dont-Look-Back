using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Menú "Don't Look Back > Crear Nivel 1 (Hospital)".
// Arma la escena Assets/_Project/Scenes/Level01_Ward.unity con los modelos de los packs.
// (La escena anterior Level01_Basement.unity se conserva sin tocar como respaldo.)
// El hospital se genera desde el mapa de texto de abajo: cada letra es una habitación,
// las paredes aparecen solas en los bordes y las "aperturas" son los huecos de puerta.
// Cada habitación se decora sola con una lista de objetos según su tema (método Dress).
// Se puede ejecutar de nuevo para regenerar la escena (sobrescribe el archivo).
public static class Level01Builder
{
    const string ScenePath = "Assets/_Project/Scenes/Level01_Ward.unity";
    const string FH = "Assets/FunctionalHorrorProps/Prefabs/";
    const string SP = "Assets/Survival Props/prefabs/";
    const string AS = "Assets/Abandoned_Asylum/Prefabs/";
    const string Paper_Prefab = "Assets/Abandoned_Asylum/Prefabs/Paper.prefab";
    const string AmbientClip = "Assets/Sounds/jorisvermeer-horror-background-atmosphere-030-394196.mp3";
    const string BreathClip = "Assets/Sounds/cryptologymedia-breathing-160445.mp3";
    const string CodeText = "472";   // 4 en la caldera, 7 entre las tuberías, 2 en la nota del archivo

    // Mapa: cada letra es una zona. ' ' = vacío. Cada celda mide "cell" metros (se mide del modelo del piso).
    // Celdas de 4 m: habitaciones de paciente = 1 celda (4x4 m); inicio, espera, enfermería y almacén = 2 celdas (8x4 m).
    //  Norte:  g = sala de espera (primera pista)   n = enfermería   b, c = pacientes   o = observación   f = celda de aislamiento
    //  Centro: x = pasillo principal   y = pasillo oscuro (tras la reja)   h = escalera de salida
    //  Sur:    a = cuarto donde despiertas   m = almacén médico (candado)   e = archivo   r = baño
    static readonly string[] Map =
    {
        "ggnnbcof    ",
        "xxxxxxxxyyyh",
        "aammer      ",
    };

    // Huecos de puerta: columna, fila y lado (N/E/S/W) de la celda. La fila 0 es el norte.
    static readonly (int c, int r, char d)[] Openings =
    {
        (1, 0, 'S'), (3, 0, 'S'), (4, 0, 'S'), (5, 0, 'S'), (6, 0, 'S'),   // espera, enfermería, pacientes y observación -> pasillo
        (1, 2, 'N'), (2, 2, 'N'), (4, 2, 'N'), (5, 2, 'N'),                // inicio (frente a la espera), almacén, archivo y baño -> pasillo
        (7, 1, 'E'), (10, 1, 'E'),                                         // reja con llave al pasillo oscuro, y vano a la escalera
    };

    // Paredes con cristal (no se atraviesan): la enfermería mira al pasillo y la observación mira a la celda
    static readonly (int c, int r, char d)[] Windows =
    {
        (2, 0, 'S'), (6, 0, 'E'),
    };

    static readonly (int dc, int dr, char name)[] Dirs = { (0, -1, 'N'), (1, 0, 'E'), (0, 1, 'S'), (-1, 0, 'W') };

    // Tema de cada zona: nombre del grupo en la jerarquía y lista de objetos con los que se decora
    static readonly Dictionary<char, string> RoomNames = new Dictionary<char, string>
    {
        { 'a', "A_Wake" }, { 'g', "G_Waiting" }, { 'n', "N_NurseStation" }, { 'b', "B_Patient1" }, { 'c', "C_Patient2_Escape" },
        { 'o', "O_Observation" }, { 'f', "F_IsolationCell" }, { 'm', "M_MedicalStorage" }, { 'e', "E_Archive" }, { 'r', "R_Bathroom" },
        { 'x', "X_Corridor" }, { 'y', "Y_DarkHall" }, { 'h', "H_Stairs" },
    };

    static float cell = 4f;       // ancho de una celda (medido)
    static float wallH = 3.2f;    // alto de pared (medido)
    static float wallBias, doorBias;
    static Material concrete;
    static GameObject toolChestBath, drawerArchive, tvNurse, gateTable;

    static Transform floors, walls, ceilings, pillars, props, lamps, pickups, documents, doorsGroup, clues, triggers, encounters;
    static readonly Dictionary<string, Transform> rooms = new Dictionary<string, Transform>();
    static readonly List<Rect> occupied = new List<Rect>();        // huellas (X,Z) de objetos ya colocados
    static readonly List<Vector2> reserved = new List<Vector2>();  // puntos que deben quedar libres (objetos recogibles, inicio)
    static readonly List<Vector2> doorPoints = new List<Vector2>();
    static System.Random rng;

    [MenuItem("Don't Look Back/Crear Nivel 1 (Hospital)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Don't Look Back", "Detén el juego (botón ▶) antes de crear el nivel.", "Entendido");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        MaterialConverter.ConvertAll();   // los packs traen materiales de otro shader que se ven rosados en URP
        EnsureFolder("Assets/_Project", "Scenes");
        EnsureFolder("Assets/_Project", "Settings");
        EnsureFolder("Assets/_Project/Art", "Materials");

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        rooms.Clear();
        occupied.Clear();
        reserved.Clear();
        doorPoints.Clear();
        rng = new System.Random(1017);   // misma decoración cada vez que se regenera

        MeasureModules();
        concrete = CreateMaterial("Concreto_Hospital", new Color(0.46f, 0.46f, 0.43f));   // techo y pilares: lo bastante claros para recibir la luz de las lámparas

        Transform systems = T("_Systems");
        Transform environment = T("Environment");
        Transform architecture = T("Architecture", environment);
        floors = T("Floors", architecture);
        walls = T("Walls", architecture);
        ceilings = T("Ceilings", architecture);
        pillars = T("Pillars", architecture);
        props = T("Props", environment);
        Transform lighting = T("Lighting");
        lamps = T("Practical", lighting);
        Transform gameplay = T("Gameplay");
        pickups = T("Pickups", T("Interactables", gameplay));
        documents = T("Documents", pickups.parent);
        doorsGroup = T("Doors", gameplay);
        clues = T("Clues", gameplay);
        triggers = T("Triggers", gameplay);
        Transform horror = T("Horror");
        encounters = T("EncounterTriggers", horror);
        Transform audio = T("Audio");

        foreach (var o in Openings) doorPoints.Add(EdgePoint(o.c, o.r, o.d));

        BuildArchitecture();
        BuildFloorCollider(architecture);
        BuildAtmosphere(lighting);
        BuildLamps();

        Flashlight flashlight;
        PlayerHealth health;
        BuildPlayer(out flashlight, out health);

        BuildKeyProps();
        BuildGameplay(flashlight, health);
        DressRooms();
        BuildHorror(horror);
        BuildAudio(audio);

        // Arquitectura estática: Unity la agrupa y dibuja más rápido
        foreach (Transform t in architecture.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = true;

        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), ScenePath);
        AddToBuildSettings(ScenePath);
        AssetDatabase.SaveAssets();
        Debug.Log("Nivel 1 creado en " + ScenePath + ". Celda=" + cell + "m, pared=" + wallH + "m, " + occupied.Count + " objetos. Dale Play.");
    }

    // ---------------------------------------------------------------- Arquitectura

    static void MeasureModules()
    {
        Vector3 floor = Measure(FH + "hospital_floor_slab.prefab");
        if (floor.x > 1.5f && floor.x < 10f) cell = Mathf.Round(Mathf.Max(floor.x, floor.z) * 100f) / 100f;

        Vector3 wall = Measure(FH + "hospital_wall_solid.prefab");
        if (wall.y > 2f && wall.y < 6f) wallH = wall.y;
        wallBias = wall.z > wall.x ? 90f : 0f;
        if (Mathf.Abs(Mathf.Max(wall.x, wall.z) - cell) > cell * 0.05f)
            Debug.LogWarning("Level01Builder: la pared mide " + Mathf.Max(wall.x, wall.z) + " m y la celda " + cell + " m; puede haber huecos.");

        Vector3 door = Measure(FH + "hospital_patient_room_door.prefab");
        doorBias = door.z > door.x ? 90f : 0f;
    }

    static void BuildArchitecture()
    {
        int rows = Map.Length;
        HashSet<Vector2Int> vertices = new HashSet<Vector2Int>();

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < Map[r].Length; c++)
            {
                char here = At(c, r);
                if (here == ' ') continue;
                Vector3 center = Cell(c, r);

                // Piso y techo (el techo es un bloque simple: el modelo del techo tiene ~90 mallas por celda)
                GameObject floor = Spawn(FH + "hospital_floor_slab.prefab", floors);
                if (floor != null) { SnapXZ(floor, center.x, center.z); SnapY(floor, 0f, true); floor.name = "Floor_" + c + "_" + r; }
                GameObject ceiling = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ceiling.name = "Ceiling_" + c + "_" + r;
                ceiling.transform.SetParent(ceilings);
                ceiling.transform.position = new Vector3(center.x, wallH + 0.1f, center.z);
                ceiling.transform.localScale = new Vector3(cell, 0.2f, cell);
                ceiling.GetComponent<Renderer>().sharedMaterial = concrete;
                Object.DestroyImmediate(ceiling.GetComponent<Collider>());

                foreach (var d in Dirs)
                {
                    char there = At(c + d.dc, r + d.dr);
                    if (there == here) continue;
                    if (there != ' ' && d.name != 'E' && d.name != 'S') continue;   // cada borde entre dos celdas se hace una vez

                    bool opening = IsOpening(c, r, d.name) || IsOpening(c + d.dc, r + d.dr, Opposite(d.name));
                    if (there == ' ') opening = false;

                    Vector3 edge = center + new Vector3(d.dc * cell / 2f, 0, -d.dr * cell / 2f);
                    bool vertical = d.name == 'E' || d.name == 'W';
                    bool window = !opening && (IsWindow(c, r, d.name) || IsWindow(c + d.dc, r + d.dr, Opposite(d.name)));
                    string name = (opening ? "Doorway_" : window ? "Window_" : "Wall_") + c + "_" + r + "_" + d.name;

                    // El modelo tiene una cara sucia (+Z, o +X si está girado) y otra limpia: la sucia debe mirar hacia el cuarto.
                    // Si solo un lado es habitación, ese lado; si no, hacia esta celda (pasillo/exterior).
                    bool faceThere = IsRoom(there) && !IsRoom(here);
                    Vector3 toThere = new Vector3(d.dc, 0, -d.dr);
                    Vector3 face = faceThere ? toThere : -toThere;
                    bool flip = vertical ? face.x < 0 : face.z < 0;
                    PlaceWall(opening ? "hospital_wall_doorway" : window ? "hospital_wall_window" : "hospital_wall_solid", name, edge, vertical, !opening, flip);

                    // Esquinas: cada extremo de pared lleva un pilar para tapar la unión
                    Vector3 a = vertical ? edge + new Vector3(0, 0, cell / 2f) : edge + new Vector3(cell / 2f, 0, 0);
                    Vector3 b = vertical ? edge - new Vector3(0, 0, cell / 2f) : edge - new Vector3(cell / 2f, 0, 0);
                    vertices.Add(Vertex(a));
                    vertices.Add(Vertex(b));
                }
            }

        foreach (Vector2Int v in vertices)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "Pillar_" + v.x + "_" + v.y;
            pillar.transform.SetParent(pillars);
            pillar.transform.position = new Vector3(v.x * cell / 2f, wallH / 2f, v.y * cell / 2f);
            pillar.transform.localScale = new Vector3(0.32f, wallH, 0.32f);
            pillar.GetComponent<Renderer>().sharedMaterial = concrete;
        }
    }

    static bool IsRoom(char c) => c != ' ' && c != 'x' && c != 'y';

    static void PlaceWall(string prefab, string name, Vector3 edge, bool vertical, bool addCollider, bool flip)
    {
        GameObject wall = Spawn(FH + prefab + ".prefab", walls);
        if (wall == null) return;
        wall.name = name;
        wall.transform.rotation = Quaternion.Euler(0, (vertical ? 90f : 0f) + wallBias + (flip ? 180f : 0f), 0);
        SnapXZ(wall, edge.x, edge.z);
        SnapY(wall, 0f, false);

        if (!addCollider) return;
        // Colisión simple y fiable aunque el modelo no traiga una: caja alineada a los ejes del mundo
        Bounds b = GetBounds(wall);
        GameObject col = new GameObject("Collider");
        col.transform.SetParent(wall.transform, true);
        col.transform.SetPositionAndRotation(new Vector3(b.center.x, wallH / 2f, b.center.z), Quaternion.identity);
        col.AddComponent<BoxCollider>().size = vertical ? new Vector3(0.3f, wallH, cell) : new Vector3(cell, wallH, 0.3f);
    }

    static void BuildFloorCollider(Transform architecture)
    {
        GameObject go = new GameObject("FloorCollider");
        go.transform.SetParent(floors);
        BoxCollider box = go.AddComponent<BoxCollider>();
        float w = Map[0].Length * cell, d = Map.Length * cell;
        go.transform.position = new Vector3(w / 2f, -0.5f, d / 2f);
        box.size = new Vector3(w, 1f, d);
    }

    // ---------------------------------------------------------------- Atmósfera e iluminación

    static readonly Color Warm = new Color(1f, 0.78f, 0.52f);
    static readonly Color Cold = new Color(0.7f, 0.86f, 1f);
    static readonly Color Sick = new Color(0.72f, 1f, 0.8f);
    static readonly Color Alarm = new Color(1f, 0.14f, 0.08f);
    static readonly Color ExitGreen = new Color(0.25f, 1f, 0.45f);

    static void BuildAtmosphere(Transform lighting)
    {
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.17f, 0.19f, 0.23f);   // base fría: sin linterna ni lámparas se reconoce la forma de las cosas
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(0.11f, 0.12f, 0.14f);
        RenderSettings.fogDensity = 0.01f;

        // Postproceso global (sutil): tonemapping neutral, viñeta, grano, color y bloom leve
        VolumeProfile profile = NewProfile("Assets/_Project/Settings/Level01_Volume.asset");
        AddEffect<Tonemapping>(profile).mode.Override(TonemappingMode.Neutral);
        Vignette vignette = AddEffect<Vignette>(profile);
        vignette.intensity.Override(0.28f);
        vignette.smoothness.Override(0.55f);
        FilmGrain grain = AddEffect<FilmGrain>(profile);
        grain.type.Override(FilmGrainLookup.Medium3);
        grain.intensity.Override(0.2f);
        ColorAdjustments color = AddEffect<ColorAdjustments>(profile);
        color.saturation.Override(-12f);
        color.contrast.Override(10f);
        color.postExposure.Override(0.5f);
        Bloom bloom = AddEffect<Bloom>(profile);
        bloom.intensity.Override(0.3f);
        bloom.threshold.Override(1f);
        EditorUtility.SetDirty(profile);

        GameObject volume = new GameObject("GlobalVolume");
        volume.transform.SetParent(lighting);
        Volume v = volume.AddComponent<Volume>();
        v.isGlobal = true;
        v.sharedProfile = profile;

        // Pasillo oscuro: volumen local que baja la exposición y cierra la viñeta al entrar (sin luces de techo)
        VolumeProfile dark = NewProfile("Assets/_Project/Settings/Level01_DarkHall_Volume.asset");
        AddEffect<ColorAdjustments>(dark).postExposure.Override(-0.9f);
        Vignette darkVignette = AddEffect<Vignette>(dark);
        darkVignette.intensity.Override(0.5f);
        darkVignette.smoothness.Override(0.6f);
        EditorUtility.SetDirty(dark);

        GameObject darkGo = new GameObject("DarkHall_Volume");
        darkGo.transform.SetParent(lighting);
        Vector3 dc = Cell(9, 1);
        darkGo.transform.position = new Vector3(dc.x, wallH / 2f, dc.z);
        BoxCollider darkBox = darkGo.AddComponent<BoxCollider>();
        darkBox.isTrigger = true;
        darkBox.size = new Vector3(cell * 3f, wallH, cell);
        Volume dv = darkGo.AddComponent<Volume>();
        dv.isGlobal = false;
        dv.blendDistance = 3f;
        dv.priority = 1f;
        dv.sharedProfile = dark;
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

    // Cada espacio lleva sus propias luminarias (con modelo). Los colores y estados cambian de un cuarto a otro:
    // fluorescentes fríos, lámparas cálidas, luces enfermizas que parpadean, lámparas muertas y luces de emergencia.
    static void BuildLamps()
    {
        string neon = AS + "NeonLamp.prefab", cage = FH + "hanging_cage_lamp.prefab", oil = FH + "oil_table_lamp.prefab", lantern = FH + "hurricane_lantern.prefab", small = AS + "SingleLight.prefab";

        // Pasillo principal: fluorescentes fríos, uno muerto junto al centro, uno que parpadea y una zona de emergencia roja ante la reja
        Fixture(neon, "Corridor_A", Cell(0, 1, 1.0f, 0), wallH, true, 0, true, Cold, 14f, 10f, false, false);
        Fixture(neon, "Corridor_B", Cell(2, 1, 0.8f, 0), wallH, true, 0, true, Cold, 14f, 10f, false, true);
        Fixture(neon, "Corridor_C", Cell(4, 1, 0f, 0), wallH, true, 0, false, Cold, 0f, 0f, false, false);
        Fixture(neon, "Corridor_D", Cell(5, 1, 1.5f, 0), wallH, true, 0, true, Sick, 12f, 10f, false, true);
        Fixture(small, "Emergency_Gate", Cell(7, 1, -0.4f, 1.85f), 2.5f, false, 0, true, Alarm, 5f, 7f, false, false);
        Fixture(small, "Exit_Gate", Cell(7, 1, 0.9f, 1.85f), 2.5f, false, 0, true, ExitGreen, 3.5f, 5f, false, false);

        // A: habitación inicial (lámpara cálida y débil que parpadea + lámpara de mesa junto a la cama)
        Fixture(cage, "Wake", Cell(0, 2, 2.0f, 0), wallH, true, 0, true, Warm, 11f, 8f, true, true);
        Fixture(oil, "Wake_Bedside", Cell(0, 2, -1.5f, 0f), 0.63f, false, 0, true, Warm, 4f, 4f, false, false);

        // G: sala de espera (verde frío; un foco bajo ilumina apenas la pared de la pista)
        Fixture(cage, "Waiting", Cell(0, 0, 2.0f, 0), wallH, true, 0, true, new Color(0.78f, 0.95f, 0.9f), 9f, 8f, false, true);
        Accent("Waiting_ClueSpot", Cell(1, 0, 0f, 1.2f), 2.3f, Warm, 5f, 4f, false);

        // N: enfermería (fluorescente frío, lámpara de escritorio cálida, emergencia roja en la esquina)
        Fixture(neon, "Nurse", Cell(2, 0, 2.0f, 0), wallH, true, 0, true, Cold, 14f, 9f, false, false);
        Fixture(oil, "Nurse_Desk", Cell(2, 0, 0.9f, 1.4f), 0.8f, false, 0, true, Warm, 4f, 4f, false, false);
        Fixture(small, "Emergency_Nurse", Cell(3, 0, 1.7f, 1.85f), 2.5f, false, 0, true, Alarm, 3.5f, 6f, false, false);

        // B / C: habitaciones de pacientes (una cálida; otra con la lámpara muerta e iluminada por un farol del suelo)
        Fixture(cage, "Patient1", Cell(4, 0, 0, 0), wallH, true, 0, true, Warm, 10f, 7f, false, false);
        Fixture(cage, "Patient2_Dead", Cell(5, 0, 0, 0), wallH, true, 0, false, Warm, 0f, 0f, false, false);
        Fixture(lantern, "Patient2_Lantern", Cell(5, 0, -0.8f, -0.9f), 0f, false, 0, true, Warm, 6f, 6f, false, true);

        // O: sala de observación (azulada, parpadea) y F: celda de aislamiento (casi sin luz: azul tenue y un punto rojo)
        Fixture(neon, "Observation", Cell(6, 0, 0, 0), wallH, true, 0, true, new Color(0.6f, 0.8f, 1f), 10f, 7f, false, true);
        Accent("Cell_Dim", Cell(7, 0, 0, 0), 2.4f, new Color(0.55f, 0.65f, 0.85f), 5f, 6f, false);
        Fixture(small, "Emergency_Cell", Cell(7, 0, 1.5f, -1.5f), 2.4f, false, 0, true, Alarm, 2.5f, 4f, false, false);

        // M: almacén médico, E: archivo, R: baño
        Fixture(neon, "Storage", Cell(2, 2, 2.0f, 0), wallH, true, 0, true, Cold, 12f, 9f, false, false);
        Fixture(cage, "Archive", Cell(4, 2, 0, 0), wallH, true, 0, true, Warm, 9f, 7f, false, false);
        Fixture(oil, "Archive_Desk", Cell(4, 2, 1.2f, 1.0f), 0.8f, false, 0, true, Warm, 3.5f, 4f, false, false);
        Fixture(neon, "Bathroom", Cell(5, 2, 0, 0), wallH, true, 0, true, Sick, 9f, 7f, false, true);

        // Pasillo oscuro: lámparas muertas, sin luz de techo. Solo una luz verde lejana y la linterna
        Fixture(cage, "Dark_A", Cell(8, 1, 0, 0), wallH, true, 0, false, Warm, 0f, 0f, false, false);
        Fixture(cage, "Dark_B", Cell(9, 1, 0, 0), wallH, true, 0, false, Warm, 0f, 0f, false, false);
        Fixture(small, "Exit_Far", Cell(10, 1, 1.5f, 1.85f), 2.5f, false, 0, true, ExitGreen, 4f, 6f, false, false);

        // H: escalera de salida (rojizo)
        Fixture(cage, "Stairs", Cell(11, 1, 0, 0), wallH, true, 0, true, new Color(1f, 0.55f, 0.45f), 8f, 6f, false, false);
    }

    // Luz sin modelo (un punto de luz puro): rellenos y focos de acento
    static void Accent(string name, Vector3 pos, float y, Color color, float intensity, float range, bool flicker)
    {
        GameObject go = new GameObject("Light_" + name);
        go.transform.SetParent(lamps);
        go.transform.position = new Vector3(pos.x, y, pos.z);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker) go.AddComponent<FlickerLight>().minFactor = 0.45f;
    }

    // Luminaria con modelo: usa la Light que trae el prefab (o crea una), la ajusta y la deja encendida o "muerta"
    static GameObject Fixture(string prefab, string name, Vector3 pos, float y, bool top, float yaw, bool lit, Color color, float intensity, float range, bool shadows, bool flicker)
    {
        GameObject go = Spawn(prefab, lamps);
        if (go == null) return null;
        go.name = "Lamp_" + name + (lit ? "" : "_Dead");
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, y, top);

        Light[] lights = go.GetComponentsInChildren<Light>(true);
        Light main;
        if (lights.Length > 0) main = lights[0];
        else
        {
            main = new GameObject("Light").AddComponent<Light>();
            main.transform.SetParent(go.transform, true);
        }
        for (int i = 1; i < lights.Length; i++) lights[i].enabled = false;   // un solo foco por lámpara

        Bounds b = GetBounds(go);
        main.transform.position = top ? new Vector3(b.center.x, b.min.y - 0.3f, b.center.z) : new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        main.type = LightType.Point;
        main.color = color;
        main.intensity = intensity;
        main.range = range;
        main.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        main.enabled = lit;
        if (lit && flicker) main.gameObject.AddComponent<FlickerLight>().minFactor = 0.45f;
        return go;
    }

    // ---------------------------------------------------------------- Jugador

    static void BuildPlayer(out Flashlight flashlight, out PlayerHealth health)
    {
        GameObject player = new GameObject("Player");
        player.tag = "Player";
        Vector3 start = Cell(1, 2, -0.4f, 0.3f);   // habitación inicial: despiertas mirando hacia la cama y la primera nota
        player.transform.position = new Vector3(start.x, 0.05f, start.z);
        player.transform.rotation = Quaternion.Euler(0, 270, 0);   // mira al oeste: cama y nota delante, la puerta a la derecha
        reserved.Add(new Vector2(start.x, start.z));
        CharacterController cc = player.AddComponent<CharacterController>();
        cc.height = 1.8f;
        cc.radius = 0.3f;
        cc.center = new Vector3(0, 0.9f, 0);
        cc.stepOffset = 0.3f;
        PlayerController controller = player.AddComponent<PlayerController>();
        controller.walkSpeed = 2.6f;
        health = player.AddComponent<PlayerHealth>();
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

        // Luz de la linterna: un Spot hijo de la cámara, así ilumina justo lo que miras
        GameObject spotGo = new GameObject("FlashlightSpot");
        spotGo.transform.SetParent(cam.transform, false);
        Light spot = spotGo.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 25f;
        spot.spotAngle = 62f;
        spot.innerSpotAngle = 30f;
        spot.intensity = 12f;
        spot.color = new Color(1f, 0.95f, 0.82f);
        spot.shadows = LightShadows.Soft;

        flashlight = cam.AddComponent<Flashlight>();
        flashlight.spot = spot;
        flashlight.hasFlashlight = true;   // empieza con la linterna en la mano y encendida
        flashlight.maxIntensity = 12f;
    }

    // ---------------------------------------------------------------- Objetos clave y decoración automática

    // Muebles que sostienen notas u objetos del puzzle, y las piezas que dan identidad a cada cuarto
    static void BuildKeyProps()
    {
        // A: habitación inicial. Cama junto a la pared oeste, mesita, silla de ruedas y colchón tirado
        Prop("hospital_bed", "A_Wake", 0, 2, -0.9f, -1.0f, 90f);
        Prop("wheelchair", "A_Wake", 1, 2, 1.0f, 0.9f, 200f);
        Prop(AS + "Mattress.prefab", "A_Wake", 1, 2, -0.5f, -1.0f, 25f);

        // G: sala de espera. La silla de ruedas mira la pared donde está la primera pista
        Prop("wheelchair", "G_Waiting", 1, 0, 0f, 0.2f, 0f);

        // N: enfermería. Mostrador con silla, archivadores y el televisor con la hoja de turno
        Prop(AS + "TableOffice.prefab", "N_NurseStation", 2, 0, 0.9f, 1.4f, 180f);
        Prop(AS + "ChairOffice.prefab", "N_NurseStation", 2, 0, 0.9f, 0.5f, 160f);
        tvNurse = Prop("crt_television", "N_NurseStation", 3, 0, 0.7f, 1.4f, 180f);
        Prop("filing_cabinet", "N_NurseStation", 3, 0, -1.2f, 1.4f, 180f);

        // B / C: habitaciones de pacientes
        Prop("hospital_bed", "B_Patient1", 4, 0, -0.9f, 0.6f, 90f);
        Prop("bedside_drawer", "B_Patient1", 4, 0, -1.5f, -0.6f, 90f);
        Prop("hospital_bed", "C_Patient2_Escape", 5, 0, 1.2f, 0.4f, 180f);   // junto a la pared este, sin tapar la puerta
        Prop("boarded_door", "C_Patient2_Escape", 5, 0, 0.6f, 1.85f, 180f);

        // O / F: observación (mesa y silla) y celda de aislamiento (colchón en el suelo)
        Prop(AS + "TableWhite.prefab", "O_Observation", 6, 0, 0f, 1.3f, 180f);
        Prop(AS + "ChairOffice.prefab", "O_Observation", 6, 0, 0f, 0.5f, 170f);
        Prop(AS + "Mattress.prefab", "F_IsolationCell", 7, 0, -0.6f, -0.5f, 80f);

        // Pasillo: carrito junto a la reja con el aviso de seguridad (primera pista hacia el almacén)
        gateTable = Prop("instrument_trolley", "X_Corridor", 7, 1, -0.9f, -1.5f, 0f);

        // E: archivo (escritorio que sostiene el registro) y R: baño (cofre de mantenimiento)
        drawerArchive = Prop(AS + "TableOffice.prefab", "E_Archive", 4, 2, -0.9f, -1.3f, 0f);
        toolChestBath = Prop("tool_chest", "R_Bathroom", 5, 2, 0.9f, -1.3f, 0f);
    }

    static void DressRooms()
    {
        // letra, objetos del tema, cuántos junto a las paredes, cuántos en el centro
        Dress('a', new[] { FH + "instrument_trolley.prefab", FH + "medicine_cabinet.prefab", FH + "bedside_drawer.prefab", AS + "Cupboard.prefab", AS + "Sink_V1.prefab", AS + "CurtainsMedic.prefab", AS + "Crutches.prefab", AS + "Box_V1.prefab", AS + "ChairWhite.prefab", AS + "TableSmall.prefab", AS + "Table_Bed.prefab", AS + "Pillow.prefab" }, 10, 2);
        Dress('g', new[] { AS + "Sofa.prefab", AS + "Bench.prefab", AS + "ChairWhite.prefab", AS + "ChairLeather.prefab", AS + "TableSmall.prefab", FH + "grandfather_clock.prefab", FH + "rocking_chair.prefab", AS + "Crutches.prefab", AS + "Box_V1.prefab", AS + "MirrorShelf.prefab" }, 10, 2);
        Dress('n', new[] { FH + "filing_cabinet.prefab", FH + "medicine_cabinet.prefab", AS + "MedRack.prefab", AS + "Cupboard.prefab", FH + "desk_fan.prefab", AS + "ChairOffice.prefab", FH + "instrument_trolley.prefab", AS + "PC_Monitor.prefab", AS + "Box_V1.prefab", AS + "Fridge.prefab", AS + "TableSmall.prefab" }, 11, 1);
        Dress('b', new[] { FH + "bedside_drawer.prefab", AS + "ChairWhite.prefab", AS + "Cupboard.prefab", AS + "TableSmall.prefab", AS + "CurtainsMedic.prefab", AS + "Crutches.prefab", AS + "Table_Bed.prefab", AS + "Box_V2.prefab", AS + "Pillow.prefab" }, 6, 1);
        Dress('c', new[] { FH + "bedside_drawer.prefab", AS + "ChairWhite.prefab", AS + "Cupboard.prefab", AS + "CurtainsMedic.prefab", AS + "Crutches.prefab", AS + "Box_V1.prefab", AS + "Mattress.prefab", AS + "Pillow.prefab", FH + "rocking_chair.prefab", FH + "medicine_cabinet.prefab" }, 7, 1);
        Dress('o', new[] { AS + "ChairOffice.prefab", AS + "PC_Monitor.prefab", AS + "Television.prefab", AS + "MedRack.prefab", AS + "Cupboard.prefab", FH + "desk_fan.prefab", AS + "Box_V1.prefab" }, 6, 1);
        Dress('f', new[] { AS + "Mattress.prefab", AS + "Pillow.prefab", AS + "Crutches.prefab", AS + "ChairWhite.prefab", AS + "Box_V2.prefab" }, 3, 0);
        Dress('m', new[] { AS + "BigShelf_Stock.prefab", AS + "BigShelf.prefab", AS + "Box_V1.prefab", AS + "Box_V2.prefab", AS + "MedRack.prefab", AS + "MedRack_V2.prefab", FH + "tool_chest.prefab", AS + "CaseMetallic.prefab", AS + "SmallMetalicCase.prefab", AS + "Cupboard.prefab" }, 12, 2);
        Dress('e', new[] { FH + "filing_cabinet.prefab", AS + "Cupboard.prefab", AS + "ChairOffice.prefab", AS + "BigShelf.prefab", AS + "Box_V1.prefab", AS + "Box_V2.prefab", AS + "MedRack.prefab", FH + "desk_fan.prefab", AS + "Heater.prefab" }, 8, 1);
        Dress('r', new[] { AS + "Toilet.prefab", AS + "Sink_V1.prefab", AS + "Bath.prefab", FH + "clawfoot_bath.prefab", FH + "chain_flush_toilet.prefab", AS + "Pipe.prefab", AS + "CurtainsMedic.prefab", AS + "MirrorShelf.prefab", AS + "Heater.prefab" }, 8, 0);
        Dress('x', new[] { AS + "Bench.prefab", AS + "WheelChair.prefab", FH + "wheelchair.prefab", AS + "SheetRack.prefab", AS + "MorgueWheelBed.prefab", AS + "Crutches.prefab", FH + "instrument_trolley.prefab", AS + "Box_V1.prefab", FH + "filing_cabinet.prefab", FH + "medicine_cabinet.prefab" }, 28, 0);
        Dress('y', new[] { AS + "WheelChair.prefab", AS + "Box_V1.prefab", AS + "Box_V2.prefab", AS + "Crutches.prefab", AS + "CanOld_Damaged.prefab", AS + "Bench.prefab" }, 6, 0);
        Dress('h', new[] { AS + "Box_V1.prefab", AS + "WheelChair.prefab", FH + "candelabrum.prefab", AS + "Heater.prefab", FH + "hospital_bed.prefab" }, 5, 1);
    }

    // Coloca objetos del tema en los bordes y el centro de la zona, sin tapar puertas ni objetos del puzzle
    static void Dress(char letter, string[] pool, int perimeter, int center)
    {
        List<string> valid = new List<string>();
        foreach (string path in pool) if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) valid.Add(path);
        if (valid.Count == 0) { Debug.LogWarning("Level01Builder: ningún objeto del tema '" + letter + "' existe."); return; }

        Transform group = RoomGroup(RoomNames[letter]);
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int r = 0; r < Map.Length; r++)
            for (int c = 0; c < Map[r].Length; c++)
                if (At(c, r) == letter) cells.Add(new Vector2Int(c, r));

        // Candidatos junto a las paredes (se saltan los lados con puerta)
        List<(Vector3 pos, float yaw)> candidates = new List<(Vector3, float)>();
        foreach (Vector2Int cr in cells)
            foreach (var d in Dirs)
            {
                if (At(cr.x + d.dc, cr.y + d.dr) == letter) continue;
                if (IsOpening(cr.x, cr.y, d.name) || IsOpening(cr.x + d.dc, cr.y + d.dr, Opposite(d.name))) continue;
                Vector3 dir = new Vector3(d.dc, 0, -d.dr), side = new Vector3(-dir.z, 0, dir.x);
                float yaw = Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg;
                for (int k = 0; k < 2; k++)
                    candidates.Add((Cell(cr.x, cr.y) + dir * (cell / 2f - 0.9f) + side * Rand(-1.4f, 1.4f), yaw));
            }
        Shuffle(candidates);

        int placed = 0;
        foreach (var cand in candidates)
        {
            if (placed >= perimeter) break;
            if (TryPlace(letter, valid, cand.pos, cand.yaw, group)) placed++;
        }

        placed = 0;
        for (int i = 0; i < center * 10 && placed < center; i++)
        {
            Vector2Int cr = cells[rng.Next(cells.Count)];
            Vector3 pos = Cell(cr.x, cr.y, Rand(-1.3f, 1.3f), Rand(-1.3f, 1.3f));
            if (TryPlace(letter, valid, pos, Rand(0f, 360f), group)) placed++;
        }
    }

    static bool TryPlace(char letter, List<string> valid, Vector3 pos, float yaw, Transform group)
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GameObject go = Spawn(valid[rng.Next(valid.Count)], group);
            if (go == null) continue;
            Fit(go);
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            SnapXZ(go, pos.x, pos.z);
            SnapY(go, 0f, false);

            Rect rect = Footprint(GetBounds(go));
            if (FitsRoom(rect, letter) && IsFree(rect))
            {
                occupied.Add(rect);
                return true;
            }
            Object.DestroyImmediate(go);
        }
        return false;
    }

    static bool FitsRoom(Rect r, char letter)
    {
        return At(CellX(r.xMin), CellY(r.yMin)) == letter && At(CellX(r.xMax), CellY(r.yMin)) == letter
            && At(CellX(r.xMin), CellY(r.yMax)) == letter && At(CellX(r.xMax), CellY(r.yMax)) == letter;
    }

    static bool IsFree(Rect r)
    {
        Rect padded = new Rect(r.x - 0.15f, r.y - 0.15f, r.width + 0.3f, r.height + 0.3f);
        foreach (Rect o in occupied) if (padded.Overlaps(o)) return false;
        foreach (Vector2 p in doorPoints) if (Dist(r, p) < 1.5f) return false;
        foreach (Vector2 p in reserved) if (Dist(r, p) < 1.0f) return false;
        return true;
    }

    static float Dist(Rect r, Vector2 p) => (new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax)) - p).magnitude;

    static Rect Footprint(Bounds b) => new Rect(b.min.x, b.min.z, b.size.x, b.size.z);

    // Un modelo demasiado grande se reduce para que no tape el cuarto
    static void Fit(GameObject go)
    {
        Bounds b = GetBounds(go);
        float footprint = Mathf.Max(b.size.x, b.size.z), limit = cell * 0.8f;
        float k = Mathf.Min(footprint > limit ? limit / footprint : 1f, b.size.y > wallH - 0.2f ? (wallH - 0.2f) / b.size.y : 1f);
        if (k < 1f) go.transform.localScale *= k;
    }

    static GameObject Prop(string name, string room, int c, int r, float dx, float dz, float yaw)
    {
        string path = name.StartsWith("Assets/") ? name : FH + name + ".prefab";
        GameObject go = Spawn(path, RoomGroup(room));
        if (go == null) return null;
        Fit(go);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        Vector3 p = Cell(c, r, dx, dz);
        SnapXZ(go, p.x, p.z);
        SnapY(go, 0f, false);
        occupied.Add(Footprint(GetBounds(go)));
        return go;
    }

    static Transform RoomGroup(string room)
    {
        if (!rooms.ContainsKey(room)) rooms[room] = T(room, props);
        return rooms[room];
    }

    // ---------------------------------------------------------------- Objetos del puzzle, pistas y puertas

    static void BuildGameplay(Flashlight flashlight, PlayerHealth health)
    {
        // --- Mesita de noche del cuarto de despertar (sostiene la primera nota)
        GameObject drawer = Prop("bedside_drawer", "A_Wake", 0, 2, -1.5f, 0.0f, 90f);

        // --- Baterías y botiquines (en el suelo, con un destello tenue para encontrarlos)
        Battery(Cell(2, 0, -1.2f, -0.8f), "Battery_Nurse", flashlight);
        Battery(Cell(5, 0, 0.9f, -1.1f), "Battery_Patient2", flashlight);
        Battery(Cell(2, 2, -1.0f, 1.0f), "Battery_Storage", flashlight);
        Battery(Cell(4, 2, 1.0f, 1.0f), "Battery_Archive", flashlight);
        Battery(Cell(5, 2, -1.0f, -1.0f), "Battery_Bathroom", flashlight);
        Battery(Cell(5, 1, 0.5f, -1.6f), "Battery_Corridor", flashlight);
        Bandage(Cell(4, 0, 1.0f, -1.0f), "Bandage_Patient1", health);
        Bandage(Cell(3, 2, 0.8f, 1.2f), "Bandage_Storage", health);

        // --- Notas (Document + Interactable), apoyadas sobre muebles
        // Cadena de pistas (cada una apunta a la siguiente sin dar la solución):
        // nota inicial -> aviso de la reja -> etiqueta del archivador -> nota de enfermería -> números en las paredes
        Note(TopOf(drawer), "Si despiertas", DocumentStyle.PatientNote, "", "",
            "Si despiertas, no grites.\n\nSi oyes respirar detrás de ti, QUÉDATE QUIETO. No camines. No corras.\n\nSe cansa de esperar.\n\nHay una reja al fondo del pasillo. Es la única salida. Pregunta a quien dejó los avisos.");
        Note(TopOf(gateTable), "Acceso clausurado", DocumentStyle.StaffReport, "SEG-0412", "18/11/1993",
            "El acceso a la escalera permanece cerrado con llave.\n\nCopia única de la llave: archivador n.º 3 del almacén médico.\n\nQueda prohibido el paso después de las 22:00 h. Los cambios de turno se harán por el lado norte.", "Jefatura de Seguridad");
        Note(TopOf(drawerArchive), "Nota de enfermería", DocumentStyle.PatientNote, "", "",
            "Combinación del archivador 3: tres números.\n\nLos escribí donde no pudieran borrarlos: uno en la sala de espera, otro tras el cristal de observación.\n\nEl último lo recuerdo bien: 2.\n\nNunca supe quién entró a las 3:07.");
        Note(TopOf(toolChestBath), "Cambio de fluorescentes", DocumentStyle.StaffReport, "MNT-0932", "09/11/1993",
            "El fondo del pasillo lleva meses sin luz. Cambié los fluorescentes tres veces y se apagan solos.\n\nLos pacientes del turno de noche no figuran en ningún registro de ingreso.\n\nSolicito que no se envíe a nadie solo a esa zona.", "J. Ortega, Mantenimiento");
        Note(TopOf(tvNurse), "Paciente 214", DocumentStyle.MedicalRecord, "0214-B", "02/12/1993",
            "Ingreso: sin registro.\nUbicación: aislamiento, ala norte.\nDiagnóstico: pendiente.\n\nObservaciones: el paciente afirma que alguien le enseña un ritual por las noches. Se oye respirar detrás de la puerta. Si te giras, ya está ahí.\n\nNo medicar sin autorización.", "Enf. M. Reyes");

        // --- Pistas pintadas en la pared (textura con luz y profundidad: no se ven a través de los muros)
        Vector3 waitingWall = Cell(1, 0, 0, cell / 2f - 0.16f);   // pared norte de la sala de espera
        WallText("4", new Vector3(waitingWall.x, 1.6f, waitingWall.z), 0f, 1.6f);
        WallText("PRIMERO", new Vector3(waitingWall.x, 1.05f, waitingWall.z), 0f, 0.35f);
        Vector3 cellWall = Cell(7, 0, cell / 2f - 0.16f, 0);      // pared este de la celda: se ve a través del cristal de observación
        WallText("7", new Vector3(cellWall.x, 1.5f, cellWall.z), 90f, 1.6f);
        Vector3 startWall = Cell(1, 2, cell / 2f - 0.16f, 0);     // pared este de la habitación inicial: queda a tu espalda al despertar
        WallText("NO TE GIRES", new Vector3(startWall.x, 1.9f, startWall.z), 90f, 0.5f);

        // --- Almacén: archivador con candado -> entrega la llave
        GameObject locker = Prop("filing_cabinet", "M_MedicalStorage", 3, 2, 0.2f, -1.3f, 0f);
        if (locker != null)
        {
            locker.name = "Locker_Padlock";
            locker.transform.SetParent(pickups.parent);   // Gameplay/Interactables
            Interactable it = AddInteractable(locker, "Abrir archivador (candado)", false);
            Padlock padlock = locker.AddComponent<Padlock>();
            padlock.fixedCode = CodeText;
            padlock.giveItem = "llave_escalera";
            padlock.unlockedMessage = "Candado abierto. Has cogido la llave de la REJA (etiquetada «REJA»).";
            Hook(it.onInteract, padlock.Open);

            // Candado visible en la puerta del archivador y etiqueta con la indicación (sin dar el código)
            GameObject lockModel = Spawn(SP + "lock.prefab", locker.transform);
            if (lockModel != null)
            {
                lockModel.name = "Padlock_Model";
                Bounds cb = GetBounds(locker);
                Bounds lb = GetBounds(lockModel);
                float longest = Mathf.Max(lb.size.x, Mathf.Max(lb.size.y, lb.size.z));
                if (longest > 0.0001f) lockModel.transform.localScale *= 0.12f / longest;
                SnapXZ(lockModel, cb.center.x, cb.max.z + 0.03f);
                SnapY(lockModel, cb.min.y + cb.size.y * 0.62f, false);
            }
            Note(TopOf(locker), "Archivador n.º 3", DocumentStyle.StaffReport, "ALM-03", "",
                "Cerrado con candado de combinación (3 cifras).\n\nLa combinación se anotó en el archivo, en una nota de enfermería. No la deje a la vista.", "Almacén médico");
        }

        BuildStairDoor();
    }

    // La reja del fondo del pasillo principal separa el pasillo oscuro: se abre con la llave del archivador
    static void BuildStairDoor()
    {
        Vector3 edge = Cell(7, 1) + new Vector3(cell / 2f, 0, 0);
        GameObject door = Spawn(FH + "barred_cell_door.prefab", doorsGroup);
        if (door != null)
        {
            door.name = "Door_Stairs";
            door.transform.rotation = Quaternion.Euler(0, 90f + doorBias, 0);
            door.transform.localScale = new Vector3(1.26f, 1.09f, 1f);   // se ajusta al hueco de la puerta (1,36 x 2,35 m)
            SnapXZ(door, edge.x, edge.z);
            SnapY(door, 0f, false);
            EnsureCollider(door);

            Interactable it = door.AddComponent<Interactable>();
            it.prompt = "Abrir reja";
            it.requiredItem = "llave_escalera";
            it.lockedMessage = "Está cerrada con llave";
            Door slide = door.AddComponent<Door>();
            slide.openOffset = new Vector3(0, wallH, 0);
            Hook(it.onInteract, slide.Open);
        }

        // Salida del nivel dentro de la escalera, al fondo del pasillo oscuro
        Vector3 exit = Cell(11, 1);
        GameObject trigger = new GameObject("LevelExit");
        trigger.transform.SetParent(triggers);
        trigger.transform.position = new Vector3(exit.x, 1.2f, exit.z);
        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(cell, 2.4f, cell);
        LevelExit levelExit = trigger.AddComponent<LevelExit>();
        levelExit.nextScene = "Level02_Service";
        levelExit.message = "Fin del nivel 1";
        reserved.Add(new Vector2(exit.x, exit.z));

        WallText("SALIDA  >  PISO 2", new Vector3(exit.x + cell / 2f - 0.16f, 1.9f, exit.z), 90f, 0.5f);
    }

    // ---------------------------------------------------------------- Horror (encuentros)

    static void BuildHorror(Transform horror)
    {
        // Fantasma TEMPORAL para el jumpscare: se reemplazará por el modelo real
        GameObject ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        ghost.name = "TEMP_Ghost";
        Object.DestroyImmediate(ghost.GetComponent<Collider>());
        ghost.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/FantasmaPrueba.mat");
        if (mat != null) ghost.GetComponent<Renderer>().sharedMaterial = mat;
        ghost.transform.SetParent(T("Ghosts", horror));
        ghost.SetActive(false);

        Jumpscare jumpscare = new GameObject("Jumpscare").AddComponent<Jumpscare>();
        jumpscare.transform.SetParent(T("JumpscarePoints", horror));
        jumpscare.scareObject = ghost;
        jumpscare.distance = 1.3f;
        jumpscare.duration = 1.0f;

        AudioClip breath = AssetDatabase.LoadAssetAtPath<AudioClip>(BreathClip);
        // Respiración: el jugador debe quedarse quieto
        Encounter("Breathing_Corridor_West", Cell(1, 1), new Vector3(cell * 3f, wallH, cell), breath, jumpscare, 1.0f, 7f);
        Encounter("Breathing_Archive", Cell(4, 2), new Vector3(cell, wallH, cell), breath, jumpscare, 1.5f, 8f);
        Encounter("Breathing_DarkHall", Cell(9, 1), new Vector3(cell * 3f, wallH, cell), breath, jumpscare, 2.0f, 8f);
    }

    static void Encounter(string name, Vector3 center, Vector3 size, AudioClip clip, Jumpscare jumpscare, float warning, float duration)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(encounters);
        go.transform.position = new Vector3(center.x, size.y / 2f, center.z);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;

        GameObject sound = new GameObject("BreathingSource");
        sound.transform.SetParent(go.transform);
        AudioSource src = sound.AddComponent<AudioSource>();
        src.clip = clip;
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 1f;
        src.maxDistance = 14f;
        src.volume = 1f;

        LookBackEncounter enc = go.AddComponent<LookBackEncounter>();
        enc.mode = LookBackEncounter.Mode.Freeze;
        enc.stepsAudio = src;
        enc.jumpscare = jumpscare;
        enc.warningTime = warning;
        enc.duration = duration;
        enc.soundDistance = 1.2f;
        enc.showHint = false;   // sin texto en pantalla: el jugador lo aprende de la nota
    }

    static void BuildAudio(Transform audio)
    {
        GameObject go = new GameObject("Ambience_Basement");
        go.transform.SetParent(audio);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientClip);
        src.loop = true;
        src.playOnAwake = true;
        src.spatialBlend = 0f;
        src.volume = 0.12f;   // muy bajito: genera tensión sin tapar los demás sonidos
    }

    // ---------------------------------------------------------------- Utilidades de objetos recogibles

    static void Battery(Vector3 pos, string name, Flashlight flashlight)
    {
        GameObject go = Item(SP + "battery big.prefab", name, pos, 0.14f, "Recoger batería", pickups);
        if (go != null) Hook(go.GetComponent<Interactable>().onInteract, flashlight.AddBattery);
    }

    static void Bandage(Vector3 pos, string name, PlayerHealth health)
    {
        GameObject go = Item(SP + "bandage.prefab", name, pos, 0.2f, "Usar vendaje", pickups);
        if (go != null) Hook(go.GetComponent<Interactable>().onInteract, health.UseBandage);
    }

    static void Note(Vector3 pos, string title, DocumentStyle style, string reference, string date, string text, string author = "")
    {
        GameObject go = Item(Paper_Prefab, "Note_" + System.Text.RegularExpressions.Regex.Replace(title, "[^A-Za-z0-9]+", "_"), pos, 0.3f, "Leer", documents);
        if (go == null) return;
        Interactable it = go.GetComponent<Interactable>();
        it.destroyAfterUse = false;
        Document doc = go.AddComponent<Document>();
        doc.title = title;
        doc.style = style;
        doc.reference = reference;
        doc.date = date;
        doc.author = author;
        doc.text = text;
        Hook(it.onInteract, doc.Read);
    }

    // Objeto recogible: se escala a un tamaño realista, se apoya en "pos", lleva un Interactable y un destello tenue
    static GameObject Item(string path, string name, Vector3 pos, float size, string prompt, Transform parent)
    {
        GameObject go = Spawn(path, parent);
        if (go == null) return null;
        go.name = name;
        Bounds b = GetBounds(go);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) go.transform.localScale *= size / longest;
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, pos.y, false);
        AddInteractable(go, prompt, true);
        reserved.Add(new Vector2(pos.x, pos.z));

        GameObject glint = new GameObject("Glint");
        glint.transform.SetParent(go.transform, true);
        glint.transform.position = GetBounds(go).center + Vector3.up * 0.15f;
        Light l = glint.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 1.8f;
        l.intensity = 0.8f;
        l.color = new Color(1f, 0.92f, 0.75f);
        l.shadows = LightShadows.None;
        return go;
    }

    static Interactable AddInteractable(GameObject go, string prompt, bool destroy)
    {
        EnsureCollider(go);
        Interactable it = go.AddComponent<Interactable>();
        it.prompt = prompt;
        it.destroyAfterUse = destroy;
        return it;
    }

    static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        Bounds b = GetBounds(go);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(b.center);
        Vector3 s = go.transform.lossyScale;
        box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
    }

    static void Hook(UnityEvent ev, UnityAction action) => UnityEventTools.AddPersistentListener(ev, action);

    static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    static void Shuffle<T2>(List<T2> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static int CellX(float x) => Mathf.FloorToInt(x / cell);
    static int CellY(float z) => Map.Length - 1 - Mathf.FloorToInt(z / cell);

    // Punto medio del borde de una celda hacia el lado d
    static Vector2 EdgePoint(int c, int r, char d)
    {
        Vector3 p = Cell(c, r);
        if (d == 'N') p.z += cell / 2f;
        else if (d == 'S') p.z -= cell / 2f;
        else if (d == 'E') p.x += cell / 2f;
        else p.x -= cell / 2f;
        return new Vector2(p.x, p.z);
    }

    // Texto "pintado": se genera una textura con letras en pixel-art y goteos de pintura, y se pone en un Quad.
    // Es un objeto normal con luz y profundidad: no se ve a través de las paredes.
    static void WallText(string text, Vector3 pos, float yaw, float size)
    {
        float height = size * 0.55f;
        Texture2D tex = PaintTexture(text);
        float width = height * tex.width / tex.height;

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Clue_" + text.Replace(' ', '_').Replace('>', '_');
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(clues);
        go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
        go.transform.localScale = new Vector3(width, height, 1f);

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(0.5f, 0.04f, 0.03f));
        mat.SetFloat("_Smoothness", 0.35f);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.4f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450;
        string path = "Assets/_Project/Art/Materials/Paint_" + System.Text.RegularExpressions.Regex.Replace(text.ToUpper(), "[^A-Z0-9]", "_") + ".mat";
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.CreateAsset(mat, path);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Texture2D PaintTexture(string text)
    {
        const int k = 10;                  // píxeles por "celda" de letra
        string label = text.ToUpper();
        int cols = label.Length * 6, rowsGlyph = 7, drip = 6;
        int w = cols * k, h = (rowsGlyph + drip) * k;
        Color32[] px = new Color32[w * h];
        System.Random rnd = new System.Random(label.GetHashCode());

        for (int i = 0; i < label.Length; i++)
        {
            string[] g = Glyph(label[i]);
            for (int gy = 0; gy < rowsGlyph; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (g[gy][gx] != '#') continue;
                    int x0 = (i * 6 + gx) * k, y0 = (drip + (rowsGlyph - 1 - gy)) * k;
                    for (int y = 0; y < k; y++)
                        for (int x = 0; x < k; x++)
                        {
                            // bordes irregulares para que parezca pintado a mano
                            if ((x == 0 || x == k - 1 || y == 0 || y == k - 1) && rnd.NextDouble() < 0.35) continue;
                            px[(y0 + y) * w + x0 + x] = new Color32(255, 255, 255, 255);
                        }
                    // goteo bajo las celdas inferiores de cada letra
                    bool bottom = gy == rowsGlyph - 1 || g[gy + 1][gx] != '#';
                    if (bottom && rnd.NextDouble() < 0.3)
                    {
                        int len = rnd.Next(k, drip * k - 4), cx = x0 + k / 2 + rnd.Next(-2, 3);
                        for (int d = 0; d < len; d++)
                        {
                            int y = y0 - 1 - d;
                            if (y < 0) break;
                            for (int dx = 0; dx < 3; dx++) px[y * w + cx + dx] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();

        EnsureFolder("Assets/_Project/Art", "Textures");
        string path = "Assets/_Project/Art/Textures/Paint_" + System.Text.RegularExpressions.Regex.Replace(label, "[^A-Z0-9]", "_") + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Letras 5x7 solo para los textos del nivel
    static string[] Glyph(char c)
    {
        switch (c)
        {
            case '2': return new[] { ".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####" };
            case '4': return new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." };
            case '7': return new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." };
            case 'P': return new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." };
            case 'R': return new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" };
            case 'I': return new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." };
            case 'M': return new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" };
            case 'E': return new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" };
            case 'O': return new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." };
            case 'N': return new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" };
            case 'T': return new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." };
            case 'G': return new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." };
            case 'S': return new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." };
            case 'A': return new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            case 'L': return new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" };
            case 'D': return new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." };
            case '>': return new[] { "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...." };
            default: return new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." };
        }
    }

    // ---------------------------------------------------------------- Geometría y ayudas

    static char At(int c, int r)
    {
        if (r < 0 || r >= Map.Length || c < 0 || c >= Map[r].Length) return ' ';
        return Map[r][c];
    }

    static bool IsOpening(int c, int r, char d)
    {
        foreach (var o in Openings) if (o.c == c && o.r == r && o.d == d) return true;
        return false;
    }

    static bool IsWindow(int c, int r, char d)
    {
        foreach (var w in Windows) if (w.c == c && w.r == r && w.d == d) return true;
        return false;
    }

    static char Opposite(char d) => d == 'N' ? 'S' : d == 'S' ? 'N' : d == 'E' ? 'W' : 'E';

    // Centro de una celda en el mundo (fila 0 = norte = z más alta)
    static Vector3 Cell(int c, int r, float dx = 0, float dz = 0)
    {
        int rows = Map.Length;
        return new Vector3(c * cell + cell / 2f + dx, 0, (rows - 1 - r) * cell + cell / 2f + dz);
    }

    static Vector2Int Vertex(Vector3 p) => new Vector2Int(Mathf.RoundToInt(p.x / (cell / 2f)), Mathf.RoundToInt(p.z / (cell / 2f)));

    static Vector3 TopOf(GameObject go)
    {
        if (go == null) return Vector3.zero;
        Bounds b = GetBounds(go);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    static Bounds GetBounds(GameObject go)
    {
        Renderer[] rs = go.GetComponentsInChildren<Renderer>();
        bool first = true;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in rs)
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    static void SnapXZ(GameObject go, float x, float z)
    {
        Bounds b = GetBounds(go);
        go.transform.position += new Vector3(x - b.center.x, 0, z - b.center.z);
    }

    // top = true: la cara superior queda en y; false: la base queda en y
    static void SnapY(GameObject go, float y, bool top)
    {
        Bounds b = GetBounds(go);
        go.transform.position += new Vector3(0, y - (top ? b.max.y : b.min.y), 0);
    }

    static Vector3 Measure(string path)
    {
        GameObject go = Spawn(path, null);
        if (go == null) return Vector3.zero;
        Vector3 size = GetBounds(go).size;
        Object.DestroyImmediate(go);
        return size;
    }

    static GameObject Spawn(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("Level01Builder: no encontré " + path);
            return null;
        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    static Transform T(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    static Material CreateMaterial(string name, Color color)
    {
        string path = "Assets/_Project/Art/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
        mat.SetFloat("_Smoothness", 0.1f);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
