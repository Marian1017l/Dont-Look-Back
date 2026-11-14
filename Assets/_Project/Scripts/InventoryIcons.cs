using System.Collections.Generic;
using UnityEngine;

public enum ItemIcon { Generic, Key, Flashlight }

// Iconos del inventario dibujados por código (sin imágenes externas): formas geométricas suavizadas
// con sombra y degradado metálico. Se generan una sola vez y se guardan.
public static class InventoryIcons
{
    const int Size = 128;
    delegate void Painter(Vector2 p, ref Color px);

    static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
    static Texture2D glow, grain;

    public static Texture2D Get(ItemIcon icon)
    {
        switch (icon)
        {
            case ItemIcon.Key: return Cached("key", PaintKey);
            case ItemIcon.Flashlight: return Cached("flashlight", PaintFlashlight);
            default: return Cached("generic", PaintGeneric);
        }
    }

    public static Texture2D Document(DocumentStyle style) =>
        Cached("doc" + style, (Vector2 p, ref Color c) => PaintDocument(p, ref c, style));

    // Resplandor circular suave (detrás del objeto seleccionado)
    public static Texture2D Glow
    {
        get
        {
            if (glow != null) return glow;
            glow = Make(64, (Vector2 p, ref Color c) =>
            {
                float d = Mathf.Clamp01(1f - Vector2.Distance(p, new Vector2(0.5f, 0.5f)) * 2f);
                c = new Color(1f, 1f, 1f, d * d);
            });
            return glow;
        }
    }

