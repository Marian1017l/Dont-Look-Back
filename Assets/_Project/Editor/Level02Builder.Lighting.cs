using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static BuildKit;

// Level02Builder, parte 4: iluminación sala por sala.
// Cada espacio tiene sus propias fuentes justificadas (fluorescentes fríos, lámparas industriales, luces de emergencia ámbar y rojas).
// Las luces "restaurables" (pasillo de aislamiento, sala eléctrica, archivo...) están apagadas hasta colocar el fusible:
// hay una versión apagada del modelo (sin emisión) que se oculta y una encendida que se activa. Ver WirePower.
public static partial class Level02Builder
{
    static readonly Color Cold = new Color(0.62f, 0.80f, 1.0f);
    static readonly Color Teal = new Color(0.45f, 0.85f, 0.80f);
    static readonly Color Amber = new Color(1.0f, 0.60f, 0.24f);
    static readonly Color WarmLamp = new Color(1.0f, 0.74f, 0.46f);
    static readonly Color Alarm = new Color(1.0f, 0.12f, 0.06f);
    static readonly Color Indicator = new Color(0.3f, 1.0f, 0.5f);
    static readonly Color Sick = new Color(0.62f, 0.95f, 0.78f);

    static Transform lampsOn, lampsOff;

    static string LightsName(char c)
    {
        switch (c)
        {
            case 'A': return "Stairs_Lights";
            case 'T': return "Transit_Lights";
            case 'B': return "Maintenance_Lights";
            case 'W': return "Storage_Lights";
            case 'C': return "Electrical_Lights";
            case 'E': return "Archive_Lights";
            case 'G': return "Final_Area_Lights";
            case 'f': case 'g': case 'n': return "Corridor_Lights";
            default: return "Isolation_Lights";
        }
    }

    static Transform Lights(char c) => Lazy(LightsName(c), lightingRoot);

