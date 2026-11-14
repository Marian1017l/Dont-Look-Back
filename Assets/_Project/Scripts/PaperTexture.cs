using System.Collections.Generic;
using UnityEngine;

// Genera por código la hoja de cada tipo de documento (sin imágenes externas):
// fibras, manchas de humedad y de café, dobleces, bordes gastados o rotos y detalles propios de cada tipo.
// Se crea una sola vez por tipo y se guarda; DocumentReader la dibuja y Document la prepara de antemano.
public static class PaperTexture
{
    public const int Width = 416, Height = 572;   // proporción 1 : 1,375 (hoja tamaño carta)

    static readonly Dictionary<DocumentStyle, Texture2D> cache = new Dictionary<DocumentStyle, Texture2D>();

    public static Texture2D Get(DocumentStyle style)
    {
        if (cache.TryGetValue(style, out Texture2D tex) && tex != null) return tex;
        tex = Create(style);
        cache[style] = tex;
        return tex;
    }

    // smoothstep de shader (Mathf.SmoothStep no sirve aquí: su tercer argumento es un factor de 0 a 1, no un valor dentro del rango)
    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Texture2D Create(DocumentStyle style)
    {
        int seed = 17 + (int)style * 31;
        System.Random rnd = new System.Random(seed);
        float ox = seed * 0.37f, oy = seed * 0.91f;
        bool patient = style == DocumentStyle.PatientNote;

        // Color base del papel según el tipo: libreta amarillenta, expediente crema, informe blanco sucio
        Color baseColor = patient ? new Color(0.86f, 0.78f, 0.58f)
                        : style == DocumentStyle.MedicalRecord ? new Color(0.88f, 0.84f, 0.72f)
                        : new Color(0.88f, 0.87f, 0.82f);
        float creaseStrength = patient ? 0.16f : 0.09f;

        // Dobleces: uno horizontal por la mitad y, en las notas de pacientes, varios arrugados al azar
        int creaseCount = patient ? 4 : 2;
        var creases = new (float x, float y, float sin, float cos)[creaseCount];
        for (int i = 0; i < creaseCount; i++)
        {
            float angle = i == 0 ? 0f : i == 1 && !patient ? Mathf.PI / 2f : (float)rnd.NextDouble() * Mathf.PI;
            float cx = i == 1 && !patient ? Width * 0.5f : Width * (0.25f + 0.5f * (float)rnd.NextDouble());
            float cy = i == 0 ? Height * (0.48f + 0.04f * (float)rnd.NextDouble()) : Height * (0.2f + 0.6f * (float)rnd.NextDouble());
            creases[i] = (cx, cy, Mathf.Sin(angle), Mathf.Cos(angle));
        }

        // Manchas de café (anillo irregular) en posiciones fijas por tipo
        var stains = new (float x, float y, float r)[]
        {
            (Width * (0.55f + 0.3f * (float)rnd.NextDouble()), Height * (0.15f + 0.2f * (float)rnd.NextDouble()), 38f + 24f * (float)rnd.NextDouble()),
            (Width * (0.1f + 0.2f * (float)rnd.NextDouble()), Height * (0.7f + 0.2f * (float)rnd.NextDouble()), 22f + 14f * (float)rnd.NextDouble()),
        };
        Color coffee = new Color(0.42f, 0.27f, 0.12f), damp = new Color(0.55f, 0.5f, 0.33f);

        Color32[] px = new Color32[Width * Height];
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                float u = x / (float)Width, v = y / (float)Height;

                // Borde: irregular (muy roto arriba en las notas de pacientes) y más oscuro y amarillento por el desgaste
                float d = Mathf.Min(Mathf.Min(x, Width - 1 - x), Mathf.Min(y, Height - 1 - y));
                float amp = patient && Height - 1 - y == d ? 16f : 5f;
                float rag = 3f + amp * Mathf.PerlinNoise(x * 0.07f + ox, y * 0.07f + oy);
                float inside = d - rag;
                if (inside <= 0f) { px[y * Width + x] = new Color32(0, 0, 0, 0); continue; }
                float alpha = Mathf.Clamp01(inside / 1.5f);
                float wear = 1f - Smooth(0f, 34f, inside);

                // Fibras finas, vetas largas y manchas grandes de envejecimiento
                float grain = Mathf.PerlinNoise(x * 0.9f + ox, y * 0.9f + oy) - 0.5f;
                float fiber = Mathf.PerlinNoise(x * 0.06f + ox, y * 0.35f + oy) - 0.5f;
                float blotch = Mathf.PerlinNoise(u * 3f + ox, v * 4f + oy);
                float shade = 1f + grain * 0.06f + fiber * 0.07f - Mathf.Clamp01((blotch - 0.45f) * 2f) * 0.14f - wear * 0.2f;

                // Dobleces: línea oscura con un reflejo al lado. Se desvanecen a tramos (no cruzan toda la hoja como una regla)
                for (int k = 0; k < creases.Length; k++)
                {
                    var c = creases[k];
                    float sd = -(x - c.x) * c.sin + (y - c.y) * c.cos;
                    if (sd > 12f || sd < -12f) continue;
                    float along = (x - c.x) * c.cos + (y - c.y) * c.sin;
                    float fade = Smooth(0.3f, 0.6f, Mathf.PerlinNoise(along * 0.012f + ox + k * 7f, k * 3.1f + 0.5f));
                    shade -= Mathf.Exp(-(sd * sd) / 9.7f) * creaseStrength * fade;
                    shade += Mathf.Exp(-((sd - 4f) * (sd - 4f)) / 18f) * creaseStrength * 0.5f * fade;
                }

                Color col = baseColor * shade;
                col = Color.Lerp(col, col * new Color(1f, 0.82f, 0.6f), wear * 0.6f);

                // Humedad (mancha ancha y tenue) y anillos de café
                float dampMask = Smooth(0.6f, 0.8f, Mathf.PerlinNoise(u * 2.2f + ox * 2f, v * 2.2f + oy * 2f));
                col = Color.Lerp(col, damp * shade, dampMask * 0.35f);
                foreach (var s in stains)
                {
                    float dx = x - s.x, dy = y - s.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > s.r * 1.35f + 8f) continue;
                    // Anillo irregular: radio y grosor cambian según el ángulo
                    float ang = Mathf.Atan2(dy, dx);
                    float r = s.r * (1f + 0.22f * (Mathf.PerlinNoise(Mathf.Cos(ang) * 1.3f + ox, Mathf.Sin(ang) * 1.3f + s.x * 0.01f) - 0.5f));
                    float strength = 0.35f + 0.65f * Mathf.PerlinNoise(Mathf.Cos(ang) * 2f + oy, Mathf.Sin(ang) * 2f + 9f);
                    float ring = Mathf.Exp(-((dist - r) * (dist - r)) / 12.5f) * 0.32f * strength + (dist < r ? 0.04f : 0f);
                    col = Color.Lerp(col, coffee, ring);
                }

                // Detalles del tipo: agujeros de espiral (libreta) o grapa (informe)
                if (patient)
                {
                    for (int k = 0; k < 11; k++)
                    {
                        float hx = x - 24f, hy = y - (48f + k * 62f);
                        float hd = hx * hx + hy * hy;
                        if (hd < 49f) alpha = 0f;
                        else if (hd < 72f) col *= 0.8f;
                    }
                }
                else if (style == DocumentStyle.StaffReport && x >= 30 && x <= 46 && y >= Height - 43 && y <= Height - 40)
                    col = new Color(0.45f, 0.45f, 0.48f);

                col.a = 1f;
                Color32 c32 = col;
                c32.a = (byte)Mathf.RoundToInt(alpha * 255f);
                px[y * Width + x] = c32;
            }

        Texture2D tex = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
        tex.name = "Paper_" + style;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels32(px);
        tex.Apply();
        return tex;
    }
}
