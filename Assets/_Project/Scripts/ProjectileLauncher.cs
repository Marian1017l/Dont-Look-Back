using UnityEngine;

// Trampa tipo cañón: cuando el jugador entra en la zona, crea un proyectil (Instantiate)
// y calcula la velocidad para que caiga en parábola sobre él.
// Va en un objeto con un BoxCollider marcado "Is Trigger" que marca la zona de disparo.
public class ProjectileLauncher : MonoBehaviour
{
    public GameObject projectilePrefab;
    public Transform firePoint;
    public float launchAngle = 35f;   // grados sobre la horizontal
    public float cooldown = 2.5f;

    float lastShot = -100f;

    void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player") || Time.time < lastShot + cooldown) return;
        lastShot = Time.time;
        Fire(other.transform.position + Vector3.up * 0.5f);
    }

    void Fire(Vector3 target)
    {
        Vector3 toTarget = target - firePoint.position;
        Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
        float d = flat.magnitude;       // distancia horizontal
        float h = toTarget.y;           // diferencia de altura
        float a = launchAngle * Mathf.Deg2Rad;
        float g = -Physics.gravity.y;

        // Tiro parabólico: h = d·tan(a) - g·d² / (2·v²·cos²(a))  →  despejamos v
        float denom = 2f * Mathf.Cos(a) * Mathf.Cos(a) * (d * Mathf.Tan(a) - h);
        if (denom <= 0f) return;        // con este ángulo no se alcanza el objetivo
        float speed = Mathf.Sqrt(g * d * d / denom);

        Vector3 direction = flat.normalized * Mathf.Cos(a) + Vector3.up * Mathf.Sin(a);
        GameObject p = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        p.GetComponent<Rigidbody>().AddForce(direction * speed, ForceMode.VelocityChange);
    }
}