    static void BuildLighting()
    {
        lampsOn = T("Powered_Lights_On", lightingRoot);
        lampsOff = T("Powered_Lights_Off", lightingRoot);

        // Los objetos del pack traen luces propias: se apagan, cada espacio usa las suyas (salvo la bobina, que se ajusta abajo)
        foreach (Light l in propsRoot.GetComponentsInChildren<Light>(true)) l.enabled = false;

        string neon = AS + "NeonLamp.prefab", cage = FH + "hanging_cage_lamp.prefab", small = AS + "SingleLight.prefab", wall = AS + "LampWall.prefab", oil = FH + "oil_table_lamp.prefab";

        // --- A: escaleras. Fría y limpia arriba (recuerdo del piso clínico), cada vez más escasa al bajar
        Transform a = Lights('A');
        Fixture(cage, "Stairs_Top", a, new Vector3(2f, 0, 30.5f), 6.4f, true, 0, new Color(0.85f, 0.92f, 1f), 20f, 11f);
        Fixture(wall, "Stairs_Landing", a, new Vector3(0.3f, 0, 25f), 3.0f, false, 90f, Amber, 8f, 7f, false, true);
        Accent("Stairs_Fill", a, new Vector3(2f, 0, 27.5f), 4.6f, Cold, 3f, 6f);
        Fixture(small, "Stairs_Bottom_Emergency", a, new Vector3(3.65f, 0, 21f), 2.3f, false, 0, Alarm, 3.0f, 6f);

        // --- T: tránsito
        Transform t = Lights('T');
        Fixture(neon, "Transit", t, new Vector3(2f, 0, 17f), WallH, true, 0, Cold, 11f, 8f, false, true);
        Fixture(small, "Transit_Emergency", t, new Vector3(3.65f, 0, 16.3f), 2.5f, false, 0, Alarm, 2.5f, 5f);

        // --- B: mantenimiento. Fluorescente frío, uno muerto, lámpara de banco cálida (con sombras) y un rincón ámbar
        Transform b = Lights('B');
        Fixture(neon, "Maintenance_West", b, new Vector3(7f, 0, 16.5f), WallH, true, 0, Cold, 12f, 9f);
        Fixture(neon, "Maintenance_East", b, new Vector3(13f, 0, 16.5f), WallH, true, 0, Cold, 0f, 0f, false, false, false);
        Fixture(cage, "Maintenance_Corner", b, new Vector3(6f, 0, 8.5f), WallH, true, 0, Amber, 10f, 7f, false, true);
        if (deskMaint2 != null)
        {
            Vector3 top = TopOf(deskMaint2);
            Fixture(oil, "Maintenance_Bench", b, new Vector3(top.x - 0.45f, 0, top.z), top.y, false, 0, WarmLamp, 5f, 5.5f, true);
        }
        Accent("Maintenance_Fuse_LED", b, new Vector3(15.6f, 0, 19f), 1.5f, Alarm, 1.3f, 2.6f);

        // --- Pasillo de servicio 1
        Transform f = Lights('f');
        Fixture(neon, "Service1_Neon", f, new Vector3(20f, 0, 17f), WallH, true, 0, Cold, 9f, 7f, false, true);
        Accent("Service1_Stub", f, new Vector3(21f, 0, 15.3f), 2.3f, Amber, 5f, 5f);
        Fixture(small, "Service1_Emergency", f, new Vector3(25.5f, 0, 17.6f), 2.4f, false, 0, Alarm, 3f, 5f);

        // --- W: almacén antiguo
        Transform w = Lights('W');
        Fixture(cage, "Storage", w, new Vector3(22f, 0, 11f), WallH, true, 0, Amber, 10f, 8f);
        Fixture(neon, "Storage_Dead", w, new Vector3(22f, 0, 9.4f), WallH, true, 0, Cold, 0f, 0f, false, false, false);

        // --- C: sala eléctrica. Sin energía: lámparas de advertencia ámbar e indicadores; con energía: fluorescentes fríos
        Transform c = Lights('C');
        foreach (float x in new[] { 28.5f, 32.5f, 36f }) Fixture(wall, "Electrical_Warning_" + x, c, new Vector3(x, 0, 21.75f), 2.6f, false, 180f, Amber, 9f, 8f);
        Accent("Electrical_LED_Red_A", c, new Vector3(30.2f, 0, 21.4f), 1.5f, Alarm, 1.5f, 2.4f);
        Accent("Electrical_LED_Red_B", c, new Vector3(31.8f, 0, 21.4f), 1.2f, Alarm, 1.2f, 2.2f);
        Accent("Electrical_LED_Green", c, new Vector3(31f, 0, 21.4f), 2.1f, Indicator, 0.9f, 2f);
        Accent("Electrical_Fill", c, new Vector3(30f, 0, 17.5f), 2.2f, Cold, 3.4f, 8f);
        PoweredFixture(neon, "Electrical_A", c, new Vector3(28f, 0, 17.9f), WallH, true, 0, Cold, 12f, 10f, false);
        PoweredFixture(neon, "Electrical_B", c, new Vector3(32f, 0, 17.9f), WallH, true, 0, Cold, 12f, 10f, true);
        PoweredFixture(neon, "Electrical_C", c, new Vector3(36f, 0, 17.9f), WallH, true, 0, Cold, 12f, 10f, false);
        Light coil = null;
        foreach (Light l in PropsGroup('C').GetComponentsInChildren<Light>(true)) { coil = l; break; }
        if (coil != null) { coil.enabled = true; coil.type = LightType.Point; coil.color = Teal; coil.intensity = 2.2f; coil.range = 3.5f; coil.shadows = LightShadows.None; }

        // --- Pasillo de servicio 2
        Transform g = Lights('g');
        Fixture(neon, "Service2_Neon", g, new Vector3(31f, 0, 10.5f), WallH, true, 90f, Cold, 8f, 7f, false, true);
        Fixture(small, "Service2_Emergency", g, new Vector3(31.65f, 0, 7.3f), 2.4f, false, 0, Alarm, 3f, 5.5f);

        // --- Aislamiento: pasillo (restaurable) y celdas con una luz fría, tenue y localizada cada una
        Transform iso = Lights('h');
        Fixture(small, "Isolation_Entry_Emergency", iso, new Vector3(31f, 0, 5.4f), 2.5f, false, 0, Alarm, 2.4f, 5f);
        foreach (float x in new[] { 22.5f, 30.5f, 38.5f })
            PoweredFixture(neon, "Isolation_" + x, iso, new Vector3(x, 0, 5f), WallH, true, 0, Cold, 10f, 8f, x == 30.5f);
        PoweredFixture(neon, "Isolation_End", iso, new Vector3(44.5f, 0, 5f), WallH, true, 0, Cold, 9f, 7f, false);
        Accent("Isolation_DeadEnd", iso, new Vector3(19.6f, 0, 5f), 2.3f, Sick, 2.2f, 4f);
        Accent("Cell1_Light", iso, new Vector3(22f, 0, 3f), 2.6f, Cold, 3.5f, 4.6f);
        Accent("Cell2_Light", iso, new Vector3(26f, 0, 3f), 2.6f, Cold, 1.4f, 4.2f);
        Accent("Cell3_Light", iso, new Vector3(30f, 0, 3f), 2.6f, WarmLamp, 3.5f, 4.6f, true);
        Accent("Cell4_Light", iso, new Vector3(34f, 0, 3f), 2.6f, Cold, 6f, 5.2f);
        Accent("Cell5_Light", iso, new Vector3(38f, 0, 3f), 2.4f, Alarm, 1.8f, 4.2f);

        // --- Pasillo de servicio 3
        Transform n = Lights('n');
        Accent("Service3_Amber", n, new Vector3(45f, 0, 12.4f), 2.3f, Amber, 4.5f, 5.5f);
        PoweredFixture(neon, "Service3", n, new Vector3(45f, 0, 9.5f), WallH, true, 90f, Cold, 8f, 7f, false);

        // --- E: archivo. Lámpara cálida con sombras, una de escritorio y el fluorescente frío restaurable
        Transform e = Lights('E');
        Fixture(cage, "Archive", e, new Vector3(46f, 0, 18.5f), WallH, true, 0, WarmLamp, 9f, 9f, true);
        if (deskArchive != null)
        {
            Vector3 top = TopOf(deskArchive);
            Fixture(oil, "Archive_Desk", e, new Vector3(top.x, 0, top.z + 0.55f), top.y, false, 0, WarmLamp, 4f, 4.5f);
        }
        PoweredFixture(neon, "Archive", e, new Vector3(44f, 0, 17f), WallH, true, 0, Cold, 10f, 8f, true);
        Accent("Archive_Fill", e, new Vector3(43f, 0, 15.5f), 2.2f, Cold, 2f, 4.5f);

        // --- G: zona final. Una lámpara industrial alta, rojo sobre el montacargas y el pozo en penumbra
        Transform fin = Lights('G');
        Fixture(cage, "Final", fin, new Vector3(53f, 0, 18f), 2f * WallH, true, 0, new Color(0.8f, 0.9f, 1f), 24f, 11f);
        Fixture(small, "Final_Emergency", fin, new Vector3(52.2f, 0, 20.4f), 2.7f, false, 0, Alarm, 3.5f, 5f);
        Accent("Final_Entry", fin, new Vector3(50.8f, 0, 19f), 2.4f, Amber, 4f, 5f);
        Accent("Elevator_Cab", fin, new Vector3(54.5f, 0, 18f), 2.2f, Amber, 2f, 3.2f);

        WirePower();
        BuildLocalVolumes();
    }

