using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class DialogoNPC : MonoBehaviour
{
    [Header("Configuración de UI")]
    public GameObject panelDialogo;
    public TextMeshProUGUI textoDialogo;

    [Header("Configuración de Diálogos")]
    [Tooltip("Añade todos los diálogos en orden. El último se repetirá indefinidamente.")]
    [TextArea(2, 5)]
    public List<string> dialogos = new List<string>();

    [Header("Efecto Máquina de Escribir")]
    [Tooltip("Tiempo en segundos entre cada letra (menor = más rápido)")]
    public float velocidadEscritura = 0.04f;

    // Variables internas
    private int indiceDialogoActual = 0;
    private Coroutine corrutinaEscritura;

    private void Start()
    {
        if (panelDialogo != null)
            panelDialogo.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Evitamos errores si olvidaste agregar diálogos en el Inspector
            if (dialogos.Count == 0) return;

            MostrarSiguienteDialogo();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            // Si estaba escribiendo cuando el jugador se fue, detenemos el efecto
            if (corrutinaEscritura != null)
                StopCoroutine(corrutinaEscritura);

            if (panelDialogo != null)
                panelDialogo.SetActive(false);
        }
    }

    private void MostrarSiguienteDialogo()
    {
        if (panelDialogo != null)
            panelDialogo.SetActive(true);

        // Tomamos el texto correspondiente a la visita actual
        string textoAMostrar = dialogos[indiceDialogoActual];

        // Si ya había una escritura en curso, la reiniciamos
        if (corrutinaEscritura != null)
            StopCoroutine(corrutinaEscritura);

        corrutinaEscritura = StartCoroutine(EscribirLetraPorLetra(textoAMostrar));

        // Avanzamos al siguiente diálogo para la próxima vez, 
        // pero nos detenemos en el último de la lista para que se repita siempre
        if (indiceDialogoActual < dialogos.Count - 1)
        {
            indiceDialogoActual++;
        }
    }

    // Corrutina para el efecto de suspenso letra por letra
    private IEnumerator EscribirLetraPorLetra(string fraseCompleta)
    {
        textoDialogo.text = ""; // Limpiamos el cuadro de texto

        foreach (char letra in fraseCompleta.ToCharArray())
        {
            textoDialogo.text += letra; // Añadimos una letra
            yield return new WaitForSeconds(velocidadEscritura); // Esperamos un instante
        }
    }
}
