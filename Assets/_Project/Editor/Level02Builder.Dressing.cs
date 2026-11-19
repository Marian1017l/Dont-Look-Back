using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BuildKit;

// Level02Builder, parte 2: mobiliario, instalaciones (tuberías, bandejas, conductos), rótulos y detalles de suelo.
// Los muebles clave se colocan a mano (BuildHeroes); el resto de cada sala se completa con listas de objetos del tema (Dress).
public static partial class Level02Builder
{
    static readonly List<Rect> occupied = new List<Rect>();        // huellas (X,Z) de objetos ya colocados
    static readonly List<Vector2> reserved = new List<Vector2>();  // puntos que deben quedar libres (objetos recogibles)
    static readonly List<Vector2> doorPoints = new List<Vector2>();
    static System.Random rng;

    // Muebles clave que usan el gameplay y la iluminación
    static GameObject deskMaint, deskMaint2, chestMaint, panelMain, deskElectric, trolleyIso, deskArchive, deskArchive2, elevatorGo, bedCellK, deskFinal, tableProjector, surgeryTable, boxFinal, boxFinal2;

    static void BuildDressing()
    {
        rng = new System.Random(2077);   // misma decoración cada vez que se regenera
        occupied.Clear(); reserved.Clear(); doorPoints.Clear();
        foreach (var g in Gaps)
            if (edgeMap.TryGetValue(Canon(g.c, g.r, g.d), out Edge e)) doorPoints.Add(new Vector2(e.Mid.x, e.Mid.z));
        foreach (var d in DoorwayModules)
            if (edgeMap.TryGetValue(Canon(d.c, d.r, d.d), out Edge e)) doorPoints.Add(new Vector2(e.Mid.x + U / 2f, e.Mid.z));   // centro del módulo de 4 m

        // Carriles de paso que la decoración automática no puede tapar (recorrido principal de cada sala)
        ReserveLane(new Vector2(4.5f, 17f), new Vector2(15.5f, 17f));    // mantenimiento: del tránsito al pasillo 1
        ReserveLane(new Vector2(26.5f, 17f), new Vector2(31f, 17f));     // sala eléctrica: del pasillo 1 al pasillo 2
        ReserveLane(new Vector2(31f, 17f), new Vector2(31f, 14.5f));
        ReserveLane(new Vector2(45f, 14.5f), new Vector2(45f, 18f));     // archivo: de la reja al pasaje a la zona final
        ReserveLane(new Vector2(45f, 18f), new Vector2(49.5f, 19f));
        ReserveLane(new Vector2(50.5f, 19f), new Vector2(52.4f, 18f));   // zona final: al montacargas
        ReserveLane(new Vector2(21f, 14.5f), new Vector2(21f, 12f));     // almacén: entrada del ramal sur

        BuildHeroes();
        BuildGameplayObjects();   // Level02Builder.Gameplay.cs: objetos recogibles, notas, puertas y salida
        DressRooms();
        BuildInstallations();
        BuildSigns();
        BuildDetails();
        BuildLighting();          // Level02Builder.Lighting.cs
    }

