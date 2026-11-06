using UnityEngine;

// Interfaz simple para la demo: vidas, mira, controles y notas recogidas.
public class DemoHUD : MonoBehaviour
{
    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 20 };
        style.normal.textColor = Color.white;

        if (GameManager.Instance != null)
            GUI.Label(new Rect(20, 15, 300, 30), $"Vidas: {GameManager.Instance.Lives} / {GameManager.Instance.maxLives}", style);

        GUI.Label(new Rect(Screen.width / 2 - 5, Screen.height / 2 - 15, 20, 30), "+", style);

        GUIStyle small = new GUIStyle(style) { fontSize = 15 };
        GUI.Label(new Rect(20, Screen.height - 35, 600, 30), "WASD moverse · Ratón mirar · E recoger o usar", small);

        if (CodeManager.Instance != null)
        {
            GUI.Label(new Rect(Screen.width - 300, 15, 280, 30), "Notas:", style);
            for (int i = 0; i < CodeManager.Instance.collectedNotes.Count; i++)
                GUI.Label(new Rect(Screen.width - 300, 45 + i * 25, 280, 25), CodeManager.Instance.collectedNotes[i], small);
        }
    }
}
