using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Pone en el Nivel 1 el demonio de la carpeta Skins (con animación). Se cambia SOLO el modelo del Jumpscare:
// los triggers, el sonido y el resto de la escena no se tocan. Es idempotente.
// El modelo anterior solo se quita de la escena; los archivos de los otros demonios se conservan en Assets.
public static class Level01Demon
{
    const string ScenePath = "Assets/_Project/Scenes/Level01_Ward.unity";

    [MenuItem("Don't Look Back/Nivel 1: usar el demonio de Skins (escena guardada)")]
    static void Apply()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("Level01Demon: detén el juego antes de aplicar el cambio.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Jumpscare jumpscare = Object.FindFirstObjectByType<Jumpscare>(FindObjectsInactive.Include);
        if (jumpscare == null) { Debug.LogError("Level01Demon: no hay ningún Jumpscare en el Nivel 1."); return; }

        GameObject old = jumpscare.scareObject;
        Animator oldAnim = old != null ? old.GetComponentInChildren<Animator>(true) : null;
        bool hasAttackAnim = false;
        if (oldAnim != null && oldAnim.runtimeAnimatorController != null)
            foreach (AnimatorControllerParameter p in oldAnim.parameters) if (p.name == "Attack") hasAttackAnim = true;
        if (old != null && old.name == "Demon" && hasAttackAnim) { Debug.Log("El Nivel 1 ya usa el demonio de Skins con animaciones de correr y atacar."); return; }

        Transform parent = old != null ? old.transform.parent : null;
        if (parent == null)
        {
            GameObject ghosts = GameObject.Find("Ghosts");
            parent = ghosts != null ? ghosts.transform : null;
        }

        GameObject demon = ScreamerKit.CreateGhost(parent != null ? parent : new GameObject("Ghosts").transform);
        if (old != null) Object.DestroyImmediate(old);

        // De pie en el suelo, 2,3 m de alto; termina a 1,3 m de la cámara
        jumpscare.scareObject = demon;
        jumpscare.headPoint = null;
        jumpscare.standOnGround = true;
        jumpscare.targetHeight = 2.3f;
        jumpscare.distance = 1.3f;

        EditorUtility.SetDirty(jumpscare);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Nivel 1: el demonio ahora es el modelo animado de Skins.");
    }
}
