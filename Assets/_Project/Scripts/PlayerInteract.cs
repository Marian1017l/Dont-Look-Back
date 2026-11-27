using UnityEngine;
using UnityEngine.InputSystem;

// Mira hacia adelante desde la cámara; con E usa lo que tenga enfrente.
// Va en la cámara del jugador.
public class PlayerInteract : MonoBehaviour
{
    public float reach = 3f;

    string message;
    float messageTimer;

    Interactable looked;          // lo que se está mirando ahora
    string promptText = "";       // último texto mostrado (se conserva mientras se desvanece)
    float promptAlpha;            // 0 a 1, aparece y desaparece suave
    GUIStyle promptStyle;

    void Update()
    {
        messageTimer -= Time.deltaTime;

        bool blocked = UIState.PanelOpen || UIState.IntroPlaying;
        looked = null;
        RaycastHit hit = default;
        bool hitSomething = !blocked && Look(out hit);
        if (hitSomething) looked = hit.collider.GetComponentInParent<Interactable>();
        if (looked != null) promptText = looked.prompt;
        promptAlpha = Mathf.MoveTowards(promptAlpha, looked != null && messageTimer <= 0f ? 1f : 0f, Time.unscaledDeltaTime * 8f);

        if (blocked || UIState.JustClosed || Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
        if (looked != null && !looked.Interact()) Show(looked.lockedMessage);
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
        if (Event.current.type != EventType.Repaint) return;
        DrawCrosshair();

        float s = Screen.height / 1080f;
        float y = Screen.height * 0.62f;
        if (messageTimer > 0f) { HudOverlay.Toast(y, message, Mathf.Clamp01(messageTimer / 0.4f)); return; }
        if (promptAlpha <= 0f || UIState.PanelOpen) return;

        // "[E]  Recoger llave": tecla dibujada + texto con sombra
        if (promptStyle == null) promptStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        promptStyle.fontSize = Mathf.RoundToInt(24f * s);
        promptStyle.normal.textColor = new Color(0.95f, 0.93f, 0.86f, promptAlpha);
        float textW = promptStyle.CalcSize(new GUIContent(promptText)).x;
        float keyW = 40f * s, h = 40f * s, gap = 14f * s;
        float x = (Screen.width - (keyW + gap + textW)) / 2f;
        HudOverlay.KeyCap(new Rect(x, y - h / 2f, keyW, h), "E", promptAlpha, s);
        HudOverlay.ShadowLabel(new Rect(x + keyW + gap, y - h / 2f, textW + 10f, h), promptText, promptStyle);
    }

    // Punto fijo en el centro: se agranda y se aclara al mirar algo con lo que se puede interactuar
    void DrawCrosshair()
    {
        if (UIState.PanelOpen || UIState.IntroPlaying) return;
        bool usable = looked != null;
        float size = usable ? 8f : 4f;
        GUI.color = usable ? new Color(1f, 1f, 1f, 0.95f) : new Color(1f, 1f, 1f, 0.45f);
        GUI.DrawTexture(new Rect(Screen.width / 2f - size / 2f, Screen.height / 2f - size / 2f, size, size), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
