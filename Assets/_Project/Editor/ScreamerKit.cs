using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Screamers del demonio (Demon_APose) para los niveles. Lo usan:
//  - Level01Builder: solo CreateGhost (el nivel 1 tiene sus propios encuentros).
//  - Niveles 2 y 3: Build crea el objeto "Horror" (demonio + Jumpscare + encuentros de respiración). Los builders lo llaman
//    al generar la escena, y los menús "Añadir screamers a Nivel 2/3" lo añaden a la escena ya guardada sin regenerarla.
// Regla de cada encuentro (LookBackEncounter, modo Confront): suena una respiración detrás del jugador y el demonio aparece DELANTE.
// Mirar al demonio es seguro; girar más de 120° (mirar atrás) o moverse más de 0,4 m te atrapa.
// Para mover, quitar o añadir un trigger se edita solo la tabla de abajo.
public static class ScreamerKit
{
    public const string DemonPrefab = "Assets/Demon Horror Creature with Weapon/Prefabs/Demon_APose.prefab";
    public const string BreathClip = "Assets/Sounds/cryptologymedia-breathing-160445.mp3";

    // Un trigger: nombre, centro en el suelo (x, y del piso, z), tamaño de la caja, segundos hasta que suena el aviso y duración de la amenaza
    public struct Spot
    {
        public string name; public Vector3 center; public Vector3 size; public float warning; public float duration;
        public Spot(string n, float x, float z, float w, float d, float sx, float sz, float floorY = 0f)
        { name = n; center = new Vector3(x, floorY, z); size = new Vector3(sx, 3.2f, sz); warning = w; duration = d; }
    }

    // Nivel 2 (celdas de 2 m, ver plano en Level02Builder). Del tranquilo al intenso: mantenimiento -> servicio -> almacén -> aislamiento -> archivo
    static readonly Spot[] Level02Spots =
    {
        new Spot("Breathing_Maintenance",      10f, 16f, 1.5f, 7f, 6f, 6f),
        new Spot("Breathing_ServiceCorridor",  21f, 17f, 1.0f, 6f, 6f, 2f),
        new Spot("Breathing_OldStorage",       22f, 11f, 1.5f, 8f, 4f, 4f),
        new Spot("Breathing_ServiceLink",      31f, 10f, 1.0f, 7f, 2f, 6f),
        new Spot("Breathing_IsolationHall",    34f,  5f, 1.8f, 8f, 8f, 2f),
        new Spot("Breathing_RestrictedArchive",46f, 18f, 1.5f, 9f, 6f, 6f),
    };

    // Nivel 3 (celdas de 2 m, ver plano en Level03Builder). Del corredor de observación al vestíbulo de S-9
    static readonly Spot[] Level03Spots =
    {
        new Spot("Breathing_ObservationCorridor", 23f, 13f, 1.5f, 7f, 6f, 2f),
        new Spot("Breathing_ProjectionRoom",      29f, 19f, 1.5f, 7f, 4f, 4f),
        new Spot("Breathing_TestChamber",         22f, 18f, 1.2f, 8f, 6f, 6f),
        new Spot("Breathing_ColdLab",             14f, 17f, 1.5f, 8f, 5f, 4f),
        new Spot("Breathing_DirectorOffice",      11f, 24f, 1.0f, 8f, 6f, 5f),
        new Spot("Breathing_VestibuleS9",         18f, 25f, 2.0f, 9f, 3f, 5f),
    };

    // ---------------------------------------------------------------- Menús (escenas ya guardadas)

    [MenuItem("Don't Look Back/Añadir screamers a Nivel 2 (escena guardada)")]
    static void AddToLevel2() => ApplyToSavedScene("Assets/_Project/Scenes/Level02_Service.unity", null, Level02Spots);

    [MenuItem("Don't Look Back/Añadir screamers a Nivel 3 (escena guardada)")]
    static void AddToLevel3() => ApplyToSavedScene("Assets/_Project/Scenes/Level03_ResearchWing.unity", "Level03_ResearchWing", Level03Spots);

