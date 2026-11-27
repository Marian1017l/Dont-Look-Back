using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Aplica a la escena YA GUARDADA del Nivel 1 (sin regenerarla, para no perder los ajustes a mano):
//  - IntroSequence (apertura de ojos + tarjeta de controles) dentro de _Systems.
//  - Luz ambiente un poco más alta.
// Es idempotente: se puede ejecutar varias veces.
public static class Level01Intro
{
    const string ScenePath = "Assets/_Project/Scenes/Level01_Ward.unity";

    // Ambiente anterior (0.085, 0.095, 0.115) subido ~55 %: se distingue mejor la arquitectura sin perder la oscuridad
    public static readonly Color Ambient = new Color(0.132f, 0.147f, 0.178f);

    [MenuItem("Don't Look Back/Añadir intro y más luz al Nivel 1 (escena guardada)")]
    static void Apply()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Level01Intro: detén el juego antes de aplicar el cambio.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        AddIntro(scene);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Ambient;

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Nivel 1: intro añadida y luz ambiente subida a " + Ambient);
    }

    // También lo usa Level01Builder si se regenera la escena
    public static void AddIntro(Scene scene)
    {
        if (Object.FindFirstObjectByType<IntroSequence>() != null) return;

        GameObject go = new GameObject("IntroSequence");
        GameObject systems = GameObject.Find("_Systems");
        if (systems != null) go.transform.SetParent(systems.transform, false);
        IntroSequence intro = go.AddComponent<IntroSequence>();
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) intro.player = player.transform;
    }
}
