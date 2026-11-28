using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Crea las escenas MainMenu y Ending, enlaza el Nivel 3 con el final y deja Build Settings en orden:
// MainMenu -> Level01 -> Level02 -> Level03 -> Ending. Las escenas de los niveles no se regeneran, solo se cambia el destino de la salida del Nivel 3.
// El exterior de la escena final usa figuras temporales (TEMP_) hasta tener modelos reales de exterior.
public static class FlowBuilder
{
    const string ScenesDir = "Assets/_Project/Scenes/";
    const string MenuPath = ScenesDir + "MainMenu.unity";
    const string EndingPath = ScenesDir + "Ending.unity";
    const string Level1Path = ScenesDir + "Level01_Ward.unity";
    const string Level2Path = ScenesDir + "Level02_Service.unity";
    const string Level3Path = ScenesDir + "Level03_ResearchWing.unity";
    const string LogoPath = "Assets/Images/Logo Juegos.png";
    const string AmbientClip = "Assets/Sounds/jorisvermeer-horror-background-atmosphere-030-394196.mp3";
    const string MatDir = "Assets/_Project/Art/Materials/Ending";

    [MenuItem("Don't Look Back/Crear menú principal y final")]
    static void BuildAll()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("FlowBuilder: detén el juego antes de crear las escenas.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        BuildEnding();
        LinkLevel3();
        BuildMenu();            // al final: queda abierta para pulsar Play directamente
        SetBuildSettings();
        Debug.Log("Menú principal y final creados. Build Settings: MainMenu, Level01, Level02, Level03, Ending.");
    }

    // ---------------------------------------------------------------- Menú principal

    static void BuildMenu()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Color.black;
        camGo.AddComponent<AudioListener>();

        Transform systems = new GameObject("Systems").transform;
        MainMenu menu = new GameObject("MainMenu").AddComponent<MainMenu>();
        menu.transform.SetParent(systems, false);
        menu.logo = AssetDatabase.LoadAssetAtPath<Texture2D>(LogoPath);
        if (menu.logo == null) Debug.LogWarning("FlowBuilder: falta el logo en " + LogoPath);

