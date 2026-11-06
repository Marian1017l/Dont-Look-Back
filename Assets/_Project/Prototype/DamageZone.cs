using UnityEngine;

// Zona de prueba que quita una vida al entrar. Sirve para probar el sistema de vidas
// antes de tener los pasos y los cañones. Va en un cubo con Collider marcado "Is Trigger".
public class DamageZone : MonoBehaviour
{
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) GameManager.Instance.LoseLife();
    }
}
