using UnityEngine;
using UnityEngine.InputSystem;

// Mira hacia adelante desde la cámara; con E usa lo que tenga enfrente.
// Va en la cámara del jugador.
public class PlayerInteract : MonoBehaviour
{
    public float reach = 3f;

    string message;
    float messageTimer;

    void Update()
    {
        messageTimer -= Time.deltaTime;
        if (UIState.PanelOpen || UIState.JustClosed || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;

        RaycastHit hit;
        if (!Look(out hit)) return;

        Interactable item = hit.collider.GetComponentInParent<Interactable>();
        if (item != null)
        {
            if (!item.Interact()) Show(item.lockedMessage);
            return;
        }
    }

    bool Look(out RaycastHit hit)
    {
        Ray ray = new Ray(transform.position, transform.forward);
        return Physics.Raycast(ray, out hit, reach, ~0, QueryTriggerInteraction.Ignore);
    }

    void Show(string text)
    {
        message = text;
        messageTimer = 2f;
    }

    void OnGUI()
    {
        DrawCrosshair();

        GUIStyle style = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
        style.normal.textColor = Color.white;
        Rect r = new Rect(0, Screen.height * 0.6f, Screen.width, 40);

        if (messageTimer > 0) { GUI.Label(r, message, style); return; }

        // Mientras se mira un objeto usable, aparece "E: texto"
        if (!UIState.PanelOpen && Look(out RaycastHit hit))
        {
            Interactable item = hit.collider.GetComponentInParent<Interactable>();
            if (item != null) GUI.Label(r, "E: " + item.prompt, style);
        }
    }

    // Punto fijo en el centro: se agranda y se aclara al mirar algo con lo que se puede interactuar
    void DrawCrosshair()
    {
        if (UIState.PanelOpen) return;
        bool usable = Look(out RaycastHit hit) && hit.collider.GetComponentInParent<Interactable>() != null;
        float size = usable ? 8f : 4f;
        GUI.color = usable ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - size / 2f, Screen.height / 2f - size / 2f, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
