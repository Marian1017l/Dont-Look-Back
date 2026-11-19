using UnityEngine;

// Puerta doble con bisagras (DoorD_V1 de Abandoned_Asylum): al abrirse, cada hoja gira sobre su bisagra con una salida suave.
// Se activa desde el "On Interact" de un Interactable (llamando a Open), igual que Door. "swingDirection" es la dirección del mundo
// hacia la que se abre (normalmente, hacia el lado al que avanza el jugador); el sentido de giro de cada hoja se calcula solo.
public class HingedDoor : MonoBehaviour
{
    public Transform leftLeaf, rightLeaf;
    public Vector3 swingDirection = Vector3.forward;
    public float openAngle = 100f;
    public float smoothTime = 0.55f;

    float angle, velocity, target, leftSign = 1f, rightSign = 1f;
    Quaternion leftClosed, rightClosed;

    void Start()
    {
        leftClosed = leftLeaf.rotation; rightClosed = rightLeaf.rotation;
        leftSign = SignFor(leftLeaf); rightSign = SignFor(rightLeaf);
    }

    // +1 o -1: sentido de giro (sobre el eje vertical) que lleva el borde libre de la hoja hacia swingDirection
    float SignFor(Transform leaf)
    {
        Renderer r = leaf.GetComponentInChildren<Renderer>();
        Vector3 edge = r != null ? r.bounds.center - leaf.position : leaf.right;
        edge.y = 0f; edge.Normalize();
        Vector3 rotated = Quaternion.AngleAxis(90f, Vector3.up) * edge;
        return Vector3.Dot(rotated, swingDirection) >= 0f ? 1f : -1f;
    }

    public void Open() => target = openAngle;
    public void Close() => target = 0f;
    public void Toggle() => target = target > 0f ? 0f : openAngle;

    void Update()
    {
        if (Mathf.Approximately(angle, target)) return;
        angle = Mathf.SmoothDamp(angle, target, ref velocity, smoothTime);
        if (Mathf.Abs(angle - target) < 0.05f) { angle = target; velocity = 0f; }
        leftLeaf.rotation = Quaternion.AngleAxis(leftSign * angle, Vector3.up) * leftClosed;
        rightLeaf.rotation = Quaternion.AngleAxis(rightSign * angle, Vector3.up) * rightClosed;
    }
}