    // Colocar el fusible: se ocultan las lámparas apagadas y se activan las encendidas
    static void WirePower()
    {
        lampsOn.gameObject.SetActive(false);
        if (fuseInteract == null) return;
        HookBool(fuseInteract.onInteract, lampsOn.gameObject.SetActive, true);
        HookBool(fuseInteract.onInteract, lampsOff.gameObject.SetActive, false);
    }

    // Luminaria con modelo: usa la Light del prefab (o crea una), la ajusta y la deja encendida o "muerta" (sin luz ni emisión)
    static GameObject Fixture(string prefab, string name, Transform parent, Vector3 pos, float y, bool top, float yaw, Color color, float intensity, float range, bool shadows = false, bool flicker = false, bool lit = true)
    {
        GameObject go = Spawn(prefab, parent);
        if (go == null) return null;
        go.name = "Lamp_" + name + (lit ? "" : "_Off");
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, y, top);

        Light[] lights = go.GetComponentsInChildren<Light>(true);
        Light main;
        if (lights.Length > 0) main = lights[0];
        else
        {
            main = new GameObject("Light_" + name).AddComponent<Light>();
            main.transform.SetParent(go.transform, true);
        }
        for (int i = 1; i < lights.Length; i++) lights[i].enabled = false;   // un solo foco por lámpara

