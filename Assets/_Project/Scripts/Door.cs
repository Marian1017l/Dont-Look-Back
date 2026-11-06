using UnityEngine;

// Puerta que se desliza al abrirse. Se activa desde el "On Interact" de un Interactable
// (llamando a Open o Toggle), así sirve para puertas con llave y para interruptores.
public class Door : MonoBehaviour
{
    public Vector3 openOffset = new Vector3(0, 3, 0);   // cuánto se mueve al abrir
    public float speed = 2f;

    Vector3 closedPosition;
    bool open;

    void Start() => closedPosition = transform.position;

    public void Open() => open = true;
    public void Close() => open = false;
    public void Toggle() => open = !open;

    void Update()
    {
        Vector3 target = open ? closedPosition + openOffset : closedPosition;
        transform.position = Vector3.MoveTowards(transform.position, target, speed * Time.deltaTime);
    }
}
