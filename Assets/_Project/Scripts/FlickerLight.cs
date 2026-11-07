using UnityEngine;

// Luz defectuosa: parpadea con ruido suave y de vez en cuando se apaga un instante.
// Se pone en el mismo objeto que la Light.
[RequireComponent(typeof(Light))]
public class FlickerLight : MonoBehaviour
{
    [Range(0, 1)] public float minFactor = 0.15f;   // intensidad mínima respecto a la original
    public float speed = 9f;
    public float blackoutChance = 0.004f;           // probabilidad por fotograma de un apagón corto

    Light lamp;
    float baseIntensity;
    float seed;
    float blackout;

    void Awake()
    {
        lamp = GetComponent<Light>();
        baseIntensity = lamp.intensity;
        seed = Random.value * 100f;
    }

    void Update()
    {
        if (blackout > 0f)
        {
            blackout -= Time.deltaTime;
            lamp.intensity = 0f;
            return;
        }
        if (Random.value < blackoutChance) blackout = Random.Range(0.1f, 0.5f);

        float n = Mathf.PerlinNoise(Time.time * speed, seed);
        lamp.intensity = baseIntensity * Mathf.Lerp(minFactor, 1f, Mathf.Clamp01(n / 0.4f));
    }
}
