using UnityEditor;
using UnityEngine;

// Convierte a URP/Lit los materiales de los packs que usan el shader Standard (se ven rosados/morados en URP).
// Conserva texturas, color, normal, metálico, oclusión y emisión. Solo toca materiales que no son URP.
public static class MaterialConverter
{
    static readonly string[] Folders =
    {
        "Assets/Abandoned_Asylum", "Assets/Dnk_Dev", "Assets/NomadicUV", "Assets/Survival Props",
    };

    [MenuItem("Don't Look Back/Convertir materiales de los packs a URP")]
    public static void ConvertAll()
    {
        int converted = 0;
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) { Debug.LogError("MaterialConverter: no encuentro el shader URP/Lit."); return; }

        foreach (string guid in AssetDatabase.FindAssets("t:Material", Folders))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null || !NeedsConversion(m)) continue;
            Convert(m, lit);
            EditorUtility.SetDirty(m);
            converted++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log("MaterialConverter: " + converted + " materiales convertidos a URP/Lit.");
    }

    static bool NeedsConversion(Material m)
    {
        string n = m.shader != null ? m.shader.name : "";
        return n == "" || n.Contains("InternalErrorShader") || n.Contains("2D") || n == "Standard" || n == "Standard (Specular setup)" || n.StartsWith("Legacy Shaders/");
    }

    static void Convert(Material m, Shader lit)
    {
        // Las propiedades antiguas se leen del YAML guardado porque el shader roto ya no las expone
        SerializedObject so = new SerializedObject(m);
        Texture main = Tex(so, "_MainTex"), bump = Tex(so, "_BumpMap"), metal = Tex(so, "_MetallicGlossMap");
        Texture occ = Tex(so, "_OcclusionMap"), emissionMap = Tex(so, "_EmissionMap");
        Vector2 tiling = TexScale(so, "_MainTex"), offset = TexOffset(so, "_MainTex");
        Color color = Col(so, "_Color", Color.white), emission = Col(so, "_EmissionColor", Color.black);
        float glossiness = Flo(so, "_Glossiness", 0.4f), metallic = Flo(so, "_Metallic", 0f);
        float mode = Flo(so, "_Mode", 0f), cutoff = Flo(so, "_Cutoff", 0.5f);
        float bumpScale = Flo(so, "_BumpScale", 1f);

        m.shader = lit;
        m.SetTexture("_BaseMap", main);
        m.SetTextureScale("_BaseMap", tiling);
        m.SetTextureOffset("_BaseMap", offset);
        m.SetColor("_BaseColor", color);
        m.SetFloat("_Smoothness", glossiness);
        m.SetFloat("_Metallic", metallic);

        if (bump != null) { m.SetTexture("_BumpMap", bump); m.SetFloat("_BumpScale", bumpScale); m.EnableKeyword("_NORMALMAP"); }
        if (metal != null) { m.SetTexture("_MetallicGlossMap", metal); m.EnableKeyword("_METALLICSPECGLOSSMAP"); }
        if (occ != null) { m.SetTexture("_OcclusionMap", occ); m.EnableKeyword("_OCCLUSIONMAP"); }
        if (emission.maxColorComponent > 0.01f || emissionMap != null)
        {
            m.SetColor("_EmissionColor", emission);
            if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
            m.EnableKeyword("_EMISSION");
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        }

        if (mode == 1f)   // Cutout
        {
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", cutoff);
            m.EnableKeyword("_ALPHATEST_ON");
            m.renderQueue = 2450;
        }
        else if (mode >= 2f)   // Fade / Transparent
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", 5f);
            m.SetFloat("_DstBlend", 10f);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = 3000;
        }
    }

    static SerializedProperty Find(SerializedObject so, string array, string name)
    {
        SerializedProperty list = so.FindProperty("m_SavedProperties." + array);
        if (list == null) return null;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty e = list.GetArrayElementAtIndex(i);
            if (e.FindPropertyRelative("first").stringValue == name) return e.FindPropertyRelative("second");
        }
        return null;
    }

    static Texture Tex(SerializedObject so, string name)
    {
        SerializedProperty p = Find(so, "m_TexEnvs", name);
        return p == null ? null : p.FindPropertyRelative("m_Texture").objectReferenceValue as Texture;
    }

    static Vector2 TexScale(SerializedObject so, string name)
    {
        SerializedProperty p = Find(so, "m_TexEnvs", name);
        return p == null ? Vector2.one : p.FindPropertyRelative("m_Scale").vector2Value;
    }

    static Vector2 TexOffset(SerializedObject so, string name)
    {
        SerializedProperty p = Find(so, "m_TexEnvs", name);
        return p == null ? Vector2.zero : p.FindPropertyRelative("m_Offset").vector2Value;
    }

    static Color Col(SerializedObject so, string name, Color fallback)
    {
        SerializedProperty p = Find(so, "m_Colors", name);
        return p == null ? fallback : p.colorValue;
    }

    static float Flo(SerializedObject so, string name, float fallback)
    {
        SerializedProperty p = Find(so, "m_Floats", name);
        return p == null ? fallback : p.floatValue;
    }
}
