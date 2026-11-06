using UnityEngine;

// Nota con una parte del código. El CodeManager la crea con Instantiate y le pone el texto.
// Va junto a un Interactable (destroyAfterUse activado) cuyo "On Interact" llama a Note.Read.
public class Note : MonoBehaviour
{
    public string text;

    public void Read()
    {
        CodeManager.Instance.AddNote(text);
        DocumentReader.Show("Nota", text);
    }
}
