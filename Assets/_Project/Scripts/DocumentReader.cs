using UnityEngine;
using UnityEngine.InputSystem;

// Panel para leer documentos (notas, cartas, expedientes). Se usa con DocumentReader.Show(titulo, texto).
// Se crea solo la primera vez que se necesita; no hay que ponerlo en la escena.
public class DocumentReader : MonoBehaviour
{
    static DocumentReader instance;

    string title, body;
    bool showing;
    int openedFrame;
    Vector2 scroll;

    public static void Show(string title, string body)
    {
        if (instance == null) instance = new GameObject("DocumentReader").AddComponent<DocumentReader>();
        instance.Open(title, body);
    }

    void Open(string newTitle, string newBody)
    {
        title = newTitle;
        body = newBody;
        scroll = Vector2.zero;
        showing = true;
        openedFrame = Time.frameCount;   // evita cerrarlo con la misma pulsación de E que lo abrió
        UIState.OpenPanel();
    }

    void Close()
    {
        showing = false;
        UIState.ClosePanel();
    }

    void Update()
    {
        if (!showing || Time.frameCount == openedFrame || Keyboard.current == null) return;
        if (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame) Close();
    }

    void OnGUI()
    {
        if (!showing) return;

        Rect box = new Rect(Screen.width / 2 - 250, Screen.height / 2 - 220, 500, 440);
        GUI.Box(box, "");

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        GUI.Label(new Rect(box.x, box.y + 10, box.width, 40), title, titleStyle);

        GUIStyle bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, alignment = TextAnchor.UpperLeft };
        Rect view = new Rect(box.x + 20, box.y + 60, box.width - 40, 290);
        float height = bodyStyle.CalcHeight(new GUIContent(body), view.width - 20);
        scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 20, height));
        GUI.Label(new Rect(0, 0, view.width - 20, height), body, bodyStyle);
        GUI.EndScrollView();

        if (GUI.Button(new Rect(box.x + box.width / 2 - 70, box.y + box.height - 60, 140, 40), "Cerrar (E)")) Close();
    }

    void OnDisable()
    {
        if (showing) UIState.ClosePanel();
    }
}
