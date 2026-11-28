using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Arte de la escena final con los packs del proyecto: bosque (Handpainted Forest) y cielo (BOXOPHOBIC Skybox).
// Los packs usan shaders antiguos que se ven ROSAS en URP, así que se crean materiales URP propios (los del pack no se tocan),
// en tonos oscuros y apagados para que encajen con el resto del juego.
// Lo usa FlowBuilder al generar la escena y el menú "Mejorar escena final" para la ya guardada. Es idempotente.
public static class EndingArt
{
    const string ForestDir = "Assets/Handpainted_Forest_pack/Models/";
    const string SkyDir = "Assets/BOXOPHOBIC/Skybox Cubemap Extended/Demo/Textures/";
    const string MatDir = "Assets/_Project/Art/Materials/Ending";
    public const string SkyMaterialPath = MatDir + "/Ending_Sky.mat";
    const string EndingPath = "Assets/_Project/Scenes/Ending.unity";

    static readonly string[] TreePrefabs = { "Fir_v1_1", "Fir_v1_2", "Pine_v1_1", "Pine_v1_2" };
    static readonly string[] GrassPrefabs = { "Grass_v1_1", "Grass_v1_2", "Grass_v1_3", "Grass_v2_1" };

    [MenuItem("Don't Look Back/Mejorar escena final: bosque y cielo (escena guardada)")]
    static void ApplyToSaved()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) { Debug.LogError("EndingArt: detén el juego primero."); return; }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = EditorSceneManager.OpenScene(EndingPath, OpenSceneMode.Single);
        Apply();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Escena final: bosque y cielo aplicados.");
    }

    // Trabaja sobre la escena abierta (debe tener el objeto "Environment" y la cámara principal)
    public static void Apply()
    {
        GameObject envGo = GameObject.Find("Environment");
        if (envGo == null) { Debug.LogError("EndingArt: la escena no tiene el objeto 'Environment'."); return; }
        Transform env = envGo.transform;
        EnsureFolder(MatDir);

        SetGround(env);
        BuildForest(env);
        BuildGrass(env);
        SetSky();
    }

    // ---------------------------------------------------------------- Suelo

    static void SetGround(Transform env)
    {
        Transform ground = env.Find("Ground");
        if (ground == null) return;
        Material floor = FirstMaterial(ForestDir + "Floor_Grass_v1_1.prefab");
        Texture tex = floor != null ? floor.mainTexture : null;
        Material m = LoadOrCreate("Ending_Ground", Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", tex);
        m.SetTextureScale("_BaseMap", new Vector2(34f, 34f));   // el plano mide 200 m: una repetición cada ~6 m
        m.SetColor("_BaseColor", new Color(0.46f, 0.58f, 0.40f));   // césped oscuro y apagado (más verde: al amanecer la luz cálida lo vuelve marrón)
        m.SetFloat("_Smoothness", 0.05f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        ground.GetComponent<Renderer>().sharedMaterial = m;
    }

    // ---------------------------------------------------------------- Bosque

    static void BuildForest(Transform env)
    {
        Clear(env, "Trees");    // los árboles temporales (esferas) de la versión anterior
        Clear(env, "Forest");
        Transform root = new GameObject("Forest").transform;
        root.SetParent(env, false);

        var prefabs = new List<GameObject>();
        var mats = new List<Material>();
        foreach (string n in TreePrefabs)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(ForestDir + n + ".prefab");
            if (p == null) { Debug.LogWarning("EndingArt: falta el prefab " + n); continue; }
            prefabs.Add(p);
            mats.Add(CutoutMaterial("Forest_" + FirstMaterialName(p), FirstMaterial(ForestDir + n + ".prefab"), new Color(0.42f, 0.50f, 0.46f)));
        }
        if (prefabs.Count == 0) return;

        var placed = new List<Vector2>();
        Random.State keep = Random.state;
        Random.InitState(11);
        // Dos anillos: árboles pegados al camino (más ordenados) y un bosque denso al fondo que se pierde en la niebla
        PlaceTrees(root, prefabs, mats, placed, 46, 6.5f, 22f, 2f, 66f, 3.2f);
        PlaceTrees(root, prefabs, mats, placed, 70, 22f, 70f, -12f, 100f, 4.5f);
        Random.state = keep;
    }

    static void PlaceTrees(Transform root, List<GameObject> prefabs, List<Material> mats, List<Vector2> placed, int count, float xMin, float xMax, float zMin, float zMax, float spacing)
    {
        int tries = 0, made = 0;
        while (made < count && tries++ < count * 30)
        {
            float x = Random.Range(xMin, xMax) * (Random.value < 0.5f ? -1f : 1f), z = Random.Range(zMin, zMax);
            var p2 = new Vector2(x, z);
            bool tooClose = false;
            foreach (Vector2 q in placed) if ((q - p2).sqrMagnitude < spacing * spacing) { tooClose = true; break; }
            if (tooClose) continue;
            int i = Random.Range(0, prefabs.Count);
            GameObject t = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i], root);
            t.name = prefabs[i].name + "_" + (made + 1).ToString("00");
            t.transform.position = new Vector3(x, 0f, z);
            t.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float s = Random.Range(0.95f, 1.7f);
            t.transform.localScale = new Vector3(s, s * Random.Range(0.95f, 1.2f), s);
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mats[i];
            placed.Add(p2);
            made++;
        }
    }

    // ---------------------------------------------------------------- Hierba

    static void BuildGrass(Transform env)
    {
        Clear(env, "Grass");
        Transform root = new GameObject("Grass").transform;
        root.SetParent(env, false);
        var prefabs = new List<GameObject>(); var mats = new List<Material>();
        foreach (string n in GrassPrefabs)
        {
            GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(ForestDir + n + ".prefab");
            if (p == null) continue;
            prefabs.Add(p);
            mats.Add(CutoutMaterial("Forest_" + FirstMaterialName(p), FirstMaterial(ForestDir + n + ".prefab"), new Color(0.36f, 0.48f, 0.34f)));
        }
        if (prefabs.Count == 0) return;
        Random.State keep = Random.state;
        Random.InitState(23);
        // Matas a los lados del camino (el camino mide 3,2 m; la valla está a ±4,6 m)
        for (int i = 0; i < 260; i++)
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            float x = side * Random.Range(1.9f, 11f), z = Random.Range(-2f, 66f);
            if (Mathf.Abs(x) < 2.1f) continue;
            int k = Random.Range(0, prefabs.Count);
            GameObject g = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[k], root);
            g.name = prefabs[k].name + "_" + (i + 1).ToString("000");
            g.transform.position = new Vector3(x, 0f, z);
            g.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float s = Random.Range(0.9f, 1.6f);
            g.transform.localScale = new Vector3(s, s, s);
            foreach (Renderer r in g.GetComponentsInChildren<Renderer>()) { r.sharedMaterial = mats[k]; r.shadowCastingMode = ShadowCastingMode.Off; }
        }
        Random.state = keep;
    }

    // ---------------------------------------------------------------- Cielo

    static void SetSky()
    {
        var day = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyDir + "Polyverse Skies - Blue Sky.png");
        var night = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyDir + "Polyverse Skies - Night Sky.exr");
        Shader sh = Shader.Find("Skybox/Cubemap Blend");
        if (day == null || night == null || sh == null) { Debug.LogWarning("EndingArt: falta el pack de cielo (BOXOPHOBIC); se deja el fondo liso."); return; }
        Material m = LoadOrCreate("Ending_Sky", sh);
        m.SetTexture("_Tex", day);          // amanecer (final)
        m.SetTexture("_Tex_Blend", night);  // noche (principio)
        m.SetFloat("_CubemapTransition", 1f);   // 1 = noche; EndingSequence lo lleva hacia el día
        m.SetFloat("_EnableRotation", 0f);
        m.SetColor("_TintColor", new Color(0.30f, 0.33f, 0.45f, 1f));
        m.SetFloat("_Exposure", 0.9f);
        EditorUtility.SetDirty(m);
        RenderSettings.skybox = m;

        Camera cam = Camera.main;
        if (cam != null) cam.clearFlags = CameraClearFlags.Skybox;
        EndingSequence seq = Object.FindAnyObjectByType<EndingSequence>();
        if (seq != null) { seq.sky = m; EditorUtility.SetDirty(seq); }
    }

    // ---------------------------------------------------------------- Utilidades

    static void Clear(Transform env, string name)
    {
        Transform t = env.Find(name);
        if (t != null) Object.DestroyImmediate(t.gameObject);
    }

    static Material FirstMaterial(string prefabPath)
    {
        GameObject p = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (p == null) return null;
        Renderer r = p.GetComponentInChildren<Renderer>();
        return r != null ? r.sharedMaterial : null;
    }

    static string FirstMaterialName(GameObject prefab)
    {
        Renderer r = prefab.GetComponentInChildren<Renderer>();
        return r != null && r.sharedMaterial != null ? r.sharedMaterial.name : prefab.name;
    }

    // Material URP/Lit con recorte de transparencia (hojas y hierba), a dos caras, con la textura del material original del pack
    static Material CutoutMaterial(string name, Material source, Color tint)
    {
        Material m = LoadOrCreate(name, Shader.Find("Universal Render Pipeline/Lit"));
        m.SetTexture("_BaseMap", source != null ? source.mainTexture : null);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Surface", 0f);
        m.SetFloat("_AlphaClip", 1f);
        m.SetFloat("_Cutoff", 0.5f);
        m.SetFloat("_Cull", 0f);
        m.SetFloat("_Smoothness", 0f);
        m.SetFloat("_Metallic", 0f);
        m.EnableKeyword("_ALPHATEST_ON");
        m.SetOverrideTag("RenderType", "TransparentCutout");
        m.renderQueue = (int)RenderQueue.AlphaTest;
        EditorUtility.SetDirty(m);
        return m;
    }

    static Material LoadOrCreate(string name, Shader shader)
    {
        string path = MatDir + "/" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        else if (m.shader != shader) m.shader = shader;
        return m;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
    }
}
