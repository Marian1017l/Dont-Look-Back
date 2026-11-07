#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace FunctionalHorrorProps
{
    // Regenerates the pack's materials for the project's active render pipeline
    // (Built-in / URP / HDRP). Paired with PipelineAutoFix, which calls this on import
    // so the materials "just work" in all three pipelines.
    public static class CrossPipelinePackBuild
    {
        const string Root = "Assets/FunctionalHorrorProps";

        [Serializable] class Manifest { public Asset[] assets; }
        [Serializable] class Asset { public string stem; public Mat[] materials; }
        [Serializable] class Mat { public string name, albedo, normal, unityMetallic, alphaMode; public float[] color, emission, uvScale, uvOffset; public float metallic, roughness, alphaCutoff, normalStrength; }

        public enum Pipe { BuiltIn, URP, HDRP }

        public static Pipe Detect()
        {
            var rp = GraphicsSettings.currentRenderPipeline ?? GraphicsSettings.defaultRenderPipeline;
            if (rp == null) return Pipe.BuiltIn;
            string n = rp.GetType().FullName ?? "";
            if (n.Contains("Universal")) return Pipe.URP;
            if (n.Contains("HighDefinition") || n.Contains("HDRenderPipeline")) return Pipe.HDRP;
            return Pipe.BuiltIn;
        }

        // Called by PipelineAutoFix when the package is opened in a different pipeline than it was built for.
        public static void RebuildMaterialsForCurrentPipeline()
        {
            if (!File.Exists(Root + "/manifest.json")) return;
            Pipe pipe = Detect();
            string sp = Root + "/pipeline.sentinel";
            if (File.Exists(sp) && File.ReadAllText(sp).Trim() == pipe.ToString()) return;
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Root + "/manifest.json"));
            int n = 0;
            foreach (var a in manifest.assets)
                foreach (var m in a.materials)
                    { try { MaterialFor(a, m, pipe); n++; } catch (Exception e) { Debug.LogWarning("[Functional Horror Props] remap " + a.stem + ": " + e.Message); } }
            File.WriteAllText(sp, pipe.ToString());
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            Debug.Log("[Functional Horror Props] remapped " + n + " materials to " + pipe);
        }

        static Material MaterialFor(Asset a, Mat m, Pipe pipe)
        {
            string path = Root + "/Materials/" + Sanitize(a.stem + "_" + m.name) + ".mat";
            var alb = Tex(m.albedo); var nrm = Tex(m.normal, true); var mr = Tex(m.unityMetallic, false, true);
            var color = new Color(m.color[0], m.color[1], m.color[2], m.color[3]);
            var emis = (m.emission != null && m.emission.Any(x => x > 0)) ? new Color(m.emission[0], m.emission[1], m.emission[2]) : (Color?)null;
            bool blend = m.alphaMode == "BLEND", mask = m.alphaMode == "MASK";
            Material mat;
            if (pipe == Pipe.URP)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                mat.SetColor("_BaseColor", color); if (alb) mat.SetTexture("_BaseMap", alb);
                mat.SetTextureScale("_BaseMap", new Vector2(m.uvScale[0], m.uvScale[1])); mat.SetTextureOffset("_BaseMap", new Vector2(m.uvOffset[0], m.uvOffset[1]));
                mat.SetFloat("_Metallic", m.metallic); mat.SetFloat("_Smoothness", 1 - m.roughness);
                if (mr) { mat.SetTexture("_MetallicGlossMap", mr); mat.EnableKeyword("_METALLICSPECGLOSSMAP"); mat.SetFloat("_Smoothness", 1); }
                if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.SetFloat("_BumpScale", m.normalStrength); mat.EnableKeyword("_NORMALMAP"); }
                if (emis.HasValue) { mat.SetColor("_EmissionColor", emis.Value); mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                if (blend) { mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0); mat.SetFloat("_SrcBlend", (int)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetFloat("_ZWrite", 0); mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue = 3000; }
                else if (mask) { mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", m.alphaCutoff); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450; }
            }
            else if (pipe == Pipe.HDRP)
            {
                mat = new Material(Shader.Find("HDRP/Lit"));
                mat.SetColor("_BaseColor", color); if (alb) mat.SetTexture("_BaseColorMap", alb);
                mat.SetTextureScale("_BaseColorMap", new Vector2(m.uvScale[0], m.uvScale[1])); mat.SetTextureOffset("_BaseColorMap", new Vector2(m.uvOffset[0], m.uvOffset[1]));
                mat.SetFloat("_Metallic", m.metallic); mat.SetFloat("_Smoothness", 1 - m.roughness);
                if (mr) { mat.SetTexture("_MaskMap", mr); mat.SetFloat("_SmoothnessRemapMax", 1); }
                if (nrm) { mat.SetTexture("_NormalMap", nrm); mat.SetFloat("_NormalScale", m.normalStrength); mat.EnableKeyword("_NORMALMAP"); }
                if (emis.HasValue) { mat.SetColor("_EmissiveColor", emis.Value); mat.SetFloat("_UseEmissiveIntensity", 0); mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                if (blend) { mat.SetFloat("_SurfaceType", 1); mat.SetFloat("_BlendMode", 0); mat.SetFloat("_ZWrite", 0); mat.renderQueue = 3000; }
                else if (mask) { mat.SetFloat("_AlphaCutoffEnable", 1); mat.SetFloat("_AlphaCutoff", m.alphaCutoff); mat.renderQueue = 2450; }
            }
            else
            {
                mat = new Material(Shader.Find("Standard"));
                mat.color = color; if (alb) mat.mainTexture = alb;
                mat.mainTextureScale = new Vector2(m.uvScale[0], m.uvScale[1]); mat.mainTextureOffset = new Vector2(m.uvOffset[0], m.uvOffset[1]);
                mat.SetFloat("_Metallic", m.metallic); mat.SetFloat("_Glossiness", 1 - m.roughness);
                if (mr) { mat.SetTexture("_MetallicGlossMap", mr); mat.SetFloat("_GlossMapScale", 1); mat.EnableKeyword("_METALLICGLOSSMAP"); }
                if (nrm) { mat.SetTexture("_BumpMap", nrm); mat.SetFloat("_BumpScale", m.normalStrength); mat.EnableKeyword("_NORMALMAP"); }
                if (emis.HasValue) { mat.SetColor("_EmissionColor", emis.Value); mat.EnableKeyword("_EMISSION"); mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive; }
                if (blend) { mat.SetFloat("_Mode", 3); mat.SetOverrideTag("RenderType", "Transparent"); mat.SetInt("_SrcBlend", (int)BlendMode.One); mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha); mat.SetInt("_ZWrite", 0); mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); mat.renderQueue = 3000; }
                else if (mask) { mat.SetFloat("_Mode", 1); mat.SetFloat("_Cutoff", m.alphaCutoff); mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue = 2450; }
            }
            mat.name = Sanitize(a.stem + "_" + m.name);
            Material result;
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing) { existing.shader = mat.shader; existing.CopyPropertiesFromMaterial(mat); result = existing; }
            else { AssetDatabase.CreateAsset(mat, path); result = mat; }
            if (pipe == Pipe.HDRP) HdrpValidate(result);
            EditorUtility.SetDirty(result); return result;
        }

        static void HdrpValidate(Material mat)
        {
            try
            {
                var types = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => { try { return x.GetTypes(); } catch { return new Type[0]; } }).ToArray();
                var hdm = types.FirstOrDefault(x => x.Name == "HDMaterial" && x.Namespace != null && x.Namespace.Contains("HighDefinition"));
                var vm = hdm?.GetMethod("ValidateMaterial", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(Material) }, null);
                if (vm != null) { vm.Invoke(null, new object[] { mat }); return; }
                var hsu = types.FirstOrDefault(x => x.Name == "HDShaderUtils" && x.Namespace != null && x.Namespace.Contains("HighDefinition"));
                hsu?.GetMethod("ResetMaterialKeywords", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, new object[] { mat });
            }
            catch { }
        }

        static string Sanitize(string s) { foreach (char c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_'); return s.Replace('/', '_').Replace(' ', '_'); }

        static Texture2D Tex(string n, bool normal = false, bool data = false)
        {
            if (string.IsNullOrEmpty(n)) return null;
            string path = Root + "/Textures/" + n; var im = (TextureImporter)AssetImporter.GetAtPath(path); if (!im) return null;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default; bool srgb = !normal && !data;
            if (im.textureType != type || im.sRGBTexture != srgb) { im.textureType = type; im.sRGBTexture = srgb; im.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
#endif
