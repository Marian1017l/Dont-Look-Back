using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Texturas del Nivel 3 dibujadas por código (no hay assets externos): los dibujos del paciente 214, garabatos de pared,
// marcas de suelo y manchas. Todo se guarda como PNG en Art/Textures con el prefijo L3_.
// Estilo: trazo de carboncillo / lápiz de cera con temblor, presión variable y papel manchado.
public static class Level03Art
{
    const string Dir = "Assets/_Project/Art/Textures/";

    // ---------------------------------------------------------------- Lienzo

    public class Canvas
    {
        public readonly int w, h;
        readonly Color[] px;
        readonly System.Random rnd;
        readonly float seed;

        public Canvas(int w, int h, Color bg, int seedValue)
        {
            this.w = w; this.h = h;
            px = new Color[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = bg;
            rnd = new System.Random(seedValue);
            seed = seedValue * 0.37f;
        }

        public float R() => (float)rnd.NextDouble();
        public float R(float a, float b) => a + R() * (b - a);

        void Blend(int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= w || y >= h || a <= 0f) return;
            Color d = px[y * w + x];
            float oa = a + d.a * (1f - a);
            if (oa <= 0f) return;
            px[y * w + x] = new Color((c.r * a + d.r * d.a * (1f - a)) / oa, (c.g * a + d.g * d.a * (1f - a)) / oa, (c.b * a + d.b * d.a * (1f - a)) / oa, oa);
        }

        // Una pasada del lápiz: disco con borde irregular
        public void Stamp(float x, float y, float rad, Color c, float flow)
        {
            int x0 = Mathf.Max(0, (int)(x - rad - 1)), x1 = Mathf.Min(w - 1, (int)(x + rad + 1));
            int y0 = Mathf.Max(0, (int)(y - rad - 1)), y1 = Mathf.Min(h - 1, (int)(y + rad + 1));
            for (int yy = y0; yy <= y1; yy++)
                for (int xx = x0; xx <= x1; xx++)
                {
                    float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                    if (d > rad) continue;
                    float a = flow * Mathf.Clamp01(1.4f - d / rad) * (0.7f + 0.3f * R());
                    Blend(xx, yy, c, a);
                }
        }

        public void Line(Vector2 a, Vector2 b, float rad, Color c, float wobble = 1.6f, float flow = 0.85f)
        {
            float len = (b - a).magnitude;
            int n = Mathf.Max(1, (int)(len / 1.3f));
            Vector2 dir = (b - a).normalized, perp = new Vector2(-dir.y, dir.x);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float off = (Mathf.PerlinNoise(seed + i * 0.05f, seed * 0.3f + a.x * 0.01f) - 0.5f) * 2f * wobble;
                Vector2 p = Vector2.Lerp(a, b, t) + perp * off;
                Stamp(p.x, p.y, rad * R(0.8f, 1.15f), c, flow);
            }
        }

        public void Poly(IList<Vector2> pts, float rad, Color c, float wobble = 1.6f)
        {
            for (int i = 0; i + 1 < pts.Count; i++) Line(pts[i], pts[i + 1], rad, c, wobble);
        }

        public void Arc(Vector2 center, float radius, float a0, float a1, float rad, Color c, float wobble = 1.6f, float squash = 1f)
        {
            int n = Mathf.Max(8, (int)(Mathf.Abs(a1 - a0) * radius / 6f));
            var pts = new List<Vector2>();
            for (int i = 0; i <= n; i++)
            {
                float t = Mathf.Lerp(a0, a1, i / (float)n);
                float r = radius * (1f + 0.03f * (Mathf.PerlinNoise(seed + i * 0.2f, 3f) - 0.5f));
                pts.Add(center + new Vector2(Mathf.Cos(t) * r, Mathf.Sin(t) * r * squash));
            }
            Poly(pts, rad, c, wobble * 0.5f);
        }

        public void Circle(Vector2 center, float radius, float rad, Color c) => Arc(center, radius, 0f, Mathf.PI * 2f + 0.25f, rad, c);

