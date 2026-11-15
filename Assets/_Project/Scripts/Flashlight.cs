using UnityEngine;
using UnityEngine.InputSystem;

// Linterna del jugador: se recoge, se enciende con F y gasta batería.
// Va en la cámara del jugador. "spot" es la luz Spot hija de la cámara.
// Las baterías del nivel llaman a AddBattery desde el "On Interact" de su Interactable.
public class Flashlight : MonoBehaviour
{
    public Light spot;
    public bool startsWithFlashlight = true;   // el jugador empieza con la linterna en la mano
    public bool hasFlashlight;                 // false solo si startsWithFlashlight está apagado hasta que la recoge
    public float batteryDuration = 300f;       // segundos de luz con la batería llena
    [Range(0, 1)] public float startCharge = 0.6f;
    [Range(0, 1)] public float batteryAmount = 0.5f;   // cuánto recarga cada batería
    [Range(0, 1)] public float lowCharge = 0.2f;       // por debajo de esto la luz parpadea
    public float maxIntensity = 6f;

    public bool IsOn { get; private set; }
    public float Charge => charge;   // 0 a 1; lo lee el inventario

    float charge;
    string message;
    float messageTimer;

    void Start()
    {
        charge = startCharge;
        if (startsWithFlashlight) hasFlashlight = true;
        if (spot == null) spot = CreateSpot();   // por si la escena no trae la luz: se crea como hija de la cámara
        SetOn(hasFlashlight);
        if (hasFlashlight) Show("F: encender o apagar la linterna");
    }

    // Foco de la linterna: igual al de la demo, un Spot pegado a la cámara
    Light CreateSpot()
    {
        GameObject go = new GameObject("FlashlightSpot");
        go.transform.SetParent(transform, false);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.range = 25f;
        l.spotAngle = 62f;
        l.innerSpotAngle = 30f;
        l.intensity = maxIntensity;
        l.color = new Color(1f, 0.95f, 0.82f);
        return l;
    }

    public void Acquire()
    {
        hasFlashlight = true;
        SetOn(true);
        Show("Linterna recogida. F para encender o apagar");
    }

    public void AddBattery()
    {
        if (charge >= 0.99f)
        {
            Inventory.AddStack("bateria");   // la linterna está llena: se guarda para después
            Show("Batería guardada. R para usarla");
            return;
        }
        Recharge();
        Show("Batería recogida");
    }

    void Recharge() => charge = Mathf.Min(1f, charge + batteryAmount);

    // R: usa una batería guardada
    void UseStoredBattery()
    {
        if (charge >= 0.99f) { Show("La linterna ya está llena"); return; }
        if (!Inventory.TakeStack("bateria")) { Show("No tienes baterías"); return; }
        Recharge();
        Show("Batería usada");
    }

    void Update()
    {
        messageTimer -= Time.deltaTime;
        if (!hasFlashlight || UIState.PanelOpen) return;

        if (Keyboard.current != null && Keyboard.current.fKey.wasPressedThisFrame && charge > 0f) SetOn(!IsOn);
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame) UseStoredBattery();
        if (!IsOn) return;

        charge -= Time.deltaTime / batteryDuration;
        if (charge <= 0f)
        {
            charge = 0f;
            SetOn(false);
            Show(Inventory.Count("bateria") > 0 ? "Se acabó la batería. R para cambiarla" : "Se acabó la batería");
            return;
        }

        // Con poca batería la luz se debilita y parpadea
        float low = Mathf.Clamp01(charge / lowCharge);
        float intensity = maxIntensity * Mathf.Lerp(0.35f, 1f, low);
        if (charge < lowCharge && Random.value < 0.01f + 0.08f * (1f - low)) intensity *= Random.Range(0f, 0.4f);
        spot.intensity = intensity;
    }

    void SetOn(bool on)
    {
        IsOn = on;
        if (spot != null) spot.enabled = on;
    }

    void Show(string text)
    {
        message = text;
        messageTimer = 3f;
    }

    void OnGUI()
    {
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
        label.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
        if (messageTimer > 0) GUI.Label(new Rect(0, Screen.height * 0.7f, Screen.width, 40), message, label);

        if (!hasFlashlight) return;

        // Barra de batería discreta, abajo a la izquierda
        Rect bar = new Rect(24, Screen.height - 36, 110, 10);
        GUI.color = new Color(0, 0, 0, 0.6f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);
        GUI.color = charge > lowCharge ? new Color(0.85f, 0.85f, 0.7f, 0.9f) : new Color(0.9f, 0.2f, 0.15f, 0.9f);
        GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * charge, bar.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }
}
