using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

// Salud por golpes: cada ataque de la entidad quita "hitDamage". Con poca salud el jugador
// se ve herido (bordes rojos, camina más lento). Sin salud: fundido a negro y reinicio del nivel actual.
// Va en el objeto Player. Los botiquines llaman a UseBandage desde su Interactable.
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance;

    public PlayerController controller;
    public float maxHealth = 100f;
    public float hitDamage = 50f;            // 2 golpes matan
    public float bandageHeal = 50f;
    [Range(0.3f, 1f)] public float hurtWalkMultiplier = 0.75f;
    public float stunTime = 0.5f;            // el jugador queda paralizado un instante al ser golpeado
    public float deathDelay = 1.4f;          // tiempo del jumpscare antes del fundido
    public float fadeTime = 1f;

    public float Health { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsLethalHit => Health - hitDamage <= 0f;   // el encuentro lo usa para elegir el jumpscare largo

    float baseSpeed;
    float flash;
    float fade;
    Texture2D vignette;

    void Awake()
    {
        Instance = this;
        Health = maxHealth;
        vignette = MakeVignette();
    }

    void Start()
    {
        if (controller != null) baseSpeed = controller.walkSpeed;
    }

    public void TakeHit() => TakeDamage(hitDamage);

    public void TakeDamage(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Max(0f, Health - amount);
        flash = 1f;
        ApplySpeed();

        if (Health <= 0f) { IsDead = true; StartCoroutine(Die()); }
        else StartCoroutine(Stun());
    }

    public void Heal(float amount)
    {
        if (IsDead) return;
        Health = Mathf.Min(maxHealth, Health + amount);
        ApplySpeed();
    }

    public void UseBandage() => Heal(bandageHeal);

    void ApplySpeed()
    {
        if (controller == null) return;
        controller.walkSpeed = Health < maxHealth ? baseSpeed * hurtWalkMultiplier : baseSpeed;
    }

    IEnumerator Stun()
    {
        if (controller != null) controller.enabled = false;
        yield return new WaitForSeconds(stunTime);
        if (!IsDead && controller != null) controller.enabled = true;
    }

    IEnumerator Die()
    {
        if (controller != null) controller.enabled = false;
        yield return new WaitForSeconds(deathDelay);
        for (float t = 0; t < fadeTime; t += Time.deltaTime)
        {
            fade = t / fadeTime;
            yield return null;
        }
        fade = 1f;
        yield return new WaitForSeconds(0.4f);
        Inventory.Clear();   // el nivel empieza de cero: sin objetos recogidos
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    void Update() => flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 2.5f);

    void OnGUI()
    {
        Rect full = new Rect(0, 0, Screen.width, Screen.height);

        float hurt = 1f - Health / maxHealth;
        if (hurt > 0f && !IsDead)
        {
            float pulse = 0.08f * Mathf.Sin(Time.time * 4f);
            GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(hurt * 1.6f + pulse));
            GUI.DrawTexture(full, vignette);
        }
        if (flash > 0f)
        {
            GUI.color = new Color(0.8f, 0.05f, 0.05f, flash * 0.5f);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
        }
        if (fade > 0f)
        {
            GUI.color = new Color(0, 0, 0, fade);
            GUI.DrawTexture(full, Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    // Textura radial: transparente al centro, roja en los bordes
    static Texture2D MakeVignette()
    {
        const int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y) / (size - 1), new Vector2(0.5f, 0.5f)) * 2f;
                float a = Mathf.SmoothStep(0.3f, 1.1f, d);
                tex.SetPixel(x, y, new Color(0.45f, 0f, 0f, a));
            }
        tex.Apply();
        return tex;
    }
}
