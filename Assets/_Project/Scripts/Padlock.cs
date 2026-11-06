using UnityEngine;
using UnityEngine.Events;

// Candado: muestra un panel para escribir el código.
// Va en un objeto con Interactable cuyo "On Interact" llama a Padlock.Open.
// Al acertar lanza "On Unlocked" (por ejemplo Door.Open) y, si se indica, entrega un objeto.
public class Padlock : MonoBehaviour
{
    public string fixedCode;                      // código escrito a mano; vacío = usar el del CodeManager
    public string giveItem;                       // objeto que entrega al abrirse (vacío = ninguno)
    public UnityEvent onUnlocked = new UnityEvent();
    public UnityEvent onWrongCode = new UnityEvent();

    public bool IsOpen { get; private set; }

    bool showing;
    string input = "";
    string message = "";
    float messageTimer;

    string Code => string.IsNullOrEmpty(fixedCode) ? CodeManager.Instance.Code : fixedCode;

    public void Open()
    {
        if (IsOpen) return;
        showing = true;
        input = "";
        UIState.OpenPanel();
    }

    void Close()
    {
        showing = false;
        UIState.ClosePanel();
    }

    void TryCode()
    {
        if (input == Code)
        {
            IsOpen = true;
            if (!string.IsNullOrEmpty(giveItem)) Inventory.Add(giveItem);
            message = "¡Clic! El candado se abrió.";
            messageTimer = 4f;
            Close();
            onUnlocked.Invoke();
        }
        else
        {
            message = "Código incorrecto... algo te escuchó.";
            messageTimer = 2f;
            onWrongCode.Invoke();
        }
    }

    void Update() => messageTimer -= Time.deltaTime;

    void OnDisable() { if (showing) UIState.ClosePanel(); }

    void OnGUI()
    {
        GUIStyle big = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        if (showing)
        {
            Rect box = new Rect(Screen.width / 2 - 160, Screen.height / 2 - 90, 320, 180);
            GUI.Box(box, "");
            GUI.Label(new Rect(box.x, box.y + 10, box.width, 30), "Candado: escribe el código", big);
            GUIStyle field = new GUIStyle(GUI.skin.textField) { fontSize = 26, alignment = TextAnchor.MiddleCenter };
            input = GUI.TextField(new Rect(box.x + 80, box.y + 50, 160, 40), input, Code.Length, field);
            if (GUI.Button(new Rect(box.x + 40, box.y + 110, 110, 40), "Probar")) TryCode();
            if (GUI.Button(new Rect(box.x + 170, box.y + 110, 110, 40), "Cerrar")) Close();
        }

        if (messageTimer > 0)
            GUI.Label(new Rect(0, Screen.height - 140, Screen.width, 40), message, big);
    }
}