    static void ReserveLane(Vector2 a, Vector2 b)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(a, b)));
        for (int i = 0; i <= n; i++) reserved.Add(Vector2.Lerp(a, b, i / (float)n));
    }

    // ---------------------------------------------------------------- Agrupaciones

    static Transform Lazy(string name, Transform parent)
    {
        string key = (parent != null ? parent.name : "") + "/" + name;
        if (groups.TryGetValue(key, out Transform t) && t != null) return t;
        t = T(name, parent);
        groups[key] = t;
        return t;
    }

    static string RoomName(char c)
    {
        switch (c)
        {
            case 'T': return "Room_Transit";
            case 'B': return "Room_Maintenance";
            case 'W': return "Room_Storage";
            case 'C': return "Room_Electrical";
            case 'E': return "Room_Archive";
            case 'G': return "Room_Final";
            case 'f': return "Corridor_Service_01";
            case 'g': return "Corridor_Service_02";
            case 'n': return "Corridor_Service_03";
            default: return "Room_Isolation";   // h, i..m
        }
    }

    static string PropsName(char c)
    {
        switch (c)
        {
            case 'A': return "Stairs_Props";
            case 'T': return "Transit_Props";
            case 'B': return "Maintenance_Props";
            case 'W': return "Storage_Props";
            case 'C': return "Electrical_Props";
            case 'E': return "Archive_Props";
            case 'G': return "Final_Props";
            case 'f': case 'g': case 'n': return "Corridor_Props";
            default: return "Isolation_Props";
        }
    }

    // Instalaciones fijas (tuberías, bandejas, conductos) de cada sala o pasillo
    static Transform RoomGroup(char c)
    {
        if (c == 'A') return stairsGroup;
        bool corridor = c == 'f' || c == 'g' || c == 'n';
        return Lazy(RoomName(c), Lazy(corridor ? "Corridors" : "Rooms", environment));
    }

    static Transform PropsGroup(char c) => Lazy(PropsName(c), propsRoot);
    static Transform Details => Lazy("Environmental_Details", propsRoot);

    // ---------------------------------------------------------------- Utilidades de colocación

    static Vector3 Dir(char side) => side == 'N' ? Vector3.forward : side == 'S' ? Vector3.back : side == 'E' ? Vector3.right : Vector3.left;
    static Vector3 W(int c, int r, float dx = 0f, float dz = 0f) => CellCenter(c, r) + new Vector3(dx, 0, dz);
    static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    static Rect Footprint(Bounds b) => new Rect(b.min.x, b.min.z, b.size.x, b.size.z);
    static char CharAt(float x, float z) => At(Mathf.FloorToInt(x / U), Rows - 1 - Mathf.FloorToInt(z / U));

    static Vector3 TopOf(GameObject go)
    {
        Bounds b = GetBounds(go);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    static void Shuffle<T2>(List<T2> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // Un modelo demasiado grande se reduce para que no tape la sala
    static void FitSize(GameObject go)
    {
        Bounds b = GetBounds(go);
        float footprint = Mathf.Max(b.size.x, b.size.z);
        float k = Mathf.Min(footprint > 2.6f ? 2.6f / footprint : 1f, b.size.y > WallH - 0.15f ? (WallH - 0.15f) / b.size.y : 1f);
        if (k < 1f) go.transform.localScale *= k;
    }

    // Objeto con la espalda pegada a la pared del lado "side" de la celda (c,r), mirando hacia dentro de la sala
    static GameObject Against(string path, Transform parent, int c, int r, char side, float along = 0f, float scale = 1f, float yawExtra = 0f, float y = 0f, bool register = true, float maxDepth = 99f)
    {
        GameObject go = Spawn(path, parent);
        if (go == null) return null;
        if (scale != 1f) go.transform.localScale *= scale;
        FitSize(go);
        Vector3 dir = Dir(side);
        go.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg + yawExtra, 0);
        Bounds b = GetBounds(go);
        float depth = Mathf.Abs(dir.x) > 0.5f ? b.size.x : b.size.z;
        if (depth > maxDepth) { Object.DestroyImmediate(go); return null; }
        Vector3 perp = new Vector3(-dir.z, 0, dir.x);
        Vector3 p = CellCenter(c, r) + dir * (U / 2f - 0.14f - depth / 2f) + perp * along;
        SnapXZ(go, p.x, p.z);
        SnapY(go, y, false);
        if (y < 1.5f) EnsureCollider(go);
        if (register) occupied.Add(Footprint(GetBounds(go)));
        return go;
    }

    static GameObject Place(string path, Transform parent, Vector3 pos, float yaw, float scale = 1f, float y = 0f, bool collider = true, bool register = true, bool fit = true)
    {
        GameObject go = Spawn(path, parent);
        if (go == null) return null;
        if (scale != 1f) go.transform.localScale *= scale;
        if (fit) FitSize(go);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, y, false);
        if (collider && y < 1.5f) EnsureCollider(go);
        if (register) occupied.Add(Footprint(GetBounds(go)));
        return go;
    }

    // Objeto pequeño apoyado sobre un mueble
    static GameObject OnTop(string path, GameObject surface, float dx, float dz, float yaw = 0f, float scale = 1f)
    {
        GameObject go = Spawn(path, surface.transform.parent);
        if (go == null) return null;
        if (scale != 1f) go.transform.localScale *= scale;
        Vector3 top = TopOf(surface);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, top.x + dx, top.z + dz);
        SnapY(go, top.y, false);
        return go;
    }

    // ---------------------------------------------------------------- Muebles clave (colocados a mano)

    static void BuildHeroes()
    {
        // --- T: tránsito. Aún queda algo del hospital de arriba (silla de ruedas, armario), mezclado con material de servicio.
        Transform t = PropsGroup('T');
        Against(AS + "Cupboard.prefab", t, 0, 6, 'W', 0f);
        Against(FH + "wheelchair.prefab", t, 0, 8, 'W', -0.1f, 1f, 25f);
        Against(AS + "Box_V1.prefab", t, 1, 8, 'S', -0.45f);
        Against(AS + "Box_V2.prefab", t, 1, 8, 'S', 0.35f);
        Against(AS + "Heater.prefab", t, 1, 6, 'E', 0f);

        // --- B: sala de mantenimiento. Banco de trabajo con herramientas bajo el rótulo, cofre de herramientas, taquillas y estantes.
        Transform b = PropsGroup('B');
        deskMaint = Against(AS + "TableOffice.prefab", b, 4, 6, 'N', 0.1f);
        deskMaint2 = Against(AS + "TableOffice.prefab", b, 5, 6, 'N', 0.1f);
        if (deskMaint != null)
        {
            OnTop(FH + "bench_vise.prefab", deskMaint, -0.35f, 0f);
            OnTop(SP + "pipe wrench.prefab", deskMaint, 0.15f, 0.12f, 30f, 1.25f);
            OnTop(SP + "screwdriver.prefab", deskMaint, 0.35f, -0.1f, -20f, 1.4f);
            OnTop(SP + "Tape.prefab", deskMaint, 0.05f, -0.15f, 0f, 1.3f);
        }
        if (deskMaint2 != null)
        {
            OnTop(FH + "valve_radio.prefab", deskMaint2, 0.4f, 0f, 200f);
            OnTop(SP + "Cable.prefab", deskMaint2, -0.3f, 0.05f, 40f, 1.5f);
        }
        chestMaint = Against(FH + "tool_chest.prefab", b, 6, 6, 'N', 0.1f);
        Against(AS + "CaseMetallic.prefab", b, 2, 8, 'W', 0f);
        Against(AS + "CaseMetallic.prefab", b, 2, 9, 'W', 0f);
        Against(AS + "BigShelf_Stock.prefab", b, 4, 9, 'S', 0f);
        Against(AS + "BigShelf_Stock.prefab", b, 5, 9, 'S', 0f);
        Against(AS + "BigShelf_Stock.prefab", b, 6, 9, 'S', 0f);
        Against(FH + "fuse_cabinet.prefab", b, 7, 6, 'E', 0f, 1.5f, 0f, 0.9f);
        Against(FH + "fuse_cabinet.prefab", b, 7, 9, 'E', 0f, 1.5f, 0f, 0.9f);
        // rincón de la L: estantes y caldera vieja
        Against(AS + "BigShelf_Stock.prefab", b, 2, 10, 'W', 0f);
        Against(AS + "BigShelf_Stock.prefab", b, 2, 11, 'W', 0f);
        Against(AS + "BigShelf_Stock.prefab", b, 2, 12, 'W', 0f);
        Against(AS + "BigBoiler.prefab", b, 3, 11, 'E', 0f);
        Against(AS + "Box_V1.prefab", b, 3, 12, 'S', -0.4f);
        Against(AS + "Box_V2.prefab", b, 3, 12, 'S', 0.3f);

        // --- C: sala eléctrica. Tablero general (interactivo) con paneles laterales, transformadores y un equipo antiguo.
        Transform c = PropsGroup('C');
        panelMain = Against(FH + "fuse_cabinet.prefab", c, 15, 5, 'N', 0f, 2.6f, 0f, 0.35f);
        Against(FH + "fuse_cabinet.prefab", c, 14, 5, 'N', 0f, 2.0f, 0f, 0.35f);
        Against(FH + "fuse_cabinet.prefab", c, 16, 5, 'N', 0f, 2.0f, 0f, 0.35f);
        Against(FH + "fuse_cabinet.prefab", c, 13, 5, 'N', 0.1f, 1.7f, 0f, 0.5f);
        Against(FH + "fuse_cabinet.prefab", c, 17, 5, 'N', -0.1f, 1.7f, 0f, 0.5f);
        Transformer(c, new Vector3(37.0f, 0, 19.2f), 270f);
        Transformer(c, new Vector3(37.0f, 0, 16.4f), 270f);
        Place(FH + "tesla_coil.prefab", c, W(14, 6, 0.2f, -0.6f), 20f, 1.3f);
        deskElectric = Against(AS + "TableOffice.prefab", c, 17, 8, 'S', 0.1f);
        if (deskElectric != null)
        {
            Place(AS + "ChairOffice.prefab", c, new Vector3(TopOf(deskElectric).x, 0, 15.7f), 200f);
            OnTop(AS + "PC_Monitor.prefab", deskElectric, -0.4f, 0f, 190f);
            OnTop(AS + "Keyboard.prefab", deskElectric, -0.4f, -0.2f, 190f);
        }
        Against(FH + "tool_chest.prefab", c, 13, 8, 'S', 0f);
        Against(AS + "CaseMetallic.prefab", c, 18, 8, 'E', 0f);

        // --- h: pasillo de aislamiento. Carrito con partes de enfermería y una silla de ruedas mirando al muro del fondo.
        Transform iso = PropsGroup('h');
        trolleyIso = Against(FH + "instrument_trolley.prefab", iso, 13, 13, 'N', 0f);
        Place(FH + "wheelchair.prefab", iso, new Vector3(19.0f, 0, 5f), 270f);
        Against(AS + "Crutches.prefab", iso, 11, 13, 'N', 0.3f, 1f, 0f, 0f, true, 0.5f);

        // --- Celdas de aislamiento (4x4 m, puerta al pasillo por el norte)
        Transform ci = PropsGroup('i');
        Against(FH + "hospital_bed.prefab", ci, 10, 15, 'W', 0.1f, 1f, 0f);
        Against(AS + "Suspension_V2.prefab", ci, 10, 14, 'W', 0.3f, 1f, 0f, 0.4f, false);
        Against(AS + "Mattress.prefab", ci, 11, 15, 'S', -0.5f, 1f, 90f);   // tumbado a lo largo de la pared
        Against(AS + "Toilet.prefab", ci, 11, 14, 'E', -0.3f);

        Against(AS + "Mattress.prefab", PropsGroup('j'), 12, 15, 'S', 0.3f, 1f, 90f);
        Against(AS + "CanOld_Damaged.prefab", PropsGroup('j'), 13, 14, 'E', 0.2f);

        bedCellK = Against(FH + "hospital_bed.prefab", PropsGroup('k'), 15, 15, 'E', 0.2f);
        Against(AS + "Suspension.prefab", PropsGroup('k'), 14, 14, 'W', 0.2f, 1f, 0f, 0.5f, false);
        Against(AS + "WheelChair.prefab", PropsGroup('k'), 14, 15, 'W', 0.1f, 1f, 30f);

        Transform cl = PropsGroup('l');
        surgeryTable = Place(AS + "SurgeryTable.prefab", cl, W(17, 15, 0.2f, 0.3f), 90f);   // fuera de la línea de la puerta
        Against(AS + "MedRack.prefab", cl, 16, 15, 'W', 0f);
        Against(FH + "instrument_trolley.prefab", cl, 17, 15, 'S', 0.3f);
        Against(FH + "surgical_lamp.prefab", cl, 17, 14, 'E', 0.3f, 1f, 0f, 0f, false);

        Against(AS + "Mattress.prefab", PropsGroup('m'), 18, 15, 'W', 0f, 1f, 90f);
        Against(AS + "Toilet.prefab", PropsGroup('m'), 19, 15, 'E', -0.2f);
        Against(AS + "Box_V1.prefab", PropsGroup('m'), 19, 14, 'E', 0.3f);

        // --- E: archivo restringido. Estantes de expedientes, archivadores, escritorio con ordenador y un proyector.
        Transform e = PropsGroup('E');
        Against(AS + "BigShelf_Stock.prefab", e, 21, 5, 'N', 0f);
        Against(AS + "BigShelf_Stock.prefab", e, 22, 5, 'N', 0f);
        Against(AS + "BigShelf_Stock.prefab", e, 23, 5, 'N', 0f);
        Against(AS + "BigShelf_Stock.prefab", e, 24, 5, 'N', 0f);
        Against(FH + "filing_cabinet.prefab", e, 21, 6, 'W', 0f);
        Against(FH + "filing_cabinet.prefab", e, 21, 7, 'W', 0f);
        Against(FH + "filing_cabinet.prefab", e, 21, 8, 'W', -0.3f);
        deskArchive = Against(AS + "TableOffice.prefab", e, 24, 7, 'E', 0f);
        if (deskArchive != null)
        {
            Place(AS + "ChairOffice.prefab", e, new Vector3(47.5f, 0, TopOf(deskArchive).z), 90f);
            OnTop(AS + "PC_Monitor.prefab", deskArchive, 0f, 0.3f, 270f);
            OnTop(AS + "Keyboard.prefab", deskArchive, -0.2f, 0.3f, 270f);
        }
        deskArchive2 = Against(AS + "TableOffice.prefab", e, 23, 8, 'S', 0.1f);
        tableProjector = Place(AS + "TableSmall.prefab", e, W(22, 5, 0.9f, -0.9f), 0f);   // lejos del carril de paso
        if (tableProjector != null) OnTop(FH + "film_projector.prefab", tableProjector, 0f, 0f, 90f);
        Against(AS + "Box_V1.prefab", e, 24, 8, 'S', 0.4f);

        // --- G: zona final. El montacargas se coloca en Level02Builder.Gameplay.cs (lleva puerta e interacción).
        Transform g = PropsGroup('G');
        boxFinal = Against(AS + "Box_V1.prefab", g, 25, 5, 'N', -0.3f);
        boxFinal2 = Against(AS + "Box_V2.prefab", g, 25, 5, 'N', 0.35f);
        Against(AS + "Tank_V2.prefab", g, 26, 5, 'N', 0.2f);
        Against(AS + "BigBoiler.prefab", g, 26, 8, 'S', 0f);
        Against(FH + "fuse_cabinet.prefab", g, 25, 8, 'W', 0.2f, 1.5f, 0f, 0.9f);
    }

    // TEMP: transformador de potencia sencillo (caja con aletas y tres aisladores). Reemplazar por un modelo real.
    static void Transformer(Transform parent, Vector3 pos, float yaw)
    {
        GameObject root = new GameObject("TEMP_Transformer");
        root.transform.SetParent(parent, false);
        root.transform.position = pos;
        root.transform.rotation = Quaternion.Euler(0, yaw, 0);
        Material body = Mat("Transformer", new Color(0.17f, 0.23f, 0.24f), 0.45f, "Metal", 0.6f);
        Material porcelain = Mat("Porcelain", new Color(0.42f, 0.33f, 0.26f), 0.7f, "Metal", 0f);

        void Part(string n, Vector3 local, Vector3 size, Material m)
        {
            GameObject p = Box(n, root.transform, Vector3.zero, size, m, false);
            p.transform.localPosition = local;
            p.transform.localRotation = Quaternion.identity;
        }
        Part("Base", new Vector3(0, 0.1f, 0), new Vector3(1.25f, 0.2f, 0.95f), body);
        Part("Tank", new Vector3(0, 0.95f, 0), new Vector3(1.0f, 1.5f, 0.7f), body);
        for (int i = -1; i <= 1; i++) Part("Fin_" + i, new Vector3(i * 0.3f, 0.95f, 0.4f), new Vector3(0.12f, 1.3f, 0.1f), body);
        for (int i = -1; i <= 1; i++)
        {
            GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.name = "Bushing_" + i;
            cyl.transform.SetParent(root.transform, false);
            cyl.transform.localPosition = new Vector3(i * 0.3f, 1.95f, 0);
            cyl.transform.localScale = new Vector3(0.14f, 0.2f, 0.14f);
            cyl.GetComponent<Renderer>().sharedMaterial = porcelain;
        }
        BoxCollider col = root.AddComponent<BoxCollider>();
        col.center = new Vector3(0, 1.0f, 0);
        col.size = new Vector3(1.25f, 2.1f, 0.95f);
        occupied.Add(new Rect(pos.x - 0.7f, pos.z - 0.7f, 1.4f, 1.4f));
    }

    // ---------------------------------------------------------------- Decoración temática automática

    static void DressRooms()
    {
        // letra, objetos del tema, cuántos junto a las paredes, cuántos en el centro, fondo máximo (pasillos estrechos)
        Dress('T', new[] { AS + "Box_V1.prefab", AS + "SheetRack.prefab", FH + "instrument_trolley.prefab", AS + "CanOld_Damaged.prefab", AS + "Crutches.prefab", AS + "Heater.prefab" }, 3, 0);
        Dress('B', new[] { AS + "Box_V1.prefab", AS + "Box_V2.prefab", AS + "SmallMetalicCase.prefab", AS + "CaseMetallic.prefab", AS + "Cupboard.prefab", FH + "tool_chest.prefab", SP + "FuelCan.prefab", FH + "desk_fan.prefab", AS + "Pipe.prefab", AS + "Heater.prefab", AS + "ChairOffice.prefab", FH + "instrument_trolley.prefab" }, 10, 3);
        Dress('W', new[] { AS + "BigShelf.prefab", AS + "BigShelf_Stock.prefab", AS + "Tank_V2.prefab", AS + "BigBoiler.prefab", AS + "Box_V1.prefab", AS + "Box_V2.prefab", SP + "FuelCan.prefab", AS + "CaseMetallic.prefab", AS + "Cupboard.prefab", AS + "Pipe.prefab", AS + "Heater.prefab" }, 11, 3);
        Dress('C', new[] { FH + "fuse_cabinet.prefab", FH + "tool_chest.prefab", AS + "CaseMetallic.prefab", SP + "FuelCan.prefab", AS + "Box_V1.prefab", AS + "Pipe.prefab", AS + "Tank_V2.prefab", FH + "valve_radio.prefab" }, 6, 1);
        Dress('h', new[] { AS + "Box_V1.prefab", SP + "FuelCan.prefab", AS + "Crutches.prefab", AS + "CanOld_Damaged.prefab" }, 4, 0, 0.5f);
        Dress('f', new[] { AS + "Box_V1.prefab", SP + "FuelCan.prefab", AS + "CanOld_Damaged.prefab", AS + "Pipe.prefab" }, 4, 0, 0.5f);
        Dress('g', new[] { AS + "Box_V1.prefab", SP + "FuelCan.prefab", AS + "Pipe.prefab" }, 2, 0, 0.5f);
        Dress('n', new[] { AS + "Box_V1.prefab", AS + "CanOld_Damaged.prefab", AS + "Pipe.prefab" }, 2, 0, 0.5f);
        Dress('E', new[] { AS + "Box_V1.prefab", AS + "Box_V2.prefab", FH + "filing_cabinet.prefab", AS + "ChairOffice.prefab", FH + "desk_fan.prefab", FH + "crt_television.prefab", AS + "SmallMetalicCase.prefab" }, 4, 1);
        Dress('G', new[] { AS + "Box_V1.prefab", AS + "Box_V2.prefab", SP + "FuelCan.prefab", AS + "CanOld_Damaged.prefab", AS + "Pipe.prefab" }, 3, 0);
    }

    static void Dress(char letter, string[] pool, int perimeter, int center, float maxDepth = 99f)
    {
        List<string> valid = new List<string>();
        foreach (string path in pool) if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) valid.Add(path);
        if (valid.Count == 0) { Debug.LogWarning("Level02Builder: ningún objeto del tema '" + letter + "' existe."); return; }

        Transform group = PropsGroup(letter);
        var cells = new List<Vector2Int>();
        for (int r = 0; r < Rows; r++) for (int c = 0; c < Cols; c++) if (grid[r, c] == letter) cells.Add(new Vector2Int(c, r));

        // Candidatos junto a las paredes (se saltan los lados con hueco o puerta)
        var candidates = new List<(int c, int r, char d, float along)>();
        foreach (Vector2Int cr in cells)
            foreach (char d in "NESW")
            {
                int nc = cr.x + (d == 'E' ? 1 : d == 'W' ? -1 : 0), nr = cr.y + (d == 'S' ? 1 : d == 'N' ? -1 : 0);
                if (At(nc, nr) == letter) continue;
                if (!edgeMap.TryGetValue(Canon(cr.x, cr.y, d), out Edge edge)) continue;
                if (edge.gap || edge.doorStart || edge.doorSecond) continue;
                for (int k = 0; k < 2; k++) candidates.Add((cr.x, cr.y, d, Rand(-0.6f, 0.6f)));
            }
        Shuffle(candidates);

        int placed = 0;
        foreach (var cand in candidates)
        {
            if (placed >= perimeter) break;
            for (int attempt = 0; attempt < 3; attempt++)
            {
                GameObject go = Against(valid[rng.Next(valid.Count)], group, cand.c, cand.r, cand.d, cand.along, 1f, 0f, 0f, false, maxDepth);
                if (go == null) continue;
                Rect rect = Footprint(GetBounds(go));
                if (FitsRoom(rect, letter) && IsFree(rect)) { occupied.Add(rect); placed++; break; }
                Object.DestroyImmediate(go);
            }
        }

        placed = 0;
        for (int i = 0; i < center * 10 && placed < center && cells.Count > 0; i++)
        {
            Vector2Int cr = cells[rng.Next(cells.Count)];
            GameObject go = Place(valid[rng.Next(valid.Count)], group, W(cr.x, cr.y, Rand(-0.6f, 0.6f), Rand(-0.6f, 0.6f)), Rand(0f, 360f), 1f, 0f, true, false);
            if (go == null) continue;
            Rect rect = Footprint(GetBounds(go));
            if (FitsRoom(rect, letter) && IsFree(rect)) { occupied.Add(rect); placed++; }
            else Object.DestroyImmediate(go);
        }
    }

    static bool FitsRoom(Rect r, char letter)
    {
        float x0 = r.xMin + 0.03f, x1 = r.xMax - 0.03f, z0 = r.yMin + 0.03f, z1 = r.yMax - 0.03f;
        return CharAt(x0, z0) == letter && CharAt(x1, z0) == letter && CharAt(x0, z1) == letter && CharAt(x1, z1) == letter;
    }

    static bool IsFree(Rect r)
    {
        Rect padded = new Rect(r.x - 0.12f, r.y - 0.12f, r.width + 0.24f, r.height + 0.24f);
        foreach (Rect o in occupied) if (padded.Overlaps(o)) return false;
        foreach (Vector2 p in doorPoints) if (Dist(r, p) < 1.2f) return false;
        foreach (Vector2 p in reserved) if (Dist(r, p) < 0.9f) return false;
        return true;
    }

    static float Dist(Rect r, Vector2 p) => (new Vector2(Mathf.Clamp(p.x, r.xMin, r.xMax), Mathf.Clamp(p.y, r.yMin, r.yMax)) - p).magnitude;

    // ---------------------------------------------------------------- Instalaciones (tuberías, bandejas, conductos)

    // Metal pintado y desgastado para instalaciones temporales (conductos, bandejas, pasamanos): hormigón del pack con brillo metálico
    static Material MetalMat(float smoothness = 0.45f) =>
        Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + "hospital_floor_slab_Exposed_slab_concrete.mat"), ZService, "Metal", new Color(0.62f, 0.70f, 0.74f), smoothness, 0.65f);

    // Tubería recta entre dos puntos, formada por tramos del modelo Pipe (3,6 m)
    static void PipeRun(Transform parent, Vector3 a, Vector3 b, float thick = 1f, string name = "Pipe")
    {
        Vector3 d = b - a;
        float len = d.magnitude;
        if (len < 0.3f) return;
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 3.6f));
        float piece = len / n;
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, d.normalized);
        for (int i = 0; i < n; i++)
        {
            GameObject go = Spawn(AS + "Pipe.prefab", parent);
            if (go == null) return;
            go.name = name + "_" + (i + 1).ToString("00");
            go.transform.rotation = rot;
            go.transform.localScale = new Vector3(thick, piece / 3.63f, thick);
            go.transform.position += a + d.normalized * (piece * (i + 0.5f)) - GetBounds(go).center;
        }
    }

    // TEMP: bandeja portacables (fondo plano y dos bordes) colgada del techo
    static void Tray(Transform parent, Vector3 a, Vector3 b, float width = 0.3f)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
        Vector3 mid = (a + b) / 2f;
        Material m = MetalMat(0.35f);
        GameObject root = new GameObject("TEMP_CableTray");
        root.transform.SetParent(parent, false);
        Vector3 sz = alongX ? new Vector3(len, 0.025f, width) : new Vector3(width, 0.025f, len);
        Box("Base", root.transform, mid, sz, m, false);
        Vector3 railSz = alongX ? new Vector3(len, 0.07f, 0.025f) : new Vector3(0.025f, 0.07f, len);
        for (int s = -1; s <= 1; s += 2)
            Box("Rail", root.transform, mid + (alongX ? new Vector3(0, 0.04f, s * width / 2f) : new Vector3(s * width / 2f, 0.04f, 0)), railSz, m, false);
        for (float t = 0.6f; t < len; t += 1.8f)
        {
            Vector3 hp = alongX ? new Vector3(a.x + Mathf.Sign(b.x - a.x) * t, mid.y + 0.12f, mid.z) : new Vector3(mid.x, mid.y + 0.12f, a.z + Mathf.Sign(b.z - a.z) * t);
            Box("Hanger", root.transform, hp, new Vector3(0.03f, 0.24f, 0.03f), m, false);
        }
    }

    // TEMP: conducto de ventilación rectangular
    static void Duct(Transform parent, Vector3 a, Vector3 b, float w, float h)
    {
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        float len = alongX ? Mathf.Abs(b.x - a.x) : Mathf.Abs(b.z - a.z);
        GameObject go = Box("TEMP_Duct", parent, (a + b) / 2f, alongX ? new Vector3(len, h, w) : new Vector3(w, h, len), MetalMat(0.55f), false);
        go.transform.position = (a + b) / 2f;
    }

    static void BuildInstallations()
    {
        float py = 2.78f;   // altura de las tuberías (por encima de la cabeza)

        // Escaleras: tuberías a lo largo del pozo, una bajante vertical y pasamanos
        Transform stairs = RoomGroup('A');
        PipeRun(stairs, new Vector3(0.5f, 5.7f, 31.6f), new Vector3(0.5f, 5.7f, 20.4f), 1f, "Pipe_Stairs_West");
        PipeRun(stairs, new Vector3(3.5f, 6.0f, 31.6f), new Vector3(3.5f, 6.0f, 20.4f), 0.8f, "Pipe_Stairs_East");
        PipeRun(stairs, new Vector3(0.45f, 1.6f, 25f), new Vector3(0.45f, 6.2f, 25f), 1f, "Pipe_Stairs_Downpipe");
        Handrails(stairs);

        Transform t = RoomGroup('T');
        PipeRun(t, new Vector3(0.6f, py, 14.3f), new Vector3(0.6f, py, 19.7f), 1f);
        PipeRun(t, new Vector3(0.4f, 0f, 19.6f), new Vector3(0.4f, 3.1f, 19.6f), 1f, "Pipe_Vertical");

        Transform b = RoomGroup('B');
        PipeRun(b, new Vector3(4.5f, py, 13.2f), new Vector3(15.5f, py, 13.2f), 1f);
        PipeRun(b, new Vector3(4.5f, py - 0.25f, 19.2f), new Vector3(15.5f, py - 0.25f, 19.2f), 0.8f);
        Tray(b, new Vector3(4.5f, 3.0f, 16.2f), new Vector3(15.5f, 3.0f, 16.2f), 0.35f);
        Duct(b, new Vector3(4.6f, 2.85f, 7.5f), new Vector3(4.6f, 2.85f, 11.5f), 0.5f, 0.4f);

        Transform w = RoomGroup('W');
        PipeRun(w, new Vector3(18.4f, py, 9.0f), new Vector3(25.6f, py, 9.0f), 1f);
        PipeRun(w, new Vector3(18.4f, py - 0.2f, 13.0f), new Vector3(25.6f, py - 0.2f, 13.0f), 0.9f);
        PipeRun(w, new Vector3(23.0f, py + 0.05f, 8.4f), new Vector3(23.0f, py + 0.05f, 13.6f), 1f);

        Transform c = RoomGroup('C');
        PipeRun(c, new Vector3(26.5f, py, 15.0f), new Vector3(37.5f, py, 15.0f), 1.1f);
        PipeRun(c, new Vector3(26.5f, py, 21.2f), new Vector3(37.5f, py, 21.2f), 1.1f);
        Tray(c, new Vector3(26.5f, 3.0f, 16.3f), new Vector3(37.5f, 3.0f, 16.3f), 0.4f);
        Tray(c, new Vector3(26.5f, 3.0f, 19.8f), new Vector3(37.5f, 3.0f, 19.8f), 0.4f);
        Duct(c, new Vector3(26.5f, 2.8f, 18.9f), new Vector3(37.5f, 2.8f, 18.9f), 0.55f, 0.4f);

        // Pasillos de servicio: dos tuberías laterales y una bandeja central; se detienen antes de las vigas de los huecos
        Corridor('f', new Vector3(16.5f, 0, 17f), new Vector3(25.5f, 0, 17f));
        Corridor('g', new Vector3(31f, 0, 13.5f), new Vector3(31f, 0, 6.5f));
        Corridor('h', new Vector3(18.5f, 0, 5f), new Vector3(45.5f, 0, 5f));
        Corridor('n', new Vector3(45f, 0, 13.5f), new Vector3(45f, 0, 6.5f));

        Transform e = RoomGroup('E');
        PipeRun(e, new Vector3(42.5f, py, 21.0f), new Vector3(49.5f, py, 21.0f), 0.9f);
        Duct(e, new Vector3(42.6f, 2.8f, 15.2f), new Vector3(49.4f, 2.8f, 15.2f), 0.5f, 0.35f);

        Transform g = RoomGroup('G');
        PipeRun(g, new Vector3(50.5f, py + 0.1f, 15.0f), new Vector3(55.5f, py + 0.1f, 15.0f), 1.2f);
        PipeRun(g, new Vector3(50.5f, py + 0.1f, 21.0f), new Vector3(55.5f, py + 0.1f, 21.0f), 1.2f);
        PipeRun(g, new Vector3(50.4f, 0f, 21.6f), new Vector3(50.4f, 3.1f, 21.6f), 1.2f, "Pipe_Vertical");
    }

    static void Corridor(char letter, Vector3 a, Vector3 b)
    {
        Transform g = RoomGroup(letter);
        bool alongX = Mathf.Abs(b.x - a.x) > Mathf.Abs(b.z - a.z);
        Vector3 side = alongX ? new Vector3(0, 0, 0.62f) : new Vector3(0.62f, 0, 0);
        Vector3 up = new Vector3(0, 2.75f, 0);
        PipeRun(g, a + side + up, b + side + up, 1f, "Pipe_A");
        PipeRun(g, a - side + up + new Vector3(0, 0.1f, 0), b - side + up + new Vector3(0, 0.1f, 0), 0.85f, "Pipe_B");
        Tray(g, a + new Vector3(0, 3.02f, 0), b + new Vector3(0, 3.02f, 0), 0.28f);
    }

    // TEMP: pasamanos de tubo de acero a ambos lados de cada tramo de escalera
    static void Handrails(Transform parent)
    {
        Material m = MetalMat(0.5f);
        foreach (var f in new[] { (z0: 30f, y0: 3.2f, z1: 26f, y1: 1.6f), (z0: 24f, y0: 1.6f, z1: 20f, y1: 0f) })
            foreach (float x in new[] { 0.32f, 3.68f })
            {
                Vector3 a = new Vector3(x, f.y0 + 0.95f, f.z0), b = new Vector3(x, f.y1 + 0.95f, f.z1);
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(rail.GetComponent<Collider>());
                rail.name = "TEMP_Handrail";
                rail.transform.SetParent(parent, false);
                rail.transform.position = (a + b) / 2f;
                rail.transform.rotation = Quaternion.FromToRotation(Vector3.up, (b - a).normalized);
                rail.transform.localScale = new Vector3(0.05f, (b - a).magnitude / 2f, 0.05f);
                rail.GetComponent<Renderer>().sharedMaterial = m;
                for (int k = 0; k < 3; k++)   // soportes a la pared
                {
                    Vector3 p = Vector3.Lerp(a, b, 0.1f + k * 0.4f);
                    float wallX = x < 2f ? 0.12f : 3.88f;
                    GameObject br = Box("TEMP_HandrailBracket", parent, new Vector3((p.x + wallX) / 2f, p.y, p.z), new Vector3(Mathf.Abs(p.x - wallX), 0.03f, 0.03f), m, false);
                }
            }
    }

    // ---------------------------------------------------------------- Rótulos pintados

    static readonly Color SignYellow = new Color(0.78f, 0.62f, 0.16f);
    static readonly Color SignWhite = new Color(0.72f, 0.75f, 0.72f);
    static readonly Color SignRed = new Color(0.55f, 0.05f, 0.04f);
    static readonly Color Scratch = new Color(0.62f, 0.62f, 0.56f);

    // Texto pintado en la pared del lado "side" de la celda (c,r). height = alto de las letras en metros.
    static void WallSign(string text, int c, int r, char side, float along, float y, float height, Color color, bool drip = false, float maxWidth = 99f)
    {
        Vector3 dir = Dir(side);
        Vector3 perp = new Vector3(-dir.z, 0, dir.x);
        Vector3 p = CellCenter(c, r) + dir * (U / 2f - 0.13f) + perp * along;
        Texture2D tex = PaintTexture(text, drip ? 5 : 0, drip ? "Sign_D_" : "Sign_", true);
        float h = height * (drip ? 12f / 7f : 1f);
        float wd = h * tex.width / tex.height;
        if (wd > maxWidth) { h *= maxWidth / wd; wd = maxWidth; }

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "Sign_" + System.Text.RegularExpressions.Regex.Replace(text, "[^A-Za-z0-9]+", "_");
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(Details, false);
        go.transform.SetPositionAndRotation(new Vector3(p.x, y, p.z), Quaternion.Euler(0, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0));
        go.transform.localScale = new Vector3(wd, h, 1f);

        string key = "L2_Sign_" + go.name + "_" + ColorUtility.ToHtmlStringRGB(color);
        if (!materials.TryGetValue(key, out Material mat))
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
            mat.SetTexture("_BaseMap", tex);
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", 0.3f);
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", 0.4f);
            mat.EnableKeyword("_ALPHATEST_ON");
            mat.renderQueue = 2450;
            AssetDatabase.CreateAsset(mat, MatDir + key + ".mat");
            materials[key] = mat;
        }
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static void BuildSigns()
    {
        WallSign("SERVICIOS TECNICOS", 0, 0, 'W', 0f, 4.5f, 0.2f, SignYellow);
        WallSign("SOLO PERSONAL AUTORIZADO", 0, 3, 'E', 0f, 2.7f, 0.17f, SignWhite);
        WallSign("ACCESO RESTRINGIDO", 1, 6, 'E', 0f, 2.0f, 0.17f, SignYellow, false, 1.7f);
        WallSign("TALLER DE MANTENIMIENTO", 5, 6, 'N', 0f, 2.35f, 0.17f, SignWhite);
        WallSign("SALA ELECTRICA >", 9, 7, 'N', 0f, 1.9f, 0.16f, SignYellow);
        WallSign("ALMACEN", 11, 9, 'N', 0f, 2.0f, 0.2f, SignWhite);
        WallSign("PELIGRO ALTA TENSION", 13, 6, 'W', 0f, 1.9f, 0.18f, SignRed);
        WallSign("TABLERO GENERAL", 15, 5, 'N', 0f, 2.75f, 0.14f, SignWhite);
        WallSign("AISLAMIENTO >", 15, 11, 'W', 0f, 1.8f, 0.16f, SignYellow);
        WallSign("AISLAMIENTO", 11, 13, 'N', 0.5f, 1.8f, 0.2f, SignWhite);
        for (int i = 0; i < 5; i++) WallSign("CELDA " + (i + 1), 10 + 2 * i, 13, 'S', 1f, 2.45f, 0.14f, SignWhite);
        WallSign("ARCHIVO RESTRINGIDO", 22, 10, 'W', 0f, 1.8f, 0.16f, SignYellow);
        WallSign("SUPERVISION ESPECIAL", 21, 6, 'W', 0.6f, 2.3f, 0.13f, SignWhite);
        WallSign("MONTACARGAS", 27, 5, 'N', -0.1f, 1.9f, 0.16f, SignYellow, false, 1.8f);
        WallSign("NIVEL INFERIOR", 27, 8, 'S', 0.1f, 1.9f, 0.16f, SignWhite, false, 1.8f);

        // Marcas en las celdas: recuento de días y una frase
        WallSign("IIII IIII III", 10, 14, 'W', 0.2f, 1.4f, 0.13f, Scratch);
        WallSign("IIII IIII IIII IIII", 12, 15, 'W', 0f, 1.2f, 0.12f, Scratch);
        WallSign("ESE NO SOY YO", 19, 15, 'S', 0.3f, 1.5f, 0.2f, SignRed, true);
    }

    // ---------------------------------------------------------------- Detalles de suelo: charcos de humedad

    static void BuildDetails()
    {
        Texture2D tex = PuddleTexture();
        string key = "L2_Detail_Puddle";
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key };
        mat.SetTexture("_BaseMap", tex);
        mat.SetColor("_BaseColor", new Color(0.025f, 0.04f, 0.05f, 1f));
        mat.SetFloat("_Smoothness", 0.97f);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_AlphaClip", 1f);
        mat.SetFloat("_Cutoff", 0.4f);
        mat.EnableKeyword("_ALPHATEST_ON");
        mat.renderQueue = 2450;
        AssetDatabase.CreateAsset(mat, MatDir + key + ".mat");

        var puddles = new (float x, float z, float s)[]
        {
            (21.5f, 16.6f, 1.1f), (31.2f, 9.5f, 1.3f), (24f, 5.3f, 1.0f), (35f, 4.7f, 1.4f), (20.5f, 11f, 1.6f), (9f, 17.5f, 1.2f),
            (6f, 8f, 1.4f), (33f, 18f, 1.8f), (45f, 9f, 1.2f), (52f, 17f, 1.5f), (13.5f, 13.4f, 0.9f), (44f, 17f, 1.0f), (2.4f, 15f, 1.0f),
        };
        int i = 0;
        foreach (var p in puddles)
        {
            GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Puddle_" + (++i).ToString("00");
            Object.DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(Details, false);
            q.transform.SetPositionAndRotation(new Vector3(p.x, 0.012f, p.z), Quaternion.Euler(90f, Rand(0, 360), 0));
            q.transform.localScale = new Vector3(p.s, p.s * Rand(0.6f, 0.85f), 1f);
            q.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }

    static Texture2D PuddleTexture()
    {
        const string path = "Assets/_Project/Art/Textures/L2_Puddle.png";
        EnsureFolder("Assets/_Project/Art", "Textures");
        const int n = 128;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        System.Random rnd = new System.Random(11);
        float phase = (float)rnd.NextDouble() * 6f;
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2(x, y) / (n - 1) * 2f - Vector2.one;
                float ang = Mathf.Atan2(p.y, p.x);
                float edge = 0.78f + 0.13f * Mathf.Sin(ang * 3f + phase) + 0.07f * Mathf.Sin(ang * 7f + phase * 2f);
                float a = 1f - Mathf.Clamp01((p.magnitude - edge * 0.78f) / 0.2f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a * a * (3f - 2f * a)));
            }
        tex.Apply();
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
}
