using UnityEngine;

// Salida del nivel. Collider con "Is Trigger": al entrar el jugador carga la escena siguiente.
// Si "nextScene" está vacía o aún no existe en Build Settings, muestra el mensaje y se queda ahí.
public class LevelExit : MonoBehaviour
{
    public string nextScene;
    public string message = "Fin del nivel 1";

    bool done;

    void OnTriggerEnter(Collider other)
    {
        if (done || !other.CompareTag("Player")) return;
        done = true;

        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        if (!string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene))
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    void OnGUI()
    {
        if (!done) return;
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 36, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        style.normal.textColor = Color.white;
        GUI.color = new Color(0, 0, 0, 0.85f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0, 0, Screen.width, Screen.height), message, style);
    }
}