        public void Spiral(Vector2 center, float r0, float r1, float turns, float rad, Color c)
        {
            var pts = new List<Vector2>();
            int n = (int)(turns * 40);
            for (int i = 0; i <= n; i++)
            {
                float t = i / (float)n;
                float ang = t * turns * Mathf.PI * 2f;
                float r = Mathf.Lerp(r0, r1, t) * (1f + 0.04f * (Mathf.PerlinNoise(seed + i * 0.15f, 7f) - 0.5f));
                pts.Add(center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r);
            }
            Poly(pts, rad, c, 0.6f);
        }

        public void Fill(Vector2 center, float radius, Color c, float flow)
        {
            for (int i = 0; i < radius * 3; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * radius * 0.8f;
                Stamp(p.x, p.y, radius * 0.35f, c, flow);
            }
        }

        public void Eye(Vector2 c0, float width, float rad, Color c)
        {
            Arc(c0 + new Vector2(0, -width * 0.12f), width * 0.55f, 0.35f, Mathf.PI - 0.35f, rad, c, 0.8f, 0.55f);
            Arc(c0 + new Vector2(0, width * 0.12f), width * 0.55f, Mathf.PI + 0.35f, Mathf.PI * 2f - 0.35f, rad, c, 0.8f, 0.55f);
            Circle(c0, width * 0.14f, rad, c);
            Stamp(c0.x, c0.y, width * 0.11f, c, 0.95f);
        }

        // Figura de palitos: cabeza, tronco, brazos y piernas torcidos
        public void Figure(Vector2 foot, float height, float rad, Color c, bool noFace = false)
        {
            Vector2 hip = foot + new Vector2(R(-2, 2), height * 0.42f);
            Vector2 neck = foot + new Vector2(R(-3, 3), height * 0.78f);
            Vector2 head = neck + new Vector2(0, height * 0.12f);
            Line(foot + new Vector2(-height * 0.1f, 0), hip, rad, c);
            Line(foot + new Vector2(height * 0.1f, 0), hip, rad, c);
            Line(hip, neck, rad, c);
            Line(neck - new Vector2(0, 2), neck + new Vector2(-height * 0.22f, -height * 0.18f), rad, c);
            Line(neck - new Vector2(0, 2), neck + new Vector2(height * 0.22f, -height * 0.18f), rad, c);
            Circle(head, height * 0.1f, rad, c);
            if (!noFace)
            {
                Stamp(head.x - height * 0.035f, head.y + height * 0.02f, rad * 0.7f, c, 1f);
                Stamp(head.x + height * 0.035f, head.y + height * 0.02f, rad * 0.7f, c, 1f);
            }
        }

        // Texto con letras 5x7 desiguales: tamaño, giro y línea base cambian en cada letra
        public void Text(string text, Vector2 origin, float cell, Color c, float rad, float chaos = 1f, float slant = 0f)
        {
            float x = origin.x;
            string t = text.ToUpper();
            for (int i = 0; i < t.Length; i++)
            {
                string[] g = BuildKit.Glyph(t[i]);
                float s = cell * R(0.9f, 1.12f);
                float rot = R(-0.14f, 0.14f) * chaos;
                float baseY = origin.y + R(-cell * 0.4f, cell * 0.4f) * chaos;
                Vector2 center = new Vector2(x + 2.5f * s, baseY + 3.5f * s);
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                    {
                        if (g[gy][gx] != '#') continue;
                        Vector2 p = new Vector2(x + gx * s, baseY + (6 - gy) * s);
                        p += new Vector2((6 - gy) * slant * s, 0) + new Vector2(R(-0.2f, 0.2f), R(-0.2f, 0.2f)) * s * chaos;
                        Vector2 rel = p - center;
                        p = center + new Vector2(rel.x * Mathf.Cos(rot) - rel.y * Mathf.Sin(rot), rel.x * Mathf.Sin(rot) + rel.y * Mathf.Cos(rot));
                        float rr = Mathf.Max(rad, s * 0.42f);   // el pincel nunca es más fino que la letra: sin puntos sueltos
                        Stamp(p.x, p.y, rr, c, 0.92f);
                        // el trazo se une a los vecinos: se lee como escritura continua
                        if (gx < 4 && g[gy][gx + 1] == '#') Stamp(p.x + s * 0.5f, p.y, rr * 0.92f, c, 0.85f);
                        if (gy > 0 && g[gy - 1][gx] == '#') Stamp(p.x, p.y + s * 0.5f, rr * 0.92f, c, 0.85f);
                        if (gx < 4 && gy > 0 && g[gy - 1][gx + 1] == '#' && g[gy][gx + 1] != '#' && g[gy - 1][gx] != '#') Stamp(p.x + s * 0.5f, p.y + s * 0.5f, rr * 0.9f, c, 0.85f);
                        if (gx > 0 && gy > 0 && g[gy - 1][gx - 1] == '#' && g[gy][gx - 1] != '#' && g[gy - 1][gx] != '#') Stamp(p.x - s * 0.5f, p.y + s * 0.5f, rr * 0.9f, c, 0.85f);
                    }
                x += 6f * s;
            }
        }