        Bounds b = GetBounds(go);
        main.transform.position = top ? new Vector3(b.center.x, b.min.y - 0.3f, b.center.z) : new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        main.type = LightType.Point;
        main.color = color;
        main.intensity = intensity;
        main.range = range;
        main.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        main.enabled = lit;
        if (lit && flicker) main.gameObject.AddComponent<FlickerLight>().minFactor = 0.45f;
        if (!lit) SwitchOff(go);
        return go;
    }

    // Lámpara restaurable: versión apagada (visible al inicio) y encendida (se activa con el fusible)
    static void PoweredFixture(string prefab, string name, Transform zone, Vector3 pos, float y, bool top, float yaw, Color color, float intensity, float range, bool flicker)
    {
        Fixture(prefab, name, lampsOff, pos, y, top, yaw, color, intensity, range, false, false, false);
        Fixture(prefab, name, lampsOn, pos, y, top, yaw, color, intensity, range, false, flicker, true);
    }

    // Luz sin modelo (un punto de luz puro): rellenos y focos de acento
    static void Accent(string name, Transform parent, Vector3 pos, float y, Color color, float intensity, float range, bool flicker = false)
    {
        GameObject go = new GameObject("Light_" + name);
        go.transform.SetParent(parent, false);
        go.transform.position = new Vector3(pos.x, y, pos.z);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker) go.AddComponent<FlickerLight>().minFactor = 0.45f;
    }

    // Quita la emisión de los materiales de una lámpara apagada
    static void SwitchOff(GameObject go)
    {
        foreach (Renderer r in go.GetComponentsInChildren<Renderer>())
        {
            Material[] mats = r.sharedMaterials;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null || !mats[i].IsKeywordEnabled("_EMISSION")) continue;
                string key = "L2_Off_" + mats[i].name;
                if (!materials.TryGetValue(key, out Material off))
                {
                    off = new Material(mats[i]) { name = key };
                    off.SetColor("_EmissionColor", Color.black);
                    off.DisableKeyword("_EMISSION");
                    AssetDatabase.CreateAsset(off, MatDir + key + ".mat");
                    materials[key] = off;
                }
                mats[i] = off;
            }
            r.sharedMaterials = mats;
        }
    }

    // Zonas intencionalmente más oscuras: el fondo del pasillo de aislamiento y la cabina del montacargas
    static void BuildLocalVolumes()
    {
        VolumeProfile dark = NewProfile("Assets/_Project/Settings/Level02_DarkZone_Volume.asset");
        AddEffect<ColorAdjustments>(dark).postExposure.Override(-0.45f);
        Vignette v = AddEffect<Vignette>(dark);
        v.intensity.Override(0.45f);
        v.smoothness.Override(0.6f);
        EditorUtility.SetDirty(dark);

        Transform parent = Lazy("Local_Volumes", postRoot);
        LocalVolume("Volume_Isolation_DeadEnd", parent, new Vector3(23f, 1.6f, 5f), new Vector3(10f, 3.2f, 2f), dark);
        LocalVolume("Volume_Elevator_Shaft", parent, new Vector3(54.5f, 1.6f, 18f), new Vector3(3.2f, 3.2f, 3.4f), dark);
    }

    static void LocalVolume(string name, Transform parent, Vector3 center, Vector3 size, VolumeProfile profile)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.position = center;
        BoxCollider box = go.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = size;
        Volume vol = go.AddComponent<Volume>();
        vol.isGlobal = false;
        vol.blendDistance = 2.5f;
        vol.priority = 1f;
        vol.sharedProfile = profile;
    }
}
