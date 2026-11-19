using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static BuildKit;

// Level03Builder, parte 4: iluminación sala por sala.
// Principio: cada sala tiene sus propias fuentes justificadas (lámparas, luz de pantalla, luces de emergencia ámbar) con una temperatura
// de color distinta; la linterna no es la única forma de ver, solo mejora el detalle. El rojo de emergencia se reserva para el vestíbulo de S-9.
public static partial class Level03Builder
{
    static readonly Color Sodium = new Color(1.0f, 0.66f, 0.30f);
    static readonly Color WarmLamp = new Color(1.0f, 0.78f, 0.50f);
    static readonly Color AmberEmergency = new Color(1.0f, 0.52f, 0.16f);
    static readonly Color SickWhite = new Color(0.80f, 0.94f, 1.0f);
    static readonly Color Phosphor = new Color(0.45f, 1.0f, 0.62f);
    static readonly Color ProjectorBeam = new Color(0.78f, 0.88f, 1.0f);
    static readonly Color AlarmRed = new Color(1.0f, 0.10f, 0.05f);

    static Transform RoomLights(char c) => Lazy(RoomName(c), Lazy("RoomLights", lightingRoot));
    static Transform CorridorLights => Lazy("ObservationCorridor", Lazy("CorridorLights", lightingRoot));
    static Transform Emergency => Lazy("EmergencyLights", lightingRoot);
    static Transform Flickering => Lazy("FlickeringLights", lightingRoot);

