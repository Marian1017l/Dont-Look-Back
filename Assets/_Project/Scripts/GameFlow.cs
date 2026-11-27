using UnityEngine;

// Flujo general del juego: menú -> Nivel 1 -> 2 -> 3 -> final -> menú.
// Los nombres de escena están aquí, en un solo sitio. Cada nivel indica su siguiente escena en su LevelExit.
public static class GameFlow
{
    public const string MenuScene = "MainMenu";
    public const string FirstLevel = "Level01_Ward";
    public const string EndingScene = "Ending";

    // Partida nueva: se borra todo lo que pasa de un nivel a otro (vida, batería, inventario, documentos leídos)
    public static void StartNewGame()
    {
        ResetRunState();
        LoadWithFade(FirstLevel);
    }

    public static void BackToMenu()
    {
        LoadWithFade(MenuScene);
    }

    public static void ResetRunState()
    {
        Inventory.ClearAll();
        PlayerHealth.CarriedHealth = -1f;
        Flashlight.CarriedCharge = -1f;
        IntroSequence.ResetForNewGame();
    }

    public static void LoadWithFade(string scene)
    {
        if (!Application.CanStreamedLevelBeLoaded(scene))
        {
            Debug.LogError("GameFlow: la escena '" + scene + "' no está en Build Settings. Ejecuta Don't Look Back > Crear menú principal y final.");
            return;
        }
        ScreenFader.Go(scene);
    }
}
