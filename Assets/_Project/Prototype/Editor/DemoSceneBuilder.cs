using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Menú "Don't Look Back > Crear escena demo": arma un pasillo de hospital con cubos,
// el jugador, notas, candado, trampa cañón y pasos. Guarda la escena en Assets/_Project/Prototype/Demo.unity.
public static class DemoSceneBuilder
{
    const string ScenePath = "Assets/_Project/Prototype/Demo.unity";

    [MenuItem("Don't Look Back/Crear escena demo")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        EnsureFolder("Assets/_Project", "Prefabs");
        EnsureFolder("Assets/_Project/Art", "Materials");

        Material floorMat = CreateMaterial("Piso", new Color(0.35f, 0.38f, 0.36f));
        Material wallMat = CreateMaterial("Pared", new Color(0.55f, 0.6f, 0.55f));
        Material tableMat = CreateMaterial("Mesa", new Color(0.4f, 0.3f, 0.2f));
        Material doorMat = CreateMaterial("Puerta", new Color(0.45f, 0.08f, 0.08f));
        Material noteMat = CreateMaterial("Nota", new Color(1f, 0.95f, 0.4f));
        Material bottleMat = CreateMaterial("Frasco", new Color(0.3f, 0.8f, 0.6f));

        GameObject notePrefab = CreateNotePrefab(noteMat);
        GameObject projectilePrefab = CreateProjectilePrefab(bottleMat);

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Ambiente oscuro
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(0.06f, 0.07f, 0.07f);

        // Pasillo: 10 de ancho, 22 de largo, 3 de alto
        Transform level = new GameObject("Hospital").transform;
        Box("Piso", new Vector3(0, -0.1f, 0), new Vector3(10, 0.2f, 22), floorMat, level);
        Box("Techo", new Vector3(0, 3.1f, 0), new Vector3(10, 0.2f, 22), wallMat, level);
        Box("Pared izquierda", new Vector3(-5, 1.5f, 0), new Vector3(0.2f, 3, 22), wallMat, level);
        Box("Pared derecha", new Vector3(5, 1.5f, 0), new Vector3(0.2f, 3, 22), wallMat, level);
        Box("Pared trasera", new Vector3(0, 1.5f, -11), new Vector3(10, 3, 0.2f), wallMat, level);
        Box("Pared frontal izq", new Vector3(-3, 1.5f, 11), new Vector3(4, 3, 0.2f), wallMat, level);
        Box("Pared frontal der", new Vector3(3, 1.5f, 11), new Vector3(4, 3, 0.2f), wallMat, level);
        Box("Sobre la puerta", new Vector3(0, 2.75f, 11), new Vector3(2, 0.5f, 0.2f), wallMat, level);

        // Puerta con candado
        GameObject door = Box("Puerta con candado", new Vector3(0, 1.25f, 11), new Vector3(2, 2.5f, 0.15f), doorMat, level);
        door.AddComponent<Padlock>();

        // Mesas con puntos donde pueden aparecer las notas
        Vector3[] tables =
        {
            new Vector3(-4, 0.4f, -5), new Vector3(4, 0.4f, -5), new Vector3(-4, 0.4f, 0),
            new Vector3(-4, 0.4f, 6), new Vector3(4, 0.4f, 6), new Vector3(0, 0.4f, 3),
        };
        List<Transform> spawnPoints = new List<Transform>();
        for (int i = 0; i < tables.Length; i++)
        {
            Box("Mesa " + (i + 1), tables[i], new Vector3(1.2f, 0.8f, 0.8f), tableMat, level);
            Transform point = new GameObject("Punto de nota " + (i + 1)).transform;
            point.position = tables[i] + new Vector3(0, 0.42f, 0);
            point.rotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
            point.SetParent(level);
            spawnPoints.Add(point);
        }

        // Luces tenues en el techo
        CeilingLight(new Vector3(0, 2.8f, -6));
        CeilingLight(new Vector3(0, 2.8f, 2));
        CeilingLight(new Vector3(0, 2.8f, 9));

        // Jugador con cámara y linterna
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.tag = "Player";
        player.transform.position = new Vector3(0, 1.05f, -9);
        Object.DestroyImmediate(player.GetComponent<CapsuleCollider>());
        player.AddComponent<CharacterController>();
        PlayerController controller = player.AddComponent<PlayerController>();

        GameObject cam = new GameObject("Main Camera");
        cam.tag = "MainCamera";
        cam.transform.SetParent(player.transform);
        cam.transform.localPosition = new Vector3(0, 0.6f, 0);
        Camera camera = cam.AddComponent<Camera>();
        camera.nearClipPlane = 0.05f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.black;
        cam.AddComponent<AudioListener>();
        cam.AddComponent<PlayerInteract>();
        controller.playerCamera = cam.transform;

        GameObject flashlight = new GameObject("Linterna");
        flashlight.transform.SetParent(cam.transform, false);
        Light spot = flashlight.AddComponent<Light>();
        spot.type = LightType.Spot;
        spot.range = 15f;
        spot.spotAngle = 55f;
        spot.intensity = 4f;
        spot.color = new Color(1f, 0.95f, 0.85f);

        // Sistemas del juego
        new GameObject("GameManager").AddComponent<GameManager>();
        new GameObject("HUD").AddComponent<DemoHUD>();

        CodeManager codes = new GameObject("CodeManager").AddComponent<CodeManager>();
        codes.digits = 3;
        codes.notePrefab = notePrefab;
        codes.spawnPoints = spawnPoints.ToArray();

        // Trampa cañón: estante en la pared derecha que dispara frascos
        GameObject trap = new GameObject("Trampa cañón");
        trap.transform.position = new Vector3(4.4f, 0, 1);
        BoxCollider zone = trap.AddComponent<BoxCollider>();
        zone.isTrigger = true;
        zone.center = new Vector3(-3.4f, 1.5f, 0);
        zone.size = new Vector3(6.8f, 3, 6);
        Box("Estante", new Vector3(4.4f, 1, 1), new Vector3(0.6f, 2, 1.5f), tableMat, trap.transform);
        Transform firePoint = new GameObject("Punto de disparo").transform;
        firePoint.SetParent(trap.transform);
        firePoint.localPosition = new Vector3(-0.5f, 2.3f, 0);
        ProjectileLauncher launcher = trap.AddComponent<ProjectileLauncher>();
        launcher.projectilePrefab = projectilePrefab;
        launcher.firePoint = firePoint;

        // Guardar y agregar a Build Settings (lo necesita el GameManager para reiniciar)
        Scene scene = SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AddToBuildSettings(ScenePath);
        Debug.Log("Escena demo creada en " + ScenePath + ". Dale Play.");
    }

    static GameObject Box(string name, Vector3 position, Vector3 scale, Material mat, Transform parent)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        go.transform.position = position;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.transform.SetParent(parent, true);
        return go;
    }

    static void CeilingLight(Vector3 position)
    {
        GameObject go = new GameObject("Luz");
        go.transform.position = position;
        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.range = 6f;
        light.intensity = 0.8f;
        light.color = new Color(0.7f, 1f, 0.8f);
    }

    static GameObject CreateNotePrefab(Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "Nota";
        go.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        go.AddComponent<Note>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Project/Prefabs/Nota.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static GameObject CreateProjectilePrefab(Material mat)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Frasco";
        go.transform.localScale = Vector3.one * 0.25f;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        go.AddComponent<Projectile>();
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/_Project/Prefabs/Frasco.prefab");
        Object.DestroyImmediate(go);
        return prefab;
    }

    static Material CreateMaterial(string name, Color color)
    {
        string path = "Assets/_Project/Art/Materials/" + name + ".mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null) return existing;

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material mat = new Material(shader) { color = color };
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