        Transform audio = new GameObject("Audio").transform;
        GameObject amb = new GameObject("Ambience");
        amb.transform.SetParent(audio, false);
        AudioSource src = amb.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AmbientClip);
        src.loop = true;
        src.playOnAwake = true;
        src.spatialBlend = 0f;
        src.volume = 0.35f;
        menu.music = src;

        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    // ---------------------------------------------------------------- Escena final

    static void BuildEnding()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EnsureFolder(MatDir);

        RenderSettings.skybox = null;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.ambientMode = AmbientMode.Flat;

        GameObject camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        Camera cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 140f;
        cam.fieldOfView = 62f;
        camGo.AddComponent<AudioListener>();
        camGo.transform.position = new Vector3(0f, 1.7f, 0f);

        Transform lighting = new GameObject("Lighting").transform;
        Light sun = new GameObject("Sun").AddComponent<Light>();
        sun.transform.SetParent(lighting, false);
        sun.type = LightType.Directional;
        sun.shadows = LightShadows.Soft;
        sun.shadowStrength = 0.8f;
        // Luz cálida y débil que sale por la puerta del hospital, detrás de la cámara
        Light door = new GameObject("DoorGlow").AddComponent<Light>();
        door.transform.SetParent(lighting, false);
        door.transform.position = new Vector3(0f, 2.2f, -3f);
        door.type = LightType.Point;
        door.color = new Color(1f, 0.62f, 0.3f);
        door.intensity = 2.5f;
        door.range = 9f;

        Transform env = new GameObject("Environment").transform;
        Material ground = Mat("Ending_Ground", new Color(0.075f, 0.085f, 0.065f), 0.05f);
        Material gravel = Mat("Ending_Path", new Color(0.17f, 0.16f, 0.15f), 0.1f);
        Material wall = Mat("Ending_Hospital", new Color(0.2f, 0.2f, 0.19f), 0.1f);
        Material dark = Mat("Ending_Dark", new Color(0.03f, 0.03f, 0.03f), 0.1f);
        Material trunk = Mat("Ending_Trunk", new Color(0.09f, 0.07f, 0.055f), 0.05f);
        Material leaves = Mat("Ending_Leaves", new Color(0.05f, 0.075f, 0.05f), 0.05f);
        Material metal = Mat("Ending_Metal", new Color(0.1f, 0.1f, 0.11f), 0.3f);

        Prim("Ground", PrimitiveType.Plane, env, new Vector3(0f, 0f, 20f), new Vector3(20f, 1f, 20f), ground);
        Prim("Path", PrimitiveType.Cube, env, new Vector3(0f, 0.03f, 31f), new Vector3(3.2f, 0.06f, 70f), gravel);

        // Hospital detrás del jugador: la cámara nunca lo enfoca, pero está ahí (y proyecta su sombra)
        Transform hospital = new GameObject("Hospital").transform;
        hospital.SetParent(env, false);
        Prim("TEMP_Hospital_Body", PrimitiveType.Cube, hospital, new Vector3(0f, 7f, -14f), new Vector3(46f, 14f, 20f), wall);
        Prim("TEMP_Hospital_Door", PrimitiveType.Cube, hospital, new Vector3(0f, 1.5f, -3.9f), new Vector3(2.4f, 3f, 0.3f), dark);

        Transform trees = new GameObject("Trees").transform;
        trees.SetParent(env, false);
        Random.State keep = Random.state;
        Random.InitState(7);
        for (int i = 0; i < 40; i++)
        {
            float side = i % 2 == 0 ? -1f : 1f;
            float x = side * Random.Range(5.5f, 24f), z = Random.Range(6f, 64f);
            float h = Random.Range(5f, 9f), r = Random.Range(1.6f, 2.8f);
            GameObject tree = new GameObject("TEMP_Tree_" + (i + 1).ToString("00"));
            tree.transform.SetParent(trees, false);
            tree.transform.position = new Vector3(x, 0f, z);
            Prim("Trunk", PrimitiveType.Cylinder, tree.transform, new Vector3(x, h * 0.3f, z), new Vector3(0.45f, h * 0.3f, 0.45f), trunk);
            Prim("Crown", PrimitiveType.Sphere, tree.transform, new Vector3(x, h * 0.75f, z), new Vector3(r * 2f, r * 2.4f, r * 2f), leaves);
        }
        Random.state = keep;

        Transform fence = new GameObject("Fence").transform;
        fence.SetParent(env, false);
        foreach (float side in new[] { -4.6f, 4.6f })
        {
            for (float z = 8f; z <= 56f; z += 3f)
                Prim("Post", PrimitiveType.Cube, fence, new Vector3(side, 0.7f, z), new Vector3(0.14f, 1.4f, 0.14f), metal);
            Prim("RailTop", PrimitiveType.Cube, fence, new Vector3(side, 1.25f, 32f), new Vector3(0.05f, 0.07f, 48f), metal);
            Prim("RailLow", PrimitiveType.Cube, fence, new Vector3(side, 0.55f, 32f), new Vector3(0.05f, 0.07f, 48f), metal);
        }
        // Portón abierto al fondo
        Transform gate = new GameObject("Gate").transform;
        gate.SetParent(env, false);
        foreach (float side in new[] { -2.3f, 2.3f })
        {
            Prim("Pillar", PrimitiveType.Cube, gate, new Vector3(side, 1.8f, 58f), new Vector3(0.9f, 3.6f, 0.9f), wall);
            GameObject leaf = Prim("GateLeaf", PrimitiveType.Cube, gate, new Vector3(side * 1.45f, 1.2f, 58.9f), new Vector3(1.9f, 2.4f, 0.06f), metal);
            leaf.transform.rotation = Quaternion.Euler(0f, side > 0 ? 70f : -70f, 0f);
        }

        Transform audio = new GameObject("Audio").transform;
        AudioSource amb = MakeSource("Ambience", audio, AmbientClip, false, 0.3f);
        AudioSource breath = MakeSource("Breathing", audio, ScreamerKit.BreathClip, true, 1f);

        Transform systems = new GameObject("Systems").transform;
        EndingSequence ending = new GameObject("EndingSequence").AddComponent<EndingSequence>();
        ending.transform.SetParent(systems, false);
        ending.cam = camGo.transform;
        ending.sun = sun;
        ending.ambience = amb;
        ending.breathing = breath;

        // Estado inicial (el script lo reaplica al empezar)
        RenderSettings.fogColor = new Color(0.015f, 0.02f, 0.035f);
        RenderSettings.fogDensity = 0.032f;
        RenderSettings.ambientLight = new Color(0.02f, 0.025f, 0.04f);
        cam.backgroundColor = RenderSettings.fogColor;
        sun.color = new Color(0.45f, 0.55f, 0.9f);
        sun.intensity = 0.3f;
        sun.transform.rotation = Quaternion.Euler(30f, 180f, 0f);

        EndingArt.Apply();   // bosque y cielo con los packs del proyecto (sustituye a los árboles temporales)
        EditorSceneManager.SaveScene(scene, EndingPath);
    }

    static AudioSource MakeSource(string name, Transform parent, string clipPath, bool spatial, float volume)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        AudioSource src = go.AddComponent<AudioSource>();
        src.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(clipPath);
        src.loop = true;
        src.playOnAwake = false;
        src.spatialBlend = spatial ? 1f : 0f;
        if (spatial) { src.rolloffMode = AudioRolloffMode.Linear; src.minDistance = 1f; src.maxDistance = 14f; }
        src.volume = volume;
        return src;
    }

    static GameObject Prim(string name, PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());   // la escena es un recorrido guiado: no hacen falta colisiones
        go.transform.SetParent(parent, false);
        go.transform.position = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go;
    }

    static Material Mat(string name, Color color, float smoothness)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", smoothness);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }

    // ---------------------------------------------------------------- Enlaces

    // La salida del Nivel 3 (LevelExit_S9, desactivada hasta usar la llave) pasa a cargar el final
    static void LinkLevel3()
    {
        var scene = EditorSceneManager.OpenScene(Level3Path, OpenSceneMode.Single);
        bool found = false;
        foreach (LevelExit exit in Object.FindObjectsByType<LevelExit>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (exit.name != "LevelExit_S9") continue;
            exit.nextScene = GameFlow.EndingScene;
            EditorUtility.SetDirty(exit);
            found = true;
        }
        if (!found) Debug.LogWarning("FlowBuilder: no se encontró LevelExit_S9 en el Nivel 3; enlázalo a mano con nextScene = Ending.");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void SetBuildSettings()
    {
        string[] order = { MenuPath, Level1Path, Level2Path, Level3Path, EndingPath };
        var list = new List<EditorBuildSettingsScene>();
        foreach (string p in order) list.Add(new EditorBuildSettingsScene(p, true));
        foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)   // lo demás (el prototipo) se conserva al final
            if (System.Array.IndexOf(order, s.path) < 0) list.Add(s);
        EditorBuildSettings.scenes = list.ToArray();
    }
}
