using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.Events;

// Utilidades comunes de los constructores de nivel (Level02Builder y los siguientes).
// Level01Builder conserva sus propias copias privadas para no tocar el Nivel 1; solo comparte la tipografía de los rótulos (Glyph).
public static class BuildKit
{
    public static GameObject Spawn(string path, Transform parent)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning("BuildKit: no encontré " + path);
            return null;
        }
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
    }

    public static Transform T(string name, Transform parent = null)
    {
        GameObject go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);
        return go.transform;
    }

    public static Bounds GetBounds(GameObject go)
    {
        bool first = true;
        Bounds b = new Bounds(go.transform.position, Vector3.zero);
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        return b;
    }

    public static void SnapXZ(GameObject go, float x, float z)
    {
        Bounds b = GetBounds(go);
        go.transform.position += new Vector3(x - b.center.x, 0, z - b.center.z);
    }

    // top = true: la cara superior queda en y; false: la base queda en y
    public static void SnapY(GameObject go, float y, bool top)
    {
        Bounds b = GetBounds(go);
        go.transform.position += new Vector3(0, y - (top ? b.max.y : b.min.y), 0);
    }

    public static void EnsureCollider(GameObject go)
    {
        if (go.GetComponentInChildren<Collider>() != null) return;
        Bounds b = GetBounds(go);
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.center = go.transform.InverseTransformPoint(b.center);
        Vector3 s = go.transform.lossyScale;
        box.size = new Vector3(b.size.x / s.x, b.size.y / s.y, b.size.z / s.z);
    }

    public static void Hook(UnityEvent ev, UnityAction action) => UnityEventTools.AddPersistentListener(ev, action);

    // Llama a un método con un argumento bool fijo (por ejemplo GameObject.SetActive)
    public static void HookBool(UnityEvent ev, UnityAction<bool> action, bool value) => UnityEventTools.AddBoolPersistentListener(ev, action, value);

    public static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
    }

    public static void AddToBuildSettings(string path)
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        if (scenes.Exists(s => s.path == path)) return;
        scenes.Add(new EditorBuildSettingsScene(path, true));   // al final: el orden no importa, los niveles se cargan por nombre
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    // ---------------------------------------------------------------- Rótulos (texto 5x7 generado por código)

    // Textura con letras en pixel-art. drip > 0: goteos de pintura. prefix: nombre del archivo en Art/Textures.
    public static Texture2D PaintTexture(string text, int drip = 6, string prefix = "Paint_", bool exact = false)
    {
        const int k = 10;
        string label = text.ToUpper();
        int cols = label.Length * 6, rowsGlyph = 7;
        int w = cols * k, h = (rowsGlyph + drip) * k;
        Color32[] px = new Color32[w * h];
        System.Random rnd = new System.Random(label.GetHashCode());

        for (int i = 0; i < label.Length; i++)
        {
            string[] g = Glyph(label[i]);
            for (int gy = 0; gy < rowsGlyph; gy++)
                for (int gx = 0; gx < 5; gx++)
                {
                    if (g[gy][gx] != '#') continue;
                    int x0 = (i * 6 + gx) * k, y0 = (drip + (rowsGlyph - 1 - gy)) * k;
                    for (int y = 0; y < k; y++)
                        for (int x = 0; x < k; x++)
                        {
                            if ((x == 0 || x == k - 1 || y == 0 || y == k - 1) && rnd.NextDouble() < 0.35) continue;   // bordes irregulares
                            px[(y0 + y) * w + x0 + x] = new Color32(255, 255, 255, 255);
                        }
                    bool bottom = gy == rowsGlyph - 1 || g[gy + 1][gx] != '#';
                    if (drip > 0 && bottom && rnd.NextDouble() < 0.3)
                    {
                        int len = rnd.Next(k, drip * k - 4), cx = x0 + k / 2 + rnd.Next(-2, 3);
                        for (int d = 0; d < len; d++)
                        {
                            int y = y0 - 1 - d;
                            if (y < 0) break;
                            for (int dx = 0; dx < 3; dx++) px[y * w + cx + dx] = new Color32(255, 255, 255, 255);
                        }
                    }
                }
        }

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels32(px);
        tex.Apply();

        EnsureFolder("Assets/_Project/Art", "Textures");
        string path = "Assets/_Project/Art/Textures/" + prefix + System.Text.RegularExpressions.Regex.Replace(label, "[^A-Z0-9]", "_") + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.alphaIsTransparency = true;
        if (exact) { importer.npotScale = TextureImporterNPOTScale.None; importer.maxTextureSize = 4096; }   // sin deformar las proporciones
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // Letras 5x7 (mayúsculas, cifras y signos básicos; sin tildes)
    public static string[] Glyph(char c)
    {
        switch (c)
        {
            case 'A': return new[] { ".###.", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            case 'B': return new[] { "####.", "#...#", "#...#", "####.", "#...#", "#...#", "####." };
            case 'C': return new[] { ".###.", "#...#", "#....", "#....", "#....", "#...#", ".###." };
            case 'D': return new[] { "####.", "#...#", "#...#", "#...#", "#...#", "#...#", "####." };
            case 'E': return new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" };
            case 'F': return new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." };
            case 'G': return new[] { ".###.", "#...#", "#....", "#.###", "#...#", "#...#", ".###." };
            case 'H': return new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" };
            case 'I': return new[] { ".###.", "..#..", "..#..", "..#..", "..#..", "..#..", ".###." };
            case 'J': return new[] { "..###", "...#.", "...#.", "...#.", "...#.", "#..#.", ".##.." };
            case 'K': return new[] { "#...#", "#..#.", "#.#..", "##...", "#.#..", "#..#.", "#...#" };
            case 'L': return new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" };
            case 'M': return new[] { "#...#", "##.##", "#.#.#", "#.#.#", "#...#", "#...#", "#...#" };
            case 'N': return new[] { "#...#", "##..#", "#.#.#", "#..##", "#...#", "#...#", "#...#" };
            case 'O': return new[] { ".###.", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." };
            case 'P': return new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." };
            case 'Q': return new[] { ".###.", "#...#", "#...#", "#...#", "#.#.#", "#..#.", ".##.#" };
            case 'R': return new[] { "####.", "#...#", "#...#", "####.", "#.#..", "#..#.", "#...#" };
            case 'S': return new[] { ".####", "#....", "#....", ".###.", "....#", "....#", "####." };
            case 'T': return new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." };
            case 'U': return new[] { "#...#", "#...#", "#...#", "#...#", "#...#", "#...#", ".###." };
            case 'V': return new[] { "#...#", "#...#", "#...#", "#...#", "#...#", ".#.#.", "..#.." };
            case 'W': return new[] { "#...#", "#...#", "#...#", "#.#.#", "#.#.#", "##.##", "#...#" };
            case 'X': return new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" };
            case 'Y': return new[] { "#...#", "#...#", ".#.#.", "..#..", "..#..", "..#..", "..#.." };
            case 'Z': return new[] { "#####", "....#", "...#.", "..#..", ".#...", "#....", "#####" };
            case '0': return new[] { ".###.", "#...#", "#..##", "#.#.#", "##..#", "#...#", ".###." };
            case '1': return new[] { "..#..", ".##..", "..#..", "..#..", "..#..", "..#..", ".###." };
            case '2': return new[] { ".###.", "#...#", "....#", "..##.", ".#...", "#....", "#####" };
            case '3': return new[] { ".###.", "#...#", "....#", "..##.", "....#", "#...#", ".###." };
            case '4': return new[] { "...#.", "..##.", ".#.#.", "#..#.", "#####", "...#.", "...#." };
            case '5': return new[] { "#####", "#....", "####.", "....#", "....#", "#...#", ".###." };
            case '6': return new[] { ".###.", "#....", "#....", "####.", "#...#", "#...#", ".###." };
            case '7': return new[] { "#####", "....#", "...#.", "..#..", ".#...", ".#...", ".#..." };
            case '8': return new[] { ".###.", "#...#", "#...#", ".###.", "#...#", "#...#", ".###." };
            case '9': return new[] { ".###.", "#...#", "#...#", ".####", "....#", "....#", ".###." };
            case '/': return new[] { "....#", "....#", "...#.", "..#..", ".#...", "#....", "#...." };
            case '-': return new[] { ".....", ".....", ".....", "#####", ".....", ".....", "....." };
            case '.': return new[] { ".....", ".....", ".....", ".....", ".....", "..##.", "..##." };
            case ':': return new[] { ".....", "..#..", "..#..", ".....", "..#..", "..#..", "....." };
            case '!': return new[] { "..#..", "..#..", "..#..", "..#..", "..#..", ".....", "..#.." };
            case '>': return new[] { "#....", ".#...", "..#..", "...#.", "..#..", ".#...", "#...." };
            default: return new[] { ".....", ".....", ".....", ".....", ".....", ".....", "....." };
        }
    }
}
