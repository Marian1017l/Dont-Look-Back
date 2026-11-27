using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Fundido a negro entre escenas. Se crea solo la primera vez que se usa y sobrevive a los cambios de escena.
// Uso: ScreenFader.Go("NombreEscena"). Lo llama GameFlow.
public class ScreenFader : MonoBehaviour
{
    static ScreenFader instance;
    float alpha;
    bool busy;

    public static void Go(string scene, float fadeOut = 0.9f, float fadeIn = 1.1f)
    {
        if (instance == null)
        {
            GameObject go = new GameObject("ScreenFader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<ScreenFader>();
        }
        if (instance.busy) return;   // nunca dos cargas a la vez
        instance.StartCoroutine(instance.Run(scene, fadeOut, fadeIn));
    }

    IEnumerator Run(string scene, float fadeOut, float fadeIn)
    {
        busy = true;
        for (float t = 0f; t < fadeOut; t += Time.unscaledDeltaTime)
        {
            alpha = Mathf.SmoothStep(0f, 1f, t / fadeOut);
            yield return null;
        }
        alpha = 1f;

        Time.timeScale = 1f;
        AsyncOperation op = SceneManager.LoadSceneAsync(scene);
        while (op != null && !op.isDone) yield return null;
        yield return null;

        for (float t = 0f; t < fadeIn; t += Time.unscaledDeltaTime)
        {
            alpha = 1f - Mathf.SmoothStep(0f, 1f, t / fadeIn);
            yield return null;
        }
        alpha = 0f;
        busy = false;
    }

    void OnGUI()
    {
        if (alpha <= 0f) return;
        GUI.depth = -1000;   // por encima de todo
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
