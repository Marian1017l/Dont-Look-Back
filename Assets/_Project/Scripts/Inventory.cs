using System.Collections.Generic;

// Lista simple de objetos que lleva el jugador: llaves, tarjetas, fusibles, pistas... (por ejemplo "tarjeta_azul").
// Cada objeto es solo un nombre; quien lo pide y quien lo da usan el mismo texto.
public static class Inventory
{
    static readonly HashSet<string> items = new HashSet<string>();

    public static void Add(string id) => items.Add(id);
    public static bool Has(string id) => items.Contains(id);
    public static void Clear() => items.Clear();
}
