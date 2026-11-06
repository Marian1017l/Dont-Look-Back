using UnityEngine;

// Frasco lanzado por la trampa. Quita una vida si golpea al jugador antes de tocar otra cosa.
public class Projectile : MonoBehaviour
{
    public float lifeTime = 5f;
    bool dangerous = true;

    void Start() => Destroy(gameObject, lifeTime);

    void OnCollisionEnter(Collision collision)
    {
        if (dangerous && collision.collider.CompareTag("Player"))
        {
            GameManager.Instance.LoseLife();
            Destroy(gameObject);
            return;
        }
        dangerous = false;   // después de rebotar ya no hace daño
    }
}
