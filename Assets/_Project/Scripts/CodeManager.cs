using System.Collections.Generic;
using UnityEngine;

// Genera un código aleatorio y crea las notas (Instantiate) en puntos al azar.
// Va en un objeto vacío de la escena.
public class CodeManager : MonoBehaviour
{
    public static CodeManager Instance;

    public int digits = 3;
    public GameObject notePrefab;
    public Transform[] spawnPoints;   // conviene tener más puntos que notas

    public string Code { get; private set; }
    public List<string> collectedNotes = new List<string>();

    void Awake() => Instance = this;

    void Start()
    {
        Code = "";
        for (int i = 0; i < digits; i++) Code += Random.Range(0, 10);

        // Mezclar los puntos para que las notas cambien de lugar en cada partida
        List<Transform> points = new List<Transform>(spawnPoints);
        for (int i = 0; i < points.Count; i++)
        {
            int j = Random.Range(i, points.Count);
            (points[i], points[j]) = (points[j], points[i]);
        }

        int count = Mathf.Min(digits, points.Count);
        for (int i = 0; i < count; i++)
        {
            GameObject note = Instantiate(notePrefab, points[i].position, points[i].rotation);
            note.GetComponent<Note>().text = $"El dígito {i + 1} es {Code[i]}";
        }
    }

    public void AddNote(string text) => collectedNotes.Add(text);
}
