using UnityEngine;

// Indica si hay un panel abierto (candado, documento...). Mientras lo hay,
// el jugador no se mueve y el cursor está libre para usar los botones.
public static class UIState
{
    public static bool PanelOpen { get; private set; }
    public static bool JustClosed => Time.frameCount == closedFrame;   // la E que cierra no debe volver a usar el objeto

    static int closedFrame = -1;

    public static void OpenPanel()
    {
        PanelOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void ClosePanel()
    {
        PanelOpen = false;
        closedFrame = Time.frameCount;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
