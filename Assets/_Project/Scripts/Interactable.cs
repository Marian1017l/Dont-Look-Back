using UnityEngine;
using UnityEngine.Events;

// Cualquier objeto que el jugador usa con E: nota, llave, puerta, interruptor...
// Se configura desde el Inspector; no hace falta un script nuevo para cada objeto.
public class Interactable : MonoBehaviour
{
    public string prompt = "Interactuar";         // texto que aparece al mirarlo
    // "Objeto" es cualquier cosa que el jugador pueda llevar: llave, tarjeta, fusible, un dígito del código...
    public string requiredItem;                   // objeto que pide (vacío = ninguno), ej. "tarjeta_azul"
    public string lockedMessage = "Está cerrado"; // mensaje si falta el objeto
    public string giveItem;                       // objeto que entrega al usarlo (vacío = ninguno)
    public bool destroyAfterUse;                  // true para objetos que se recogen
    public UnityEvent onInteract = new UnityEvent(); // qué pasa: abrir una puerta, leer una nota...

    // Devuelve false si el jugador no tiene el objeto que se pide
    public bool Interact()
    {
        if (!string.IsNullOrEmpty(requiredItem) && !Inventory.Has(requiredItem)) return false;

        if (!string.IsNullOrEmpty(giveItem)) Inventory.Add(giveItem);
        onInteract.Invoke();
        if (destroyAfterUse) Destroy(gameObject);
        return true;
    }
}
