using UnityEngine;
using UnityEngine.InputSystem;

// Salida del nivel. Collider con "Is Trigger": al entrar el jugador carga la escena siguiente.
// Si "nextScene" está vacía o aún no existe en Build Settings, muestra el mensaje y deja reiniciar con R
// (así el jugador nunca se queda atrapado en una pantalla negra).
public class LevelExit : MonoBehaviour
{
    public string nextScene;
    public string message = "Fin del nivel 1";

    bool done, hasNext;

    void OnTriggerEnter(Collider other)
    {
        if (done || !other.CompareTag("Player")) return;
        done = true;

        PlayerController controller = other.GetComponent<PlayerController>();
        if (controller != null) controller.enabled = false;

        hasNext = !string.IsNullOrEmpty(nextScene) && Application.CanStreamedLevelBeLoaded(nextScene);
        if (hasNext) UnityEngine.SceneManagement.SceneManager.LoadScene(nextScene);
    }

    void Update()
    {
        if (!done || hasNext || Keyboard.current == null || !Keyboard.current.rKey.wasPressedThisFrame) return;
        Inventory.ClearAll();   // volver a empezar: también se borran los documentos leídos
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
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

        if (hasNext) return;
        GUIStyle small = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        small.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        GUI.Label(new Rect(0, Screen.height * 0.6f, Screen.width, 40), "El siguiente nivel está en desarrollo.   R: volver a empezar", small);
    }
}
