using UnityEngine;
using UnityEngine.InputSystem;

// Movimiento en primera persona con WASD y ratón (usa el Input System nuevo).
// Va en el objeto "Player" (con tag Player y un CharacterController). La cámara es hija del jugador.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    public Transform playerCamera;
    public float walkSpeed = 3f;
    public float mouseSensitivity = 0.1f;
    public float gravity = -9.81f;

    CharacterController controller;
    float verticalVelocity;
    float cameraPitch;

    void Start()
    {
        controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (UIState.PanelOpen) return;   // no moverse mientras hay un panel abierto
        Look();
        Move();
    }

    void Look()
    {
        if (Mouse.current == null) return;
        Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;

        transform.Rotate(Vector3.up * delta.x);
        cameraPitch = Mathf.Clamp(cameraPitch - delta.y, -80f, 80f);
        playerCamera.localEulerAngles = new Vector3(cameraPitch, 0f, 0f);
    }

    void Move()
    {
        Vector2 input = Vector2.zero;
        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.wKey.isPressed) input.y += 1;
            if (k.sKey.isPressed) input.y -= 1;
            if (k.dKey.isPressed) input.x += 1;
            if (k.aKey.isPressed) input.x -= 1;
        }

        Vector3 move = (transform.right * input.x + transform.forward * input.y).normalized * walkSpeed;

        if (controller.isGrounded && verticalVelocity < 0) verticalVelocity = -2f;
        verticalVelocity += gravity * Time.deltaTime;
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);
    }
}
