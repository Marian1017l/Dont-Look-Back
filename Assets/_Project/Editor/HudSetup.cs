using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Añade el HUD (vida, linterna, recordatorio de inventario) a las escenas ya guardadas de los niveles, sin regenerarlas.
// Es idempotente: si la escena ya tiene HudOverlay, solo vuelve a asignar los iconos.
public static class HudSetup
{
    const string HeartPath = "Assets/Images/Vida (1).png";
    const string LightPath = "Assets/Images/Linterna.png";

    static readonly string[] Scenes =
    {
        "Assets/_Project/Scenes/Level01_Ward.unity",
        "Assets/_Project/Scenes/Level02_Service.unity",
        "Assets/_Project/Scenes/Level03_ResearchWing.unity",
    };

    [MenuItem("Don't Look Back/Añadir HUD a los niveles 1, 2 y 3 (escenas guardadas)")]
    static void ApplyToAll()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("HudSetup: detén el juego antes de añadir el HUD.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (string path in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            AddHud(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("HUD añadido a " + scene.name);
        }
    }

    // También lo usa Level01Builder al regenerar la escena
    public static void AddHud(Scene scene)
    {
        HudOverlay hud = Object.FindFirstObjectByType<HudOverlay>();
        if (hud == null)
        {
            GameObject go = new GameObject("HudOverlay");
            GameObject systems = GameObject.Find("_Systems");
            if (systems != null) go.transform.SetParent(systems.transform, false);
            hud = go.AddComponent<HudOverlay>();
        }
        hud.heartIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(HeartPath);
        hud.flashlightIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(LightPath);
        hud.health = Object.FindFirstObjectByType<PlayerHealth>();
        hud.flashlight = Object.FindFirstObjectByType<Flashlight>();
        if (hud.heartIcon == null || hud.flashlightIcon == null)
            Debug.LogWarning("HudSetup: faltan los iconos en Assets/Images (Vida (1).png, Linterna.png).");
        EditorUtility.SetDirty(hud);
    }
}