    static void BuildLighting()
    {
        // Los objetos del pack traen luces propias (velas, apliques, lámparas): se apagan y cada sala usa las suyas
        foreach (Light l in propsRoot.GetComponentsInChildren<Light>(true)) l.enabled = false;

        string neon = AS + "NeonLamp.prefab", cage = FH + "hanging_cage_lamp.prefab", small = AS + "SingleLight.prefab";

        // --- M: llegada del montacargas. Un fluorescente que apenas aguanta y la luz de la cabina.
        Fixture(neon, "Landing", RoomLights('M'), new Vector3(30.9f, 0, 3.0f), WallH, true, 0, Sodium, 4.5f, 6.5f, false, true);
        Accent("Elevator_Cab", RoomLights('M'), new Vector3(34.6f, 0, 4.0f), 2.2f, WarmLamp, 1.3f, 4.0f);

        // --- R: recepción. Lámpara de jaula cálida sobre el mostrador, luz de la lámpara de mesa y dos apliques.
        Transform r = RoomLights('R');
        Fixture(cage, "Reception_Main", r, new Vector3(25.6f, 0, 3.6f), WallH, true, 0, WarmLamp, 13f, 10f, true);
        Accent("Reception_CounterLamp", r, new Vector3(26.3f, 0, 2.85f), 1.15f, WarmLamp, 2.6f, 3.5f);
        Accent("Reception_Clock", r, new Vector3(23.0f, 0, 3.5f), 1.8f, Sodium, 3.2f, 3.5f);
        Accent("Reception_MonitorGlow", r, new Vector3(23.8f, 0, 2.9f), 1.2f, Phosphor, 1.4f, 2.5f);
        Accent("Reception_DoorLight", r, new Vector3(27.8f, 0, 5.0f), 2.4f, Sodium, 4f, 5.5f);

        // --- O: corredor de observación. Penumbra ámbar con un tubo que parpadea y la luz que llega desde la cámara a través del vidrio.
        Transform o = CorridorLights;
        Fixture(neon, "Corridor_West", o, new Vector3(23.0f, 0, 13.0f), WallH, true, 0, Sodium, 11f, 9f, false, true);
        Fixture(cage, "Corridor_Vertical", o, new Vector3(29.0f, 0, 10.5f), WallH, true, 0, Sodium, 8f, 7f);
        Fixture(small, "Corridor_Emergency_1", Emergency, new Vector3(29.75f, 0, 13.0f), 2.4f, false, 0, AmberEmergency, 5.5f, 6.5f);
        Fixture(small, "Corridor_Emergency_2", Emergency, new Vector3(28.25f, 0, 7.8f), 2.4f, false, 0, AmberEmergency, 3.5f, 5f);
        Accent("Corridor_Collapse", o, new Vector3(20.2f, 0, 13.0f), 1.3f, AmberEmergency, 3.2f, 4.5f);

        // --- P: sala de proyección. Haz del proyector hacia la pantalla, resplandor de la pantalla y de los monitores.
        Transform p = RoomLights('P');
        Fixture(cage, "Projection_Overhead", p, new Vector3(29.0f, 0, 19.0f), WallH, true, 0, Sodium, 7f, 8f, false, true);
        SpotAccent("Projection_Beam", p, new Vector3(31.0f, 1.45f, 17.3f), Quaternion.LookRotation(new Vector3(-0.08f, 0.02f, 1f)), 38f, ProjectorBeam, 22f, 9f, false);
        Accent("Projection_ScreenGlow", p, new Vector3(29.0f, 0, 21.3f), 1.7f, ProjectorBeam, 3.5f, 5.5f);
        Accent("Projection_Monitors", p, new Vector3(27.3f, 0, 16.9f), 1.0f, Phosphor, 2.2f, 3.2f);

        // --- C: cámara de pruebas. Un único cono duro sobre la silla (sombras profundas) y relleno ámbar desde el techo.
        Transform c = RoomLights('C');
        SpotAccent("Chamber_SurgicalSpot", c, new Vector3(22.0f, 5.6f, 18.0f), Quaternion.Euler(90, 0, 0), 62f, new Color(1f, 0.90f, 0.72f), 34f, 11f, true);
        Fixture(cage, "Chamber_Ceiling_East", c, new Vector3(24.2f, 0, 20.4f), WallH * 2f, true, 0, Sodium, 12f, 9f);
        Fixture(cage, "Chamber_Ceiling_West", c, new Vector3(20.3f, 0, 15.6f), WallH * 2f, true, 0, Sodium, 10f, 8f, false, true);
        Accent("Chamber_UnderGallery", c, new Vector3(19.0f, 0, 17.5f), 2.5f, WarmLamp, 3.5f, 4.5f);
        Accent("Chamber_Gallery_Desk", c, new Vector3(18.9f, 0, 16.2f), WallH + 1.25f, WarmLamp, 4.5f, 5f);
        Accent("Chamber_Stairs", c, new Vector3(23.0f, 0, 20.9f), 2.6f, Sodium, 4f, 6f);

        // --- L: laboratorio / cámara fría. Blanco enfermizo, el único frío del nivel.
        Transform lab = RoomLights('L');
        Fixture(neon, "ColdLab_West", lab, new Vector3(12.4f, 0, 17.0f), WallH, true, 0, SickWhite, 10f, 8.5f);
        Fixture(neon, "ColdLab_East", lab, new Vector3(16.3f, 0, 17.9f), WallH, true, 0, SickWhite, 8f, 8f, false, true);
        Accent("ColdLab_Fridge", lab, new Vector3(16.8f, 0, 14.9f), 1.5f, SickWhite, 2.4f, 3f);

        // --- D: sala de dibujos. Una bombilla desnuda y la lámpara de la mesa: la luz justa para leer las paredes.
        Transform d = RoomLights('D');
        Fixture(cage, "Drawing_Bulb", d, new Vector3(7.0f, 0, 17.0f), WallH, true, 0, Sodium, 9f, 7.5f, false, true);
        Accent("Drawing_DeskLantern", d, new Vector3(7.55f, 0, 19.3f), 1.15f, WarmLamp, 4f, 5f);
        Accent("Drawing_BedCorner", d, new Vector3(5.2f, 0, 15.3f), 1.7f, Sodium, 2.6f, 4f);

        // --- X: dirección. Lámpara de araña cálida, lámpara de escritorio, velas del gabinete y el brillo verde de un monitor.
        Transform x = RoomLights('X');
        Accent("Office_Chandelier", x, new Vector3(11.0f, 0, 24.0f), 2.5f, WarmLamp, 12f, 10f);
        Accent("Office_DeskLamp", x, new Vector3(11.9f, 0, 26.9f), 1.35f, WarmLamp, 4.5f, 5f);
        Accent("Office_Candles", x, new Vector3(15.5f, 0, 22.4f), 1.6f, AmberEmergency, 3.0f, 4f, true);
        Accent("Office_MonitorGlow", x, new Vector3(10.2f, 0, 26.8f), 1.1f, Phosphor, 1.5f, 2.8f);
        Accent("Office_Fill", x, new Vector3(8.0f, 0, 21.8f), 2.3f, Sodium, 3f, 6f);

        // --- V: vestíbulo de S-9. Rojo de emergencia sobre la puerta, que parpadea apenas; una luz cálida muy débil para ver el suelo.
        Transform v = RoomLights('V');
        Fixture(small, "Vestibule_AlarmRed", Emergency, new Vector3(18.0f, 0, 27.3f), 2.7f, false, 0, AlarmRed, 4.5f, 6f, false, true);
        Accent("Vestibule_Floor", v, new Vector3(18.0f, 0, 24.5f), 2.5f, WarmLamp, 8.5f, 7f);
        Accent("Vestibule_DoorWash", v, new Vector3(18.0f, 0, 26.6f), 2.0f, Sodium, 3.5f, 4f);
        Accent("Vestibule_Candles", v, new Vector3(19.3f, 0, 25.4f), 0.95f, AmberEmergency, 2.2f, 3f, true);
    }

