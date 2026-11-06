using UnityEngine;

// Zona invisible que guarda el punto de reaparición. Va en un cubo con Collider marcado "Is Trigger".
public class Checkpoint : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
            GameManager.Instance.SetCheckpoint(transform.position, other.transform.rotation);
    }
}