    // Grano fino repetible: da textura de pared sucia al panel
    public static Texture2D Grain
    {
        get
        {
            if (grain != null) return grain;
            const int n = 128;
            System.Random rnd = new System.Random(7);
            grain = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear };
            Color[] px = new Color[n * n];
            for (int i = 0; i < px.Length; i++)
            {
                float v = (float)rnd.NextDouble();
                px[i] = v > 0.5f ? new Color(1f, 1f, 0.9f, (v - 0.5f) * 0.09f) : new Color(0f, 0f, 0f, (0.5f - v) * 0.22f);
            }
            grain.SetPixels(px);
            grain.Apply();
            return grain;
        }
    }

    static Texture2D Cached(string key, Painter painter)
    {
        if (cache.TryGetValue(key, out Texture2D tex) && tex != null) return tex;
        tex = Make(Size, painter);
        cache[key] = tex;
        return tex;
    }

    static Texture2D Make(int size, Painter paint)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
        Color[] px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Color c = Color.clear;
                paint(new Vector2((x + 0.5f) / size, (y + 0.5f) / size), ref c);
                px[y * size + x] = c;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- Formas (distancia con signo: negativo = dentro)

    static float Circle(Vector2 p, float cx, float cy, float r) => Vector2.Distance(p, new Vector2(cx, cy)) - r;

    static float Box(Vector2 p, float cx, float cy, float hx, float hy, float round = 0f)
    {
        float dx = Mathf.Abs(p.x - cx) - (hx - round), dy = Mathf.Abs(p.y - cy) - (hy - round);
        float outside = new Vector2(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(dx, dy), 0f) - round;
    }

    static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);   // oscurece sin tocar la opacidad
    static float Min(float a, float b) => Mathf.Min(a, b);
    static float Min(float a, float b, float c) => Mathf.Min(a, Mathf.Min(b, c));
    static float Subtract(float a, float b) => Mathf.Max(a, -b);

    static Vector2 Rot(Vector2 p, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad, cos = Mathf.Cos(r), sin = Mathf.Sin(r);
        Vector2 q = p - new Vector2(0.5f, 0.5f);
        return new Vector2(q.x * cos - q.y * sin, q.x * sin + q.y * cos) + new Vector2(0.5f, 0.5f);
    }

    // Pinta una forma encima: borde suavizado y un reborde más oscuro cerca del contorno
    static void Layer(ref Color px, float d, Color fill, Color rim)
    {
        float coverage = Mathf.Clamp01(0.5f - d * Size);
        if (coverage <= 0f) return;
        Color c = Color.Lerp(rim, fill, Mathf.Clamp01(-d / 0.028f));
        c.a = fill.a * coverage;
        Over(ref px, c);
    }

    static void Over(ref Color dst, Color src)
    {
        float a = src.a + dst.a * (1f - src.a);
        if (a <= 0f) { dst = Color.clear; return; }
        float k = dst.a * (1f - src.a);
        dst = new Color((src.r * src.a + dst.r * k) / a, (src.g * src.a + dst.g * k) / a, (src.b * src.a + dst.b * k) / a, a);
    }

    static readonly Color Shadow = new Color(0f, 0f, 0f, 0.4f);

    // ---------------------------------------------------------------- Iconos

    static float KeyShape(Vector2 q)
    {
        float ring = Subtract(Circle(q, 0.25f, 0.5f, 0.17f), Circle(q, 0.25f, 0.5f, 0.075f));
        float shaft = Box(q, 0.62f, 0.5f, 0.26f, 0.036f, 0.014f);
        float collar = Box(q, 0.45f, 0.5f, 0.022f, 0.074f, 0.01f);
        float teeth = Min(Box(q, 0.77f, 0.42f, 0.028f, 0.06f, 0.008f), Box(q, 0.86f, 0.435f, 0.024f, 0.05f, 0.008f));
        return Min(Min(ring, shaft, collar), teeth);
    }

    static void PaintKey(Vector2 p, ref Color c)
    {
        Vector2 q = Rot(p, -38f);
        Layer(ref c, KeyShape(Rot(p - new Vector2(0.018f, -0.024f), -38f)), Shadow, Shadow);
        float d = KeyShape(q);
        Color brass = Color.Lerp(new Color(0.5f, 0.36f, 0.13f), new Color(0.93f, 0.78f, 0.42f), p.y);
        Layer(ref c, d, brass, new Color(0.26f, 0.17f, 0.06f));
        Layer(ref c, Mathf.Max(Box(q, 0.62f, 0.518f, 0.24f, 0.007f), d + 0.012f), new Color(1f, 0.96f, 0.75f, 0.55f), new Color(1f, 0.96f, 0.75f, 0.55f));   // brillo del metal
    }

    static float FlashlightShape(Vector2 q)
    {
        float handle = Box(q, 0.33f, 0.5f, 0.21f, 0.068f, 0.03f);
        float neck = Box(q, 0.53f, 0.5f, 0.05f, 0.085f, 0.01f);
        float head = Box(q, 0.64f, 0.5f, 0.095f, 0.108f, 0.02f);
        return Min(handle, neck, head);
    }

    static void PaintFlashlight(Vector2 p, ref Color c)
    {
        Vector2 q = Rot(p, -35f);

        // Haz de luz: cuña suave que sale de la lente
        float along = q.x - 0.74f;
        if (along > 0f)
        {
            float half = 0.06f + along * 0.55f;
            float beam = Mathf.Clamp01((half - Mathf.Abs(q.y - 0.5f)) / 0.04f) * Mathf.Clamp01((0.98f - q.x) / 0.22f);
            Over(ref c, new Color(1f, 0.93f, 0.65f, beam * 0.26f));
        }

        Layer(ref c, FlashlightShape(Rot(p - new Vector2(0.018f, -0.024f), -35f)), Shadow, Shadow);
        Color steel = Color.Lerp(new Color(0.14f, 0.15f, 0.17f), new Color(0.46f, 0.48f, 0.52f), p.y);
        Layer(ref c, FlashlightShape(q), steel, new Color(0.05f, 0.05f, 0.06f));

        // Agarre, cabeza más clara, interruptor y lente
        float body = FlashlightShape(q);
        float grip = Min(Box(q, 0.2f, 0.5f, 0.006f, 0.062f), Box(q, 0.25f, 0.5f, 0.006f, 0.062f), Box(q, 0.3f, 0.5f, 0.006f, 0.062f));
        Layer(ref c, Mathf.Max(grip, body + 0.01f), new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0.55f));
        Layer(ref c, Box(q, 0.64f, 0.5f, 0.095f, 0.108f, 0.02f), Color.Lerp(new Color(0.22f, 0.23f, 0.25f), new Color(0.62f, 0.64f, 0.68f), p.y), new Color(0.06f, 0.06f, 0.07f));
        Layer(ref c, Box(q, 0.4f, 0.575f, 0.034f, 0.014f, 0.007f), new Color(0.72f, 0.2f, 0.14f), new Color(0.3f, 0.06f, 0.04f));
        Layer(ref c, Box(q, 0.728f, 0.5f, 0.016f, 0.09f, 0.008f), new Color(1f, 0.94f, 0.7f), new Color(0.8f, 0.65f, 0.3f));
        float glowAmount = Mathf.Clamp01(1f - Vector2.Distance(q, new Vector2(0.74f, 0.5f)) / 0.2f);
        Over(ref c, new Color(1f, 0.92f, 0.6f, glowAmount * glowAmount * 0.5f));
    }

    static void PaintDocument(Vector2 p, ref Color c, DocumentStyle style)
    {
        Vector2 q = Rot(p, 5f);
        Color paper = style == DocumentStyle.PatientNote ? new Color(0.86f, 0.78f, 0.58f)
                    : style == DocumentStyle.MedicalRecord ? new Color(0.9f, 0.86f, 0.74f)
                    : new Color(0.9f, 0.89f, 0.84f);

        System.Func<Vector2, float> page = v => Mathf.Max(Box(v, 0.5f, 0.5f, 0.26f, 0.34f, 0.015f), (v.x + v.y - 1.47f) * 0.7071f);   // hoja con la esquina doblada
        Layer(ref c, page(Rot(p - new Vector2(0.016f, -0.022f), 5f)), Shadow, Shadow);
        float d = page(q);
        Layer(ref c, d, Color.Lerp(Shade(paper, 0.8f), paper, p.y + 0.2f), Shade(paper, 0.5f));

        float fold = Mathf.Max(Box(q, 0.69f, 0.78f, 0.06f, 0.06f), (1.47f - (q.x + q.y)) * 0.7071f);
        Layer(ref c, fold, Shade(paper, 0.62f), Shade(paper, 0.4f));

        Color ink = style == DocumentStyle.PatientNote ? new Color(0.12f, 0.12f, 0.3f, 0.65f) : new Color(0.1f, 0.09f, 0.08f, 0.6f);
        float[] widths = { 0.17f, 0.2f, 0.2f, 0.12f, 0.19f };
        for (int i = 0; i < widths.Length; i++)
            Layer(ref c, Mathf.Max(Box(q, 0.5f - (0.2f - widths[i]), 0.66f - i * 0.085f, widths[i], 0.008f), d + 0.02f), ink, ink);

        if (style == DocumentStyle.MedicalRecord)
            Layer(ref c, Mathf.Max(Box(q, 0.62f, 0.27f, 0.09f, 0.035f, 0.008f), d + 0.02f), new Color(0.62f, 0.12f, 0.1f, 0.6f), new Color(0.62f, 0.12f, 0.1f, 0.6f));   // sello
    }

    static void PaintGeneric(Vector2 p, ref Color c)
    {
        Layer(ref c, Box(p - new Vector2(0.016f, -0.022f), 0.5f, 0.5f, 0.28f, 0.28f, 0.05f), Shadow, Shadow);
        Color gray = Color.Lerp(new Color(0.22f, 0.24f, 0.25f), new Color(0.55f, 0.57f, 0.58f), p.y);
        Layer(ref c, Box(p, 0.5f, 0.5f, 0.28f, 0.28f, 0.05f), gray, new Color(0.08f, 0.09f, 0.09f));
        Layer(ref c, Box(p, 0.5f, 0.5f, 0.1f, 0.1f, 0.02f), new Color(0.1f, 0.11f, 0.11f, 0.6f), new Color(0.1f, 0.11f, 0.11f, 0.6f));
    }
}
