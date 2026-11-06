using UnityEngine;

// Documento que se lee en pantalla. Va en un objeto con Interactable; en su "On Interact"
// se llama a Document.Read. Sirve para cartas, expedientes, carteles, historia del hospital...
public class Document : MonoBehaviour
{
    public string title = "Documento";
    [TextArea(4, 12)]
    public string text;

    public void Read() => DocumentReader.Show(title, text);
}
