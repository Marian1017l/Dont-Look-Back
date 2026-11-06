using UnityEngine;
using UnityEngine.UI;

// Muestra las vidas como corazones. Va en el Canvas.
// Arrastra las imágenes de corazón (en orden) al arreglo "hearts".
public class HeartsUI : MonoBehaviour
{
    public Image[] hearts;
    public Image damageFlash;        // imagen roja a pantalla completa, opcional
    public float flashTime = 0.5f;

    int lastLives = -1;
    float flashTimer;

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLivesChanged += UpdateHearts;
            UpdateHearts(GameManager.Instance.Lives);
        }
        if (damageFlash != null) damageFlash.enabled = false;
    }

    void UpdateHearts(int lives)
    {
        for (int i = 0; i < hearts.Length; i++)
            if (hearts[i] != null) hearts[i].enabled = i < lives;

        if (lastLives != -1 && lives < lastLives) flashTimer = flashTime;
        lastLives = lives;
    }

    void Update()
    {
        if (damageFlash == null) return;
        flashTimer -= Time.deltaTime;
        damageFlash.enabled = flashTimer > 0;
    }

    void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.OnLivesChanged -= UpdateHearts;
    }
}
