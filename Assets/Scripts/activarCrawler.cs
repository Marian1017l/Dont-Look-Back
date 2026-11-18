using System.Collections;
using UnityEngine;

public class EventoNPCArrastrando : MonoBehaviour
{
    [Header("Referencias del NPC")]
    public GameObject npcObjeto;
    public Animator npcAnimator;

    [Header("Configuración de Tiempo y Movimiento")]
    public float tiempoArrastrando = 4f;
    public float velocidadDesplazamiento = 0f;

    [Header("Configuración del Parámetro Int")]
    public string nombreParametroInt = "morir";
    public int valorArrastrando = 0;
    public int valorAcostarse = 1;

    private bool eventoActivado = false;
    private bool estaArrastrando = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && !eventoActivado)
        {
            eventoActivado = true;
            StartCoroutine(SecuenciaNPC());
        }
    }

    private void Update()
    {
        if (estaArrastrando && velocidadDesplazamiento > 0)
        {
            npcObjeto.transform.Translate(Vector3.forward * velocidadDesplazamiento * Time.deltaTime);
        }
    }

    private IEnumerator SecuenciaNPC()
    {
        // 1. Aparece el NPC
        if (npcObjeto != null)
            npcObjeto.SetActive(true);

        // Nos aseguramos de que el parámetro Int empiece en 0
        if (npcAnimator != null)
            npcAnimator.SetInteger(nombreParametroInt, valorArrastrando);

        estaArrastrando = true;

        // 2. Esperamos los 4 segundos
        yield return new WaitForSeconds(tiempoArrastrando);

        // 3. Detenemos el avance y cambiamos el Int a 1 para que se acueste
        estaArrastrando = false;

        if (npcAnimator != null)
            npcAnimator.SetInteger(nombreParametroInt, valorAcostarse);
    }
}
