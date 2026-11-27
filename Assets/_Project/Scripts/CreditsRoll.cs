using UnityEngine;

// Créditos que suben por la pantalla. Los usan el menú principal y la escena final.
// Para cambiar el contenido se edita solo este archivo (Team y Lines).
public static class CreditsRoll
{
    // Nombres del equipo, uno por entrada. Si está vacío no se muestra el apartado.
    static readonly string[] Team = { "Mariana López", "Andrés Restrepo" };

    enum Kind { Title, Heading, Name, Gap }

    struct Line
    {
        public string text; public Kind kind;
        public Line(string t, Kind k) { text = t; kind = k; }
    }

    static Line[] lines;
    static GUIStyle title, heading, name;

    static void Build()
    {
        if (lines != null) return;
        var l = new System.Collections.Generic.List<Line>
        {
            new Line("DON'T LOOK BACK", Kind.Title),
            new Line("Terror psicológico en primera persona", Kind.Name),
            new Line("", Kind.Gap),
        };
        if (Team.Length > 0)
        {
            l.Add(new Line("EQUIPO DE DESARROLLO", Kind.Heading));
            foreach (string n in Team) l.Add(new Line(n, Kind.Name));
            l.Add(new Line("", Kind.Gap));
        }
        l.Add(new Line("AMBIENTE Y MODELOS", Kind.Heading));
        foreach (string n in new[] { "Abandoned Asylum", "Hospital Horror Pack (Dnk_Dev)", "Functional Horror Props", "NomadicUV", "Survival Props", "Demon Horror Creature with Weapon" })
            l.Add(new Line(n, Kind.Name));
        l.Add(new Line("", Kind.Gap));
        l.Add(new Line("SONIDO", Kind.Heading));
        foreach (string n in new[] { "Respiración: cryptologymedia", "Atmósfera: jorisvermeer", "Scary Screamer Sound Effect" })
            l.Add(new Line(n, Kind.Name));
        l.Add(new Line("", Kind.Gap));
        l.Add(new Line("Gracias por jugar", Kind.Name));
        l.Add(new Line("No mires atrás.", Kind.Heading));
        lines = l.ToArray();
    }

    static void BuildStyles()
    {
        if (title != null) return;
        title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        heading = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        name = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
    }

    static float Height(Kind k, float s)
    {
        switch (k)
        {
            case Kind.Title: return 110f * s;
            case Kind.Heading: return 70f * s;
            case Kind.Name: return 46f * s;
            default: return 70f * s;
        }
    }

    // Alto total del texto: sirve para saber cuánto dura el desplazamiento
    public static float TotalHeight(float s)
    {
        Build();
        float h = 0f;
        foreach (Line ln in lines) h += Height(ln.kind, s);
        return h;
    }

    // Dibuja los créditos dentro de 'area'. scroll = píxeles desplazados; 0 empieza con el texto justo debajo del área.
    public static void Draw(Rect area, float scroll, float s, float alpha)
    {
        Build();
        BuildStyles();
        GUI.BeginClip(area);
        float y = area.height - scroll;
        foreach (Line ln in lines)
        {
            float h = Height(ln.kind, s);
            if (y + h > 0f && y < area.height && ln.kind != Kind.Gap)
            {
                GUIStyle st; Color c;
                switch (ln.kind)
                {
                    case Kind.Title: st = title; st.fontSize = Mathf.RoundToInt(64f * s); c = new Color(0.86f, 0.82f, 0.72f); break;
                    case Kind.Heading: st = heading; st.fontSize = Mathf.RoundToInt(26f * s); c = new Color(0.95f, 0.78f, 0.45f); break;
                    default: st = name; st.fontSize = Mathf.RoundToInt(28f * s); c = new Color(0.88f, 0.87f, 0.83f); break;
                }
                // Se desvanece al acercarse a los bordes superior e inferior del área
                float edge = Mathf.Clamp01(Mathf.Min(y, area.height - (y + h)) / (area.height * 0.18f));
                st.normal.textColor = new Color(c.r, c.g, c.b, alpha * edge);
                HudOverlay.ShadowLabel(new Rect(0, y, area.width, h), ln.text, st);
            }
            y += h;
        }
        GUI.EndClip();
    }
}
