using System;
using UnityEngine;
using UnityEngine.SceneManagement;

// Guarda las vidas, mueve al jugador al checkpoint y cambia de nivel.
// Va en un objeto vacío "GameManager" en el primer nivel; sobrevive entre escenas.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    public int maxLives = 3;
    public float respawnDelay = 1f;

    public int Lives { get; private set; }
    public event Action<int> OnLivesChanged;    // la interfaz se suscribe aquí

    Vector3 checkpointPosition;
    Quaternion checkpointRotation;
    bool respawning;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Lives = maxLives;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void Start() => SaveStartCheckpoint();

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        respawning = false;
        SaveStartCheckpoint();
        OnLivesChanged?.Invoke(Lives);
    }

    // El punto de inicio del nivel es el primer checkpoint
    void SaveStartCheckpoint()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null) SetCheckpoint(player.transform.position, player.transform.rotation);
    }

    public void SetCheckpoint(Vector3 position, Quaternion rotation)
    {
        checkpointPosition = position;
        checkpointRotation = rotation;
    }

    public void LoseLife()
    {
        if (respawning) return;   // evita perder dos vidas por el mismo golpe
        respawning = true;
        Lives--;
        OnLivesChanged?.Invoke(Lives);

        if (Lives <= 0) Invoke(nameof(GameOver), respawnDelay);
        else Invoke(nameof(RespawnPlayer), respawnDelay);
    }

    public void AddLife()
    {
        Lives = Mathf.Min(Lives + 1, maxLives);
        OnLivesChanged?.Invoke(Lives);
    }

    void RespawnPlayer()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            // El CharacterController se apaga un momento para poder mover al jugador
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            player.transform.SetPositionAndRotation(checkpointPosition, checkpointRotation);
            if (cc != null) cc.enabled = true;
        }
        respawning = false;
    }

    // Game Over: se reinicia solo el nivel actual (no todo el juego) con las vidas llenas
    void GameOver()
    {
        Lives = maxLives;
        Inventory.Clear();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void NextLevel()
    {
        int next = SceneManager.GetActiveScene().buildIndex + 1;
        if (next < SceneManager.sceneCountInBuildSettings) SceneManager.LoadScene(next);
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
