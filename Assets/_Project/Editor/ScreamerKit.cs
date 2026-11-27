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
    public const string ScreamClip = "Assets/Sounds/Scary-Screamer-Sound-Effect-_Free_.mp3";
    public const string BreathClip ="Assets/Sounds/cryptologymedia-breathing-160445.mp3";

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
        jumpscare.targetHeight = 2.3f;

        // Grito del ataque (sin esto el susto salía mudo)
        GameObject scream = new GameObject("ScreamSound");
        scream.transform.SetParent(jumpscare.transform, false);
        AudioSource screamSrc = scream.AddComponent<AudioSource>();
        screamSrc.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(ScreamClip);
        screamSrc.playOnAwake = false;
        screamSrc.spatialBlend = 0f;
        jumpscare.scareSound = screamSrc;

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

    // El demonio del susto. Prioridad: el modelo animado de la carpeta Skins (Mixamo, con animación de pie); si no está,
    // el Demon_APose del pack; y si tampoco, la cápsula temporal. Siempre queda desactivado y sin colisiones.
    public const string SkinFbx = "Assets/Skins/Ch30_nonPBR@Standing Idle.fbx";
    public const string SkinController = "Assets/AnimacionCtrl/DemonSkin.controller";
    const string SkinMatDir = "Assets/_Project/Art/Materials/DemonSkin";

    public static GameObject CreateGhost(Transform parent)
    {
        EnsureSkinImport();
        GameObject skin = AssetDatabase.LoadAssetAtPath<GameObject>(SkinFbx);
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DemonPrefab);
        GameObject ghost;
        if (skin != null)
        {
            ghost = (GameObject)PrefabUtility.InstantiatePrefab(skin, parent);
            ghost.name = "Demon";
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
            Animator anim = ghost.GetComponentInChildren<Animator>();
            if (anim == null) anim = ghost.AddComponent<Animator>();   // el FBX viene sin avatar: Unity no le pone Animator
            anim.runtimeAnimatorController = EnsureSkinController();
            anim.applyRootMotion = false;
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(SkinFbx)) if (o is Avatar av) { anim.avatar = av; break; }
        }
        else if (prefab != null)
        {
            Debug.LogWarning("ScreamerKit: no se encontró " + SkinFbx + ". Se usa Demon_APose.");
            ghost = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            ghost.name = "Demon_APose";
            foreach (Collider c in ghost.GetComponentsInChildren<Collider>()) c.enabled = false;
        }
        else
        {
            Debug.LogError("ScreamerKit: no se encontró ningún demonio. Se usa la cápsula temporal TEMP_Ghost.");
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

    // Textura de cada material del FBX. Las texturas del FBX no se extraen solas, así que se usan las ya extraídas de
    // Characters/demon (son exactamente las mismas, comparadas byte a byte). No borrar esa carpeta.
    const string SkinTexDir = "Assets/Characters/demon/textures";
    static readonly string[,] SkinMaterials = { { "Ch30_Body", "1001" }, { "Ch30_Body1", "1002" } };

    // Deja el FBX de Skins listo: con avatar (para que tenga Animator), animación en bucle y materiales URP con su textura.
    // Solo reimporta si algo cambia, así que ejecutarlo varias veces no cuesta nada.
    static void EnsureSkinImport()
    {
        ModelImporter imp = AssetImporter.GetAtPath(SkinFbx) as ModelImporter;
        if (imp == null) return;
        bool dirty = false;

        // Humanoid: las animaciones de Mixamo bajadas aparte (correr, atacar) usan otros nombres de hueso y solo se retargetean así
        if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
        if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }

        // El clip de Mixamo no viene en bucle: se activa para que el demonio respire sin congelarse
        ModelImporterClipAnimation[] clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        bool loopChanged = false;
        foreach (ModelImporterClipAnimation c in clips) if (!c.loopTime) { c.loopTime = true; loopChanged = true; }
        if (loopChanged) { imp.clipAnimations = clips; dirty = true; }

        for (int i = 0; i < SkinMaterials.GetLength(0); i++)
        {
            var key = new AssetImporter.SourceAssetIdentifier(typeof(Material), SkinMaterials[i, 0]);
            if (imp.GetExternalObjectMap().ContainsKey(key)) continue;
            Material mat = EnsureSkinMaterial(SkinMaterials[i, 0], SkinMaterials[i, 1]);
            if (mat == null) continue;
            imp.AddRemap(key, mat);
            dirty = true;
        }
        if (dirty) imp.SaveAndReimport();
    }

    static Material EnsureSkinMaterial(string name, string id)
    {
        Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinTexDir + "/Ch30_" + id + "_Diffuse.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(SkinTexDir + "/Ch30_" + id + "_Normal.png");
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (diffuse == null || lit == null)
        {
            Debug.LogWarning("ScreamerKit: faltan las texturas Ch30_" + id + " en " + SkinTexDir + "; el demonio saldrá sin textura.");
            return null;
        }
        // El mapa de normales debe estar importado como "Normal map" o la luz se ve mal
        string normalPath = SkinTexDir + "/Ch30_" + id + "_Normal.png";
        TextureImporter ti = normal != null ? AssetImporter.GetAtPath(normalPath) as TextureImporter : null;
        if (ti != null && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }

        if (!AssetDatabase.IsValidFolder(SkinMatDir))
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Project/Art/Materials")) AssetDatabase.CreateFolder("Assets/_Project/Art", "Materials");
            AssetDatabase.CreateFolder("Assets/_Project/Art/Materials", "DemonSkin");
        }
        string path = SkinMatDir + "/DemonSkin_" + name + ".mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
        m.SetTexture("_BaseMap", diffuse);
        m.SetColor("_BaseColor", Color.white);
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.EnableKeyword("_NORMALMAP"); }
        m.SetFloat("_Smoothness", 0.25f);
        m.SetFloat("_Metallic", 0f);
        EditorUtility.SetDirty(m);
        AssetDatabase.SaveAssets();
        return m;
    }

    public const string RunFbx = "Assets/Animaciones/Zombie Running.fbx";
    public const string AttackFbx = "Assets/Animaciones/Zombie Attack.fbx";

    // Animación suelta de Mixamo (sin modelo): Humanoid para que se retargetee al demonio. loop = true para correr.
    static void EnsureAnimationImport(string path, bool loop)
    {
        ModelImporter imp = AssetImporter.GetAtPath(path) as ModelImporter;
        if (imp == null) return;
        bool dirty = false;
        if (imp.animationType != ModelImporterAnimationType.Human) { imp.animationType = ModelImporterAnimationType.Human; dirty = true; }
        if (imp.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel) { imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; dirty = true; }
        ModelImporterClipAnimation[] clips = imp.clipAnimations.Length > 0 ? imp.clipAnimations : imp.defaultClipAnimations;
        bool clipChanged = false;
        foreach (ModelImporterClipAnimation c in clips)
        {
            if (c.loopTime != loop) { c.loopTime = loop; clipChanged = true; }
            if (loop && !c.loopPose) { c.loopPose = true; clipChanged = true; }
        }
        if (clipChanged) { imp.clipAnimations = clips; dirty = true; }
        if (dirty) imp.SaveAndReimport();
    }

    static AnimationClip FirstClip(string path)
    {
        foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(path))
            if (o is AnimationClip c && !c.name.StartsWith("__preview__")) return c;
        return null;
    }

    // Animator del demonio: Idle (de pie, en bucle) -> Run (trigger "Run") -> Attack (trigger "Attack").
    // Jumpscare activa Run al empezar el ataque y Attack justo antes de llegar a la cara. Al desactivar el demonio el Animator vuelve a Idle.
    static RuntimeAnimatorController EnsureSkinController()
    {
        EnsureAnimationImport(RunFbx, true);
        EnsureAnimationImport(AttackFbx, false);
        AnimationClip idle = FirstClip(SkinFbx), run = FirstClip(RunFbx), attack = FirstClip(AttackFbx);

        // Se reconstruye el contenido del controlador (sin borrar el archivo, para que las escenas conserven su referencia)
        var controller = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>(SkinController);
        if (controller == null) controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(SkinController);
        for (int i = controller.parameters.Length - 1; i >= 0; i--) controller.RemoveParameter(i);
        var sm = controller.layers[0].stateMachine;
        foreach (var st in sm.states) sm.RemoveState(st.state);
        controller.AddParameter("Run", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);

        var idleState = sm.AddState("Idle");
        idleState.motion = idle;
        sm.defaultState = idleState;

        if (run != null)
        {
            var runState = sm.AddState("Run");
            runState.motion = run;
            var toRun = idleState.AddTransition(runState);
            toRun.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Run");
            toRun.hasExitTime = false; toRun.duration = 0.05f;

            if (attack != null)
            {
                var attackState = sm.AddState("Attack");
                attackState.motion = attack;
                var toAttack = runState.AddTransition(attackState);
                toAttack.AddCondition(UnityEditor.Animations.AnimatorConditionMode.If, 0f, "Attack");
                toAttack.hasExitTime = false; toAttack.duration = 0.08f;
            }
        }
        if (idle == null) Debug.LogWarning("ScreamerKit: no se encontró la animación dentro de " + SkinFbx);
        if (run == null) Debug.LogWarning("ScreamerKit: falta " + RunFbx + "; el demonio atacará sin animación de correr.");
        if (attack == null) Debug.LogWarning("ScreamerKit: falta " + AttackFbx + "; el demonio atacará sin animación de ataque.");
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
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