        // Papel viejo: fibra, manchas de humedad, borde oscurecido, doblez y mancha de taza
        public void Paper(Color baseColor)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float n = Mathf.PerlinNoise(seed + x * 0.012f, y * 0.012f) * 0.55f + Mathf.PerlinNoise(x * 0.07f, seed + y * 0.07f) * 0.3f + R() * 0.15f;
                    float edge = Mathf.Min(Mathf.Min(x, w - 1 - x), Mathf.Min(y, h - 1 - y)) / (w * 0.12f);
                    float k = 0.82f + n * 0.28f - Mathf.Clamp01(1f - edge) * 0.22f;
                    px[y * w + x] = new Color(baseColor.r * k, baseColor.g * k, baseColor.b * k * 0.97f, 1f);
                }
            int foldY = (int)(h * R(0.45f, 0.55f));
            for (int x = 0; x < w; x++) { Blend(x, foldY, new Color(0.25f, 0.2f, 0.12f), 0.28f); Blend(x, foldY + 1, Color.white, 0.12f); }
            Vector2 ring = new Vector2(R(0.2f, 0.8f) * w, R(0.15f, 0.85f) * h);
            float rr = w * R(0.09f, 0.14f);
            Arc(ring, rr, 0.4f, 5.6f, 2.5f, new Color(0.35f, 0.22f, 0.1f), 1.2f);
            Fill(ring, rr * 0.9f, new Color(0.4f, 0.28f, 0.14f), 0.05f);
        }

        public Texture2D ToTexture()
        {
            Texture2D t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.SetPixels(px);
            t.Apply();
            return t;
        }
    }

    static Texture2D Save(Canvas cv, string name)
    {
        BuildKit.EnsureFolder("Assets/_Project", "Art");
        BuildKit.EnsureFolder("Assets/_Project/Art", "Textures");
        string path = Dir + name + ".png";
        Texture2D t = cv.ToTexture();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.mipmapEnabled = true;
        imp.maxTextureSize = 2048;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static readonly Color Charcoal = new Color(0.07f, 0.06f, 0.06f);
    static readonly Color Pencil = new Color(0.22f, 0.21f, 0.22f);
    static readonly Color Wax = new Color(0.62f, 0.07f, 0.05f);
    static readonly Color Blue = new Color(0.12f, 0.16f, 0.34f);

    // ---------------------------------------------------------------- Rótulos pintados a mano (blanco sobre transparente; el color lo da el material)

    public static Texture2D SignTexture(string text, int drip)
    {
        const int cell = 22;
        string label = text.ToUpper();
        int w = label.Length * 6 * cell + cell * 2, h = (7 + 3 + drip * 2) * cell;
        Canvas c = new Canvas(w, h, new Color(1, 1, 1, 0), label.GetHashCode() & 0xffff);
        c.Text(label, new Vector2(cell, (drip * 2 + 2) * cell), cell, Color.white, cell * 0.5f, 0.5f, 0.06f);
        for (int i = 0; i < drip * 3; i++)   // goteos de pintura bajo las letras
        {
            float x = c.R(cell, w - cell), y0 = (drip * 2 + 2) * cell + c.R(0, 3) * cell;
            c.Line(new Vector2(x, y0), new Vector2(x + c.R(-3, 3), y0 - c.R(1, drip * 2) * cell), cell * 0.16f, Color.white, 0.8f, 0.9f);
        }
        string path = Dir + "L3_Sign_" + System.Text.RegularExpressions.Regex.Replace(label, "[^A-Z0-9]", "_") + ".png";
        BuildKit.EnsureFolder("Assets/_Project/Art", "Textures");
        Texture2D t = c.ToTexture();
        System.IO.File.WriteAllBytes(path, t.EncodeToPNG());
        Object.DestroyImmediate(t);
        AssetDatabase.ImportAsset(path);
        TextureImporter imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.alphaIsTransparency = true;
        imp.maxTextureSize = 4096;
        imp.npotScale = TextureImporterNPOTScale.None;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ---------------------------------------------------------------- Hojas del paciente 214 (papel 512x704)

    public static readonly string[] SheetNames = { "TwoFigures", "Spiral", "DoorS9", "RitualCircle", "Faces", "Tally0307", "Eyes", "Corridor" };

    public static Texture2D Sheet(string kind)
    {
        int seed = kind.GetHashCode() & 0xffff;
        Canvas c = new Canvas(512, 704, new Color(0.78f, 0.7f, 0.55f), seed);
        c.Paper(new Color(0.78f, 0.7f, 0.55f));
        Vector2 mid = new Vector2(256, 352);
        switch (kind)
        {
            case "TwoFigures":   // dos figuras idénticas; la de la derecha, tachada en rojo
                c.Figure(new Vector2(170, 200), 300, 3.2f, Pencil);
                c.Figure(new Vector2(340, 200), 300, 3.2f, Pencil);
                c.Line(new Vector2(290, 220), new Vector2(395, 520), 4f, Wax, 3f);
                c.Line(new Vector2(395, 220), new Vector2(290, 520), 4f, Wax, 3f);
                c.Text("CUAL", new Vector2(150, 90), 10f, Pencil, 2.4f, 1.3f);
                break;
            case "Spiral":
                c.Spiral(mid, 6, 210, 7.5f, 2.6f, Charcoal);
                c.Eye(mid, 70, 2.8f, Wax);
                break;
            case "DoorS9":       // la puerta del sótano profundo, dibujada una y otra vez
                c.Poly(new[] { new Vector2(150, 140), new Vector2(150, 560), new Vector2(360, 560), new Vector2(360, 140), new Vector2(150, 140) }, 3.6f, Pencil, 2f);
                c.Poly(new[] { new Vector2(170, 160), new Vector2(170, 540), new Vector2(340, 540), new Vector2(340, 160), new Vector2(170, 160) }, 2.4f, Pencil, 2f);
                c.Circle(new Vector2(322, 350), 9, 3f, Charcoal);
                c.Text("S-9", new Vector2(170, 590), 14f, Charcoal, 3f, 0.8f);
                for (int i = 0; i < 5; i++) c.Line(new Vector2(150 + i * 14, 100), new Vector2(150 + i * 14, 55), 2.4f, Pencil, 1f);
                c.Line(new Vector2(148, 78), new Vector2(210, 78), 2.4f, Pencil, 1f);
                c.Figure(new Vector2(430, 140), 120, 2.4f, Pencil, true);
                break;
            case "RitualCircle": // círculo doble con marcas; no explica nada
                c.Circle(mid, 190, 3f, Charcoal);
                c.Circle(mid, 162, 2.4f, Charcoal);
                for (int i = 0; i < 12; i++)
                {
                    float a = i * Mathf.PI / 6f;
                    c.Line(mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 166, mid + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 186, 2.4f, Charcoal, 1f);
                }
                c.Figure(mid + new Vector2(0, -60), 130, 3f, Wax, true);
                c.Text("UNO", new Vector2(40, 40), 8f, Pencil, 2f, 1.4f);
                break;
            case "Faces":        // una cuadrícula de caras idénticas; una de ellas, distinta
                for (int gy = 0; gy < 5; gy++)
                    for (int gx = 0; gx < 4; gx++)
                    {
                        Vector2 p = new Vector2(100 + gx * 100, 110 + gy * 120);
                        c.Circle(p, 36, 2.4f, Pencil);
                        bool odd = gx == 2 && gy == 3;
                        c.Stamp(p.x - 12, p.y + 8, odd ? 6f : 3f, Charcoal, 1f);
                        c.Stamp(p.x + 12, p.y + 8, 3f, Charcoal, 1f);
                        if (odd) { c.Arc(p + new Vector2(0, -6), 22, Mathf.PI + 0.3f, Mathf.PI * 2f - 0.3f, 2.4f, Charcoal, 0.4f, 0.8f); c.Circle(p, 50, 3.2f, Wax); }
                        else c.Line(p + new Vector2(-14, -14), p + new Vector2(14, -14), 2.2f, Pencil, 0.6f);
                    }
                break;
            case "Tally0307":    // recuento de noches y la hora, escrita y tachada una y otra vez
                for (int g = 0; g < 6; g++)
                {
                    Vector2 o = new Vector2(70 + (g % 3) * 150, 520 - (g / 3) * 120);
                    for (int i = 0; i < 4; i++) c.Line(o + new Vector2(i * 14, 0), o + new Vector2(i * 14, 70), 2.6f, Charcoal, 1f);
                    c.Line(o + new Vector2(-8, 60), o + new Vector2(62, 12), 2.6f, Charcoal, 1f);
                }
                c.Text("03:07", new Vector2(70, 250), 14f, Wax, 3f, 1f);
                c.Text("03:07", new Vector2(70, 160), 14f, Pencil, 2.6f, 1.2f);
                c.Text("03:07", new Vector2(70, 70), 14f, Charcoal, 2.6f, 1.4f);
                break;
            case "Eyes":
                for (int gy = 0; gy < 6; gy++)
                    for (int gx = 0; gx < 3; gx++)
                        c.Eye(new Vector2(110 + gx * 150 + c.R(-8, 8), 100 + gy * 105), 90, 2.4f, gx == 1 && gy == 2 ? Wax : Pencil);
                break;
            default:             // un pasillo en perspectiva con una figura al fondo
                c.Line(new Vector2(60, 60), new Vector2(210, 330), 3f, Pencil, 2f);
                c.Line(new Vector2(450, 60), new Vector2(300, 330), 3f, Pencil, 2f);
                c.Poly(new[] { new Vector2(210, 330), new Vector2(210, 500), new Vector2(300, 500), new Vector2(300, 330), new Vector2(210, 330) }, 3f, Pencil, 1.5f);
                c.Figure(new Vector2(255, 335), 130, 2.4f, Charcoal, true);
                c.Text("DETRAS", new Vector2(100, 600), 11f, Wax, 2.6f, 1.2f);
                break;
        }
        return Save(c, "L3_Drawing_" + kind);
    }

    // ---------------------------------------------------------------- Garabatos grandes de pared (fondo transparente 1024x1024)

    public static Texture2D WallScrawl(string kind)
    {
        int seed = (kind.GetHashCode() & 0xffff) + 7;
        bool wideText = kind == "NoEsElMismo" || kind == "NoFueElTecho" || kind == "NoDesdeEsteLado";   // los textos usan un lienzo apaisado 2048x700
        Canvas c = new Canvas(wideText ? 2048 : 1024, wideText ? 700 : 1024, new Color(0, 0, 0, 0), seed);
        Vector2 mid = new Vector2(512, 512);
        switch (kind)
        {
            case "Spiral":
                c.Spiral(mid, 10, 440, 9f, 6f, Charcoal);
                c.Eye(mid, 180, 7f, Wax);
                break;
            case "Tally":
                for (int g = 0; g < 9; g++)
                {
                    Vector2 o = new Vector2(110 + (g % 3) * 300, 800 - (g / 3) * 250);
                    for (int i = 0; i < 4; i++) c.Line(o + new Vector2(i * 36, 0), o + new Vector2(i * 36, 170), 6f, Charcoal, 2.4f);
                    c.Line(o + new Vector2(-20, 150), o + new Vector2(150, 20), 6f, Charcoal, 2.4f);
                }
                break;
            case "NoEsElMismo":
                c.Text("NO ES", new Vector2(70, 410), 60f, Wax, 8f, 1.2f, 0.1f);
                c.Text("EL MISMO", new Vector2(70, 70), 40f, Wax, 8f, 1.2f, 0.1f);
                c.Line(new Vector2(50, 40), new Vector2(1980, 60), 7f, Wax, 6f);
                break;
            case "NoFueElTecho":
                c.Text("NO FUE", new Vector2(70, 410), 60f, Charcoal, 8f, 1.3f);
                c.Text("EL TECHO", new Vector2(70, 70), 40f, Charcoal, 8f, 1.3f);
                break;
            case "NoDesdeEsteLado":
                c.Text("NO LA ABRAS", new Vector2(40, 410), 30f, Wax, 6.5f, 1.2f);
                c.Text("DESDE ESTE LADO", new Vector2(40, 90), 21f, Wax, 5.5f, 1.2f);
                break;
            case "Hands":        // manos abiertas contra la pared
                for (int i = 0; i < 14; i++)
                {
                    Vector2 p = new Vector2(c.R(140, 880), c.R(150, 820));
                    c.Fill(p, 34, new Color(0.35f, 0.04f, 0.03f), 0.06f);
                    for (int f = 0; f < 5; f++) c.Line(p, p + new Vector2(Mathf.Lerp(-50, 50, f / 4f), 70 + c.R(-12, 18)), 6f, new Color(0.32f, 0.04f, 0.03f), 2f, 0.7f);
                }
                break;
            default:
                c.Text(kind, new Vector2(60, 500), 30f, Charcoal, 6f, 1.2f);
                break;
        }
        return Save(c, "L3_Scrawl_" + kind);
    }

    // Marca de suelo: doble círculo seco, marrón (la silla de la cámara de pruebas y el vestíbulo S-9)
    public static Texture2D FloorMark()
    {
        Canvas c = new Canvas(1024, 1024, new Color(0, 0, 0, 0), 31);
        Vector2 mid = new Vector2(512, 512);
        Color dried = new Color(0.22f, 0.07f, 0.04f);
        c.Circle(mid, 470, 9f, dried);
        c.Circle(mid, 420, 6f, dried);
        for (int i = 0; i < 24; i++)
        {
            float a = i * Mathf.PI / 12f;
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            c.Line(mid + dir * 425, mid + dir * 465, 5f, dried, 2f);
        }
        for (int i = 0; i < 10; i++) c.Fill(mid + Random.insideUnitCircle * 380, c.R(20, 50), dried, 0.05f);
        return Save(c, "L3_FloorMark");
    }

    // Mancha oscura irregular (humedad, suciedad y sangre vieja)
    public static Texture2D Stain()
    {
        Canvas c = new Canvas(256, 256, new Color(0, 0, 0, 0), 5);
        Vector2 mid = new Vector2(128, 128);
        for (int i = 0; i < 60; i++) c.Stamp(mid.x + c.R(-60, 60) * c.R(), mid.y + c.R(-60, 60) * c.R(), c.R(14, 40), new Color(0.05f, 0.03f, 0.02f), 0.12f);
        return Save(c, "L3_Stain");
    }
}