    // Lámpara con modelo: apaga las luces del prefab salvo una, que se configura. flicker => va al grupo FlickeringLights.
    static GameObject Fixture(string prefab, string name, Transform parent, Vector3 pos, float y, bool top, float yaw, Color color, float intensity, float range, bool shadows = false, bool flicker = false)
    {
        GameObject go = Spawn(prefab, flicker ? Flickering : parent);
        if (go == null) return null;
        go.name = "Lamp_" + name;
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
        for (int i = 1; i < lights.Length; i++) lights[i].enabled = false;

        Bounds b = GetBounds(go);
        main.transform.position = top ? new Vector3(b.center.x, b.min.y - 0.3f, b.center.z) : new Vector3(b.center.x, b.max.y + 0.15f, b.center.z);
        main.type = LightType.Point;
        main.color = color;
        main.intensity = intensity;
        main.range = range;
        main.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        main.enabled = true;
        if (flicker) main.gameObject.AddComponent<FlickerLight>().minFactor = 0.45f;
        return go;
    }

    // Punto de luz sin modelo (rellenos y luz de pantallas). Si parpadea va al grupo FlickeringLights.
    static void Accent(string name, Transform parent, Vector3 pos, float y, Color color, float intensity, float range, bool flicker = false)
    {
        GameObject go = new GameObject("Light_" + name);
        go.transform.SetParent(flicker ? Flickering : parent, false);
        go.transform.position = new Vector3(pos.x, y, pos.z);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = LightShadows.None;
        if (flicker) go.AddComponent<FlickerLight>().minFactor = 0.45f;
    }

    static void SpotAccent(string name, Transform parent, Vector3 pos, Quaternion rot, float angle, Color color, float intensity, float range, bool shadows)
    {
        GameObject go = new GameObject("Light_" + name);
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        Light l = go.AddComponent<Light>();
        l.type = LightType.Spot;
        l.spotAngle = angle;
        l.innerSpotAngle = angle * 0.55f;
        l.color = color;
        l.intensity = intensity;
        l.range = range;
        l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
    }
}
