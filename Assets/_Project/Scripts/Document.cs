using UnityEngine;

// Tipo de hoja: cambia el papel, la tipografía y la presentación al leerlo.
//  PatientNote   = hoja arrugada escrita a mano por alguien asustado (sin membrete)
//  MedicalRecord = expediente clínico: membrete, referencia, fecha, sello, máquina de escribir
//  StaffReport   = informe interno del personal: membrete sobrio, referencia, fecha, firma
public enum DocumentStyle { PatientNote, MedicalRecord, StaffReport }

// Documento que se lee en pantalla. Va en un objeto con Interactable; en su "On Interact"
// se llama a Document.Read. Sirve para cartas, expedientes, carteles, historia del hospital...
public class Document : MonoBehaviour
{
    public string title = "Documento";
    public DocumentStyle style = DocumentStyle.StaffReport;
    public string reference;   // identificador ficticio, por ejemplo "0214-B"
    public string date;        // fecha escrita en la hoja, por ejemplo "02/12/1993"
    public string author;      // firma o remitente (vacío = sin firma)
    [TextArea(4, 12)]
    public string text;

    // Prepara la hoja de antemano para que abrir el documento no cause un tirón
    void Start() => PaperTexture.Get(style);

    // Lo lee en pantalla y lo guarda en el inventario (pestaña Documentos) para poder releerlo
    public void Read()
    {
        Inventory.AddDocument(new DocumentEntry { title = title, text = text, style = style, reference = reference, date = date, author = author });
        DocumentReader.Show(title, text, style, reference, date, author);
    }
}