    // Los builders lo llaman justo antes de guardar la escena
    public static void BuildLevel2(Transform parent = null) => Build(parent, Level02Spots);
    public static void BuildLevel3(Transform parent) => Build(parent, Level03Spots);

    static void ApplyToSavedScene(string scenePath, string rootName, Spot[] spots)
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("ScreamerKit: detén el juego antes de añadir los screamers.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        Transform parent = null;
        if (rootName != null)
        {
            GameObject root = GameObject.Find(rootName);
            if (root != null) parent = root.transform;
        }
        Transform horror = Build(parent, spots);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Screamers añadidos a " + scene.name + ": " + horror.GetComponentsInChildren<LookBackEncounter>().Length + " triggers.");
    }

    // ---------------------------------------------------------------- Construcción

    // Crea (o recrea) el objeto "Horror" con el demonio, el Jumpscare y un encuentro por cada Spot
    public static Transform Build(Transform parent, Spot[] spots)
    {
        RemoveExisting(parent);

        Transform horror = new GameObject("Horror").transform;
        if (parent != null) horror.SetParent(parent, false);

        GameObject ghost = CreateGhost(new GameObject("Ghosts").transform);
        ghost.transform.parent.SetParent(horror, false);

        Jumpscare jumpscare = new GameObject("Jumpscare").AddComponent<Jumpscare>();
        jumpscare.transform.SetParent(horror, false);
        jumpscare.scareObject = ghost;
        jumpscare.distance = 1.3f;
        jumpscare.duration = 1.0f;
        jumpscare.standOnGround = true;
        jumpscare.lungeDistance = 0.8f;

        Transform encounters = new GameObject("Encounters").transform;
        encounters.SetParent(horror, false);
        AudioClip breath = AssetDatabase.LoadAssetAtPath<AudioClip>(BreathClip);
        foreach (Spot s in spots) CreateEncounter(encounters, s, breath, jumpscare);
        return horror;
    }

    static void RemoveExisting(Transform parent)
    {
        if (parent != null)
        {
            Transform old = parent.Find("Horror");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            return;
        }
        foreach (GameObject go in SceneManager.GetActiveScene().GetRootGameObjects())
            if (go.name == "Horror") { Object.DestroyImmediate(go); break; }
    }

    // El demonio del susto: instancia del prefab, desactivado y sin colisiones. Si falta el prefab usa la cápsula temporal de antes.
    public static GameObject CreateGhost(Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemonPrefab);
        GameObject ghost;
        if (prefab != null)
        {
            ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            ghost.name = "Demon_APose";
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
        else
        {
            Debug.LogError("ScreamerKit: no se encontró " + DemonPrefab + ". Se usa la cápsula temporal TEMP_Ghost.");
            ghost = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            ghost.name = "TEMP_Ghost";
            Object.DestroyImmediate(ghost.GetComponent<Collider>());
            ghost.transform.localScale = new Vector3(0.7f, 1.0f, 0.7f);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/FantasmaPrueba.mat");
            if (mat != null) ghost.GetComponent<Renderer>().sharedMaterial = mat;
            ghost.transform.SetParent(parent, false);
        }
        ghost.SetActive(false);
        return ghost;
    }

    static void CreateEncounter(Transform parent, Spot s, AudioClip clip, Jumpscare jumpscare)
    {
        GameObject go = new GameObject(s.name);
        go.transform.SetParent(parent, false);
        go.transform.position = s.center + new Vector3(0f, s.size.y / 2f, 0f);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = s.size;

        GameObject sound = new GameObject("BreathingSource");
        sound.transform.SetParent(go.transform, false);
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
        enc.mode = LookBackEncounter.Mode.Confront;   // el demonio aparece delante: mirar atrás (>120°) o moverse te atrapa
        enc.maxAngle = 120f;
        enc.demonDistance = 5f;
        enc.stepsAudio = src;
        enc.jumpscare = jumpscare;
        enc.warningTime = s.warning;
        enc.duration = s.duration;
        enc.soundDistance = 1.2f;
        enc.showHint = false;   // sin texto en pantalla: el jugador ya aprendió la regla en el nivel 1
    }
}
