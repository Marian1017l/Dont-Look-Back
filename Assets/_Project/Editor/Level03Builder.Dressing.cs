using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static BuildKit;

// Level03Builder, parte 2: mobiliario, equipo, rótulos, dibujos del paciente 214 y detalles ambientales.
// Todo se coloca a mano, sala por sala (el nivel es pequeño y cada sala cuenta algo). Las posiciones son metros del mundo.
// Las utilidades de colocación (Put, Wall, OnTop) se basan en las de Level02Builder, copiadas a propósito para mantener los niveles independientes.
public static partial class Level03Builder
{
    // Interior de cada sala (x, z, ancho, fondo) en metros
    static readonly Rect rM = new Rect(30, 2, 6, 4), rR = new Rect(22, 0, 8, 6), rOh = new Rect(18, 12, 12, 2), rOv = new Rect(28, 6, 2, 10);
    static readonly Rect rP = new Rect(26, 16, 6, 6), rC = new Rect(18, 14, 8, 8), rL = new Rect(10, 14, 8, 6), rD = new Rect(4, 14, 6, 6);
    static readonly Rect rX = new Rect(6, 20, 10, 8), rV = new Rect(16, 22, 4, 6);

    static System.Random rng;

    // Muebles y superficies que usan el gameplay y la iluminación
    static GameObject counterL, counterR, corridorBench, projDesk, projTable, chamberTrolley, galleryDesk, labBench, labLocker, drawDesk, drawBedside, officeDesk, vestBench;
    static GameObject elevatorGo, mainDoorGo, officeAltar, officeMonitor;

    static void BuildDressing()
    {
        rng = new System.Random(3307);   // misma decoración cada vez que se regenera
        BuildProps();
        BuildPaperTrails();
        BuildDrawingRoom();
        BuildSigns();
        BuildDetails();
        BuildGameplayObjects();   // Level03Builder.Gameplay.cs
        BuildLighting();          // Level03Builder.Lighting.cs
        BuildLocalVolumes();
    }

    // ---------------------------------------------------------------- Utilidades de colocación

    static float Rand(float min, float max) => min + (float)rng.NextDouble() * (max - min);
    static Transform Cat(string category) => Lazy(category, propsRoot);
    static Transform RoomDetails(char c) => Lazy(RoomName(c), Lazy(c == 'O' ? "Corridors" : "Rooms", environment));
    static Transform EnvDetails => Lazy("EnvironmentalDetails", environment);

    static Vector3 Dir(char side) => side == 'N' ? Vector3.forward : side == 'S' ? Vector3.back : side == 'E' ? Vector3.right : Vector3.left;
    static char Opposite(char side) => side == 'N' ? 'S' : side == 'S' ? 'N' : side == 'E' ? 'W' : 'E';
    static Quaternion Facing(Vector3 normal) => Quaternion.Euler(0, Mathf.Atan2(-normal.x, -normal.z) * Mathf.Rad2Deg, 0);   // un Quad mira hacia -Z

    static Vector3 TopOf(GameObject go)
    {
        Bounds b = GetBounds(go);
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    // Punto sobre la cara interior de una pared de la sala (wall = lado de la pared)
    static Vector3 WallPoint(Rect r, char wall, float along, float y)
    {
        const float inset = 0.135f;
        switch (wall)
        {
            case 'N': return new Vector3(along, y, r.yMax - inset);
            case 'S': return new Vector3(along, y, r.yMin + inset);
            case 'E': return new Vector3(r.xMax - inset, y, along);
            default: return new Vector3(r.xMin + inset, y, along);
        }
    }

    static GameObject Put(string cat, string name, string prefab, Vector3 pos, float yaw, float scale = 1f, float y = 0f, bool collider = true)
    {
        GameObject go = Spawn(prefab, Cat(cat));
        if (go == null) return null;
        go.name = name;
        if (scale != 1f) go.transform.localScale *= scale;
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, y, false);
        if (collider) EnsureCollider(go);
        return go;
    }

    // Objeto con la espalda pegada a la pared "side" de la sala, mirando hacia dentro. along = coordenada a lo largo de la pared (x o z).
    static GameObject Wall(string cat, string name, string prefab, Rect room, char side, float along, float scale = 1f, float y = 0f, float yawExtra = 0f)
    {
        GameObject go = Spawn(prefab, Cat(cat));
        if (go == null) return null;
        go.name = name;
        if (scale != 1f) go.transform.localScale *= scale;
        Vector3 dir = Dir(side);
        go.transform.rotation = Quaternion.Euler(0, Mathf.Atan2(-dir.x, -dir.z) * Mathf.Rad2Deg + yawExtra, 0);
        Bounds b = GetBounds(go);
        float depth = Mathf.Abs(dir.x) > 0.5f ? b.size.x : b.size.z;
        const float inset = 0.135f;
        Vector3 p;
        switch (side)
        {
            case 'S': p = new Vector3(along, 0, room.yMin + inset + depth / 2f); break;
            case 'N': p = new Vector3(along, 0, room.yMax - inset - depth / 2f); break;
            case 'W': p = new Vector3(room.xMin + inset + depth / 2f, 0, along); break;
            default: p = new Vector3(room.xMax - inset - depth / 2f, 0, along); break;
        }
        SnapXZ(go, p.x, p.z);
        SnapY(go, y, false);
        EnsureCollider(go);
        return go;
    }

    // Objeto pequeño apoyado sobre un mueble
    static GameObject OnTop(string cat, string name, string prefab, GameObject surface, float dx, float dz, float yaw = 0f, float scale = 1f)
    {
        if (surface == null) return null;
        GameObject go = Spawn(prefab, Cat(cat));
        if (go == null) return null;
        go.name = name;
        if (scale != 1f) go.transform.localScale *= scale;
        Vector3 top = TopOf(surface);
        go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        SnapXZ(go, top.x + dx, top.z + dz);
        SnapY(go, top.y, false);
        return go;
    }

    // ---------------------------------------------------------------- Mobiliario por sala

    static void BuildProps()
    {
        // --- M: llegada del montacargas. La cabina es de donde sale el jugador; el resto es un rellano de carga abandonado.
        elevatorGo = Put("Furniture", "Landing_Elevator", AS + "Elevator.prefab", new Vector3(34.18f, 0, 4f), 90f, 1f, 0f, false);   // la cabina ya trae su MeshCollider
        Put("Storage", "Landing_CrateLower", AS + "Box_V1.prefab", new Vector3(31.0f, 0, 2.6f), 12f);
        Put("Storage", "Landing_CrateUpper", AS + "Box_V2.prefab", new Vector3(31.0f, 0, 2.6f), -20f, 1f, 0.23f);
        Wall("MedicalEquipment", "Landing_Heater", AS + "Heater.prefab", rM, 'S', 32.2f);
        Wall("Furniture", "Landing_Wheelchair", FH + "wheelchair.prefab", rM, 'N', 33.0f, 1f, 0f, 20f);

        // --- R: recepción de investigación. Mostrador, archivadores, reloj parado a las 03:07 y una puerta tapiada.
        counterL = Put("Furniture", "Reception_CounterLeft", AS + "TableOffice.prefab", new Vector3(24.2f, 0, 2.7f), 0f);
        counterR = Put("Furniture", "Reception_CounterRight", AS + "TableOffice.prefab", new Vector3(25.8f, 0, 2.7f), 0f);
        Put("Furniture", "Reception_StaffChair", AS + "ChairOffice.prefab", new Vector3(24.9f, 0, 1.7f), 25f);
        GameObject benchR = Wall("Furniture", "Reception_WaitingBench", AS + "Bench.prefab", rR, 'N', 23.3f);
        receptionBench = benchR;
        Wall("Furniture", "Reception_WaitingArmchair", AS + "Sofa_Chair.prefab", rR, 'N', 25.4f, 1f, 0f, 15f);
        Wall("Storage", "Reception_FilingCabinet_1", FH + "filing_cabinet.prefab", rR, 'S', 27.3f);
        Wall("Storage", "Reception_FilingCabinet_2", FH + "filing_cabinet.prefab", rR, 'S', 28.05f);
        Wall("Storage", "Reception_Cupboard", AS + "Cupboard.prefab", rR, 'W', 1.3f);
        Wall("Decoration", "Reception_GrandfatherClock", FH + "grandfather_clock.prefab", rR, 'W', 3.5f);
        Wall("Decoration", "Reception_BoardedDoor", FH + "boarded_door.prefab", rR, 'W', 5.0f);
        Wall("Decoration", "Reception_NoticeBoard", AS + "Board.prefab", rR, 'S', 24.6f, 0.5f);
        Wall("Decoration", "Reception_Poster", AS + "PosterHuman.prefab", rR, 'N', 27.2f, 0.5f, 1.1f);
        OnTop("MedicalEquipment", "Reception_Monitor", AS + "PC_Monitor.prefab", counterL, -0.4f, 0.05f, 15f);
        OnTop("MedicalEquipment", "Reception_Keyboard", AS + "Keyboard.prefab", counterL, -0.38f, -0.2f, 15f);
        OnTop("Decoration", "Reception_OilLamp", FH + "oil_table_lamp.prefab", counterR, 0.5f, 0.15f);
        OnTop("Decoration", "Reception_Mug", AS + "Mug.prefab", counterR, -0.2f, -0.2f, 40f);

        // --- O: corredor de observación. Material de planta abandonado y, al fondo, el tramo derrumbado.
        corridorBench = Wall("Furniture", "Corridor_Bench", AS + "Bench.prefab", rOh, 'S', 24.4f);
        Wall("Storage", "Corridor_Cupboard", AS + "Cupboard.prefab", rOh, 'S', 26.9f);
        Wall("Furniture", "Corridor_Wheelchair", FH + "wheelchair.prefab", rOv, 'W', 9.2f, 1f, 0f, 30f);
        Wall("MedicalEquipment", "Corridor_Trolley", FH + "instrument_trolley.prefab", rOv, 'E', 10.8f);
        Wall("Storage", "Corridor_Crate", AS + "Box_V1.prefab", rOv, 'E', 8.3f);

        // --- P: sala de proyección. Pantalla, filas de sillas, proyector y monitores de la sala de grabación.
        GameObject screen = Wall("Decoration", "Projection_Screen", AS + "ProjectorCloth.prefab", rP, 'N', 29.0f, 0.62f, 0.55f);
        if (screen != null)
        {
            // TEMP: el modelo trae solo el marco; se añade la tela (hoja blanca, amarillenta, con un leve brillo de proyección)
            Bounds sb = GetBounds(screen);
            Material cloth = Mat("ScreenCloth", new Color(0.62f, 0.58f, 0.48f), 0.15f, "Prop");
            cloth.EnableKeyword("_EMISSION");
            cloth.SetColor("_EmissionColor", new Color(0.10f, 0.12f, 0.14f));
            Box("Projection_ScreenCloth", Cat("Decoration"), new Vector3(sb.center.x, sb.center.y, rP.yMax - 0.16f), new Vector3(sb.size.x * 0.92f, sb.size.y * 0.9f, 0.02f), cloth, false);
        }
        Put("MedicalEquipment", "Projection_FilmProjector", AS + "FilmProjector.prefab", new Vector3(31.0f, 0, 17.1f), 0f);
        foreach (float x in new[] { 28.6f, 29.5f, 30.4f }) Put("Furniture", "Projection_SeatFront_" + x.ToString("F1"), AS + "ChairSchool.prefab", new Vector3(x, 0, 19.9f), Rand(-8, 8));
        Put("Furniture", "Projection_SeatBack_1", AS + "ChairSchool.prefab", new Vector3(29.0f, 0, 18.5f), 12f);
        Put("Furniture", "Projection_SeatBack_2", AS + "ChairSchool.prefab", new Vector3(30.7f, 0, 18.4f), 100f);   // volcada hacia un lado
        projDesk = Wall("Furniture", "Projection_Desk", AS + "TableOffice.prefab", rP, 'W', 21.1f);
        Put("Furniture", "Projection_DeskChair", AS + "ChairOffice.prefab", new Vector3(27.5f, 0, 21.0f), 270f);
        Wall("Storage", "Projection_FilmShelf", AS + "BigShelf.prefab", rP, 'E', 19.0f);
        projTable = Wall("Furniture", "Projection_MonitorTable", AS + "TableWhite.prefab", rP, 'S', 27.3f);
        OnTop("MedicalEquipment", "Projection_Monitor_1", FH + "crt_television.prefab", projTable, -0.3f, 0f, 20f);
        OnTop("MedicalEquipment", "Projection_Monitor_2", FH + "crt_television.prefab", projTable, 0.3f, 0f, -15f);
        OnTop("Decoration", "Projection_ReelBox", AS + "Box_V1.prefab", projDesk, 0.3f, 0.2f, 70f);

        // --- C: cámara de pruebas. Una silla bajo la lámpara quirúrgica, vista desde el corredor (ventanas) y desde la galería.
        Put("MedicalEquipment", "Chamber_SubjectChair", AS + "ChairLeather.prefab", new Vector3(22.0f, 0, 18.0f), 180f);
        // Correas de sujeción: cuelgan de un riel metálico fijado a la pared este (antes flotaban en medio de la sala)
        Wall("MedicalEquipment", "Chamber_RestraintStraps", AS + "Suspension.prefab", rC, 'E', 16.0f, 1f, 1.2f);
        Box("Chamber_StrapRail", Cat("MedicalEquipment"), new Vector3(rC.xMax - 0.19f, 2.95f, 16.0f), new Vector3(0.09f, 0.07f, 1.8f), Mat("RailMetal", new Color(0.20f, 0.17f, 0.15f), 0.4f, "Metal", 0.6f), false);
        Box("Chamber_StrapRailBracket_1", Cat("MedicalEquipment"), new Vector3(rC.xMax - 0.15f, 2.95f, 15.25f), new Vector3(0.08f, 0.12f, 0.05f), Mat("RailMetal", new Color(0.20f, 0.17f, 0.15f), 0.4f, "Metal", 0.6f), false);
        Box("Chamber_StrapRailBracket_2", Cat("MedicalEquipment"), new Vector3(rC.xMax - 0.15f, 2.95f, 16.75f), new Vector3(0.08f, 0.12f, 0.05f), Mat("RailMetal", new Color(0.20f, 0.17f, 0.15f), 0.4f, "Metal", 0.6f), false);
        Put("MedicalEquipment", "Chamber_SurgicalLamp", FH + "surgical_lamp.prefab", new Vector3(23.4f, 0, 17.3f), 200f);
        Put("MedicalEquipment", "Chamber_Gurney", AS + "SurgeryWheelBad.prefab", new Vector3(24.7f, 0, 15.1f), 90f);
        chamberTrolley = Put("MedicalEquipment", "Chamber_InstrumentTrolley", FH + "instrument_trolley.prefab", new Vector3(24.4f, 0, 19.7f), 160f);
        Wall("Storage", "Chamber_MedRack", AS + "MedRack.prefab", rC, 'N', 19.1f);
        Put("Furniture", "Chamber_AbandonedWheelchair", AS + "WheelChair.prefab", new Vector3(19.1f, 0, 15.0f), 65f);
        // Galería (a 3,2 m): puesto del observador
        galleryDesk = Put("Furniture", "Gallery_ObserverDesk", AS + "TableOffice.prefab", new Vector3(18.6f, 0, 16.6f), 90f, 1f, WallH);
        Put("Furniture", "Gallery_ObserverChair", AS + "ChairOffice.prefab", new Vector3(19.35f, 0, 16.8f), 270f, 1f, WallH);
        OnTop("Decoration", "Gallery_OilLamp", FH + "oil_table_lamp.prefab", galleryDesk, 0f, -0.45f);
        Put("Furniture", "Gallery_SecondChair", AS + "ChairOffice.prefab", new Vector3(19.4f, 0, 15.2f), 250f, 1f, WallH);

        // --- L: laboratorio / cámara fría
        Wall("MedicalEquipment", "ColdLab_MorgueUnit", AS + "MorgueBox.prefab", rL, 'W', 18.85f, 0.6f);
        Wall("MedicalEquipment", "ColdLab_Tank", AS + "Tank.prefab", rL, 'N', 11.5f, 0.45f);
        labLocker = Wall("Storage", "ColdLab_CustodyLocker", AS + "CaseMetallic.prefab", rL, 'N', 17.1f);
        labBench = Put("Furniture", "ColdLab_Bench", AS + "TableWhite.prefab", new Vector3(14.6f, 0, 15.0f), 0f);
        GameObject sink = Wall("MedicalEquipment", "ColdLab_Sink", AS + "Sink_V1.prefab", rL, 'S', 11.6f);
        OnTop("MedicalEquipment", "ColdLab_Tap", AS + "Tap.prefab", sink, 0f, 0.28f);
        Wall("MedicalEquipment", "ColdLab_Fridge", AS + "Fridge.prefab", rL, 'S', 17.0f);
        Wall("Storage", "ColdLab_MedicineCabinet", FH + "medicine_cabinet.prefab", rL, 'W', 14.9f, 1f, 1.2f);
        OnTop("Decoration", "ColdLab_Kettle", AS + "Kettle.prefab", labBench, -0.4f, 0.1f);
        OnTop("Decoration", "ColdLab_Mug", AS + "Mug.prefab", labBench, 0.45f, -0.15f, 70f);

        // --- X: dirección de investigación (alfombra, madera oscura, un gabinete que parece un altar)
        GameObject carpet = Put("Decoration", "Office_Carpet", AS + "Carpet.prefab", new Vector3(11.0f, 0, 23.8f), 0f, 1.9f, 0.012f, false);
        officeDesk = Put("Furniture", "Office_DirectorDesk", AS + "TableOffice.prefab", new Vector3(11.0f, 0, 26.7f), 180f, 1.5f);
        Put("Furniture", "Office_DirectorChair", AS + "ChairOffice.prefab", new Vector3(11.0f, 0, 27.65f), 190f);
        Put("Furniture", "Office_Armchair_1", AS + "Sofa_Chair.prefab", new Vector3(9.2f, 0, 23.2f), 12f);
        Put("Furniture", "Office_Armchair_2", AS + "Sofa_Chair.prefab", new Vector3(10.5f, 0, 22.6f), -8f);
        GameObject chand = Put("Decoration", "Office_Chandelier", AS + "Chandelier.prefab", new Vector3(11.0f, 0, 24.0f), 0f, 0.6f, 0f, false);
        if (chand != null)
        {
            SnapY(chand, WallH, true);   // cuelga del techo; a escala 0,6 queda a ~1,9 m del suelo
            foreach (Collider cc in chand.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(cc);   // decorativa: no debe bloquear al jugador
        }
        officeAltar = Wall("Furniture", "Office_AltarCabinet", FH + "altar_cabinet.prefab", rX, 'E', 22.4f);
        OnTop("Decoration", "Office_Candelabrum", FH + "candelabrum.prefab", officeAltar, 0f, 0f);
        Wall("Decoration", "Office_WallMap", AS + "MapGroundFloor.prefab", rX, 'W', 23.6f, 1f, 0.9f, -90f);   // la lámina es fina en X: girada 90° salía de la pared como una repisa
        Wall("Storage", "Office_Bookshelf", AS + "BigShelf.prefab", rX, 'W', 26.6f);
        Wall("Storage", "Office_Cupboard", AS + "Cupboard.prefab", rX, 'N', 7.7f);
        Wall("Storage", "Office_FilingCabinet_1", FH + "filing_cabinet.prefab", rX, 'N', 13.7f);
        Wall("Storage", "Office_FilingCabinet_2", FH + "filing_cabinet.prefab", rX, 'N', 14.45f);
        Wall("Storage", "Office_EmptyStrongbox", AS + "SmallMetalicCase.prefab", rX, 'S', 7.6f);
        officeMonitor = OnTop("MedicalEquipment", "Office_Monitor", AS + "PC_Monitor.prefab", officeDesk, -0.8f, 0.05f, 150f);
        OnTop("Decoration", "Office_DeskLamp", FH + "oil_table_lamp.prefab", officeDesk, 0.9f, 0.2f);

        // --- V: vestíbulo de S-9
        mainDoorGo = Wall("Decoration", "Vestibule_SealedDoorS9", AS + "MainDoor.prefab", rV, 'N', 18.0f);
        vestBench = Wall("Furniture", "Vestibule_Bench", AS + "Bench.prefab", rV, 'W', 22.95f);   // fuera del hueco de paso (z 24-26)
        GameObject candleTable = Put("Furniture", "Vestibule_CandleTable", AS + "TableSmall.prefab", new Vector3(19.3f, 0, 25.4f), 0f);
        OnTop("Decoration", "Vestibule_Candelabrum", FH + "candelabrum.prefab", candleTable, 0f, 0f);
    }

    static GameObject receptionBench;

    // ---------------------------------------------------------------- Papeles caídos (rastro de una evacuación apresurada)

    static void BuildPaperTrails()
    {
        void Scatter(string room, Rect r, int count)
        {
            for (int i = 0; i < count; i++)
            {
                GameObject p = Spawn(AS + "Paper.prefab", EnvDetails);
                if (p == null) return;
                p.name = room + "_FloorPaper_" + (i + 1).ToString("00");
                p.transform.rotation = Quaternion.Euler(0, Rand(0, 360), 0);
                SnapXZ(p, Rand(r.xMin + 0.6f, r.xMax - 0.6f), Rand(r.yMin + 0.6f, r.yMax - 0.6f));
                SnapY(p, 0.012f + i * 0.0012f, false);
            }
        }
        Scatter("Reception", rR, 9);
        Scatter("ObservationCorridor", new Rect(18, 12, 10, 2), 4);
        Scatter("ProjectionRoom", rP, 6);
        Scatter("TestChamber", rC, 6);
        Scatter("ColdLab", rL, 5);
        Scatter("DirectorOffice", rX, 12);
    }

    // ---------------------------------------------------------------- Sala de dibujos del paciente 214

    static void BuildDrawingRoom()
    {
        Wall("Furniture", "Drawing_PatientBed", FH + "hospital_bed.prefab", rD, 'W', 15.1f);
        drawBedside = Wall("Furniture", "Drawing_Bedside", AS + "Table_Bed.prefab", rD, 'W', 16.3f);
        drawDesk = Wall("Furniture", "Drawing_Desk", AS + "TableOffice.prefab", rD, 'N', 7.0f);
        // La silla da la espalda a la puerta: quien dibujaba miraba solo la pared
        Put("Furniture", "Drawing_DeskChair", AS + "ChairSchool.prefab", new Vector3(7.0f, 0, 18.5f), 0f);
        OnTop("Decoration", "Drawing_Lantern", FH + "hurricane_lantern.prefab", drawDesk, 0.55f, 0.1f);
        Put("Furniture", "Drawing_Mattress", AS + "Mattress.prefab", new Vector3(8.7f, 0, 15.2f), 80f);

        // Hojas clavadas en las paredes: (tipo, pared, posición a lo largo, altura, ancho, giro)
        var sheets = new (string kind, char wall, float along, float y, float w, float roll)[]
        {
            ("DoorS9", 'N', 5.0f, 1.9f, 0.40f, -4f), ("DoorS9", 'N', 5.6f, 1.2f, 0.34f, 6f), ("Spiral", 'N', 6.75f, 1.75f, 0.42f, 3f),
            ("Eyes", 'N', 8.0f, 1.9f, 0.38f, -3f), ("DoorS9", 'N', 8.7f, 1.3f, 0.36f, 5f), ("RitualCircle", 'N', 9.4f, 1.8f, 0.40f, -6f),
            ("Faces", 'W', 14.9f, 2.05f, 0.40f, 2f), ("TwoFigures", 'W', 18.6f, 1.9f, 0.40f, -5f), ("Tally0307", 'W', 19.3f, 1.2f, 0.34f, 4f),
            ("TwoFigures", 'S', 5.0f, 1.8f, 0.38f, 5f), ("Corridor", 'S', 5.7f, 1.3f, 0.34f, -4f), ("Tally0307", 'S', 8.9f, 1.9f, 0.40f, 3f),
            ("Eyes", 'S', 9.5f, 1.25f, 0.34f, -7f), ("Faces", 'E', 14.5f, 1.8f, 0.38f, 6f), ("Corridor", 'E', 15.4f, 1.3f, 0.34f, -3f),
            ("RitualCircle", 'E', 18.5f, 1.9f, 0.40f, 4f),
        };
        var pins = new List<Vector3>();
        foreach (var s in sheets)
        {
            Rect r = rD;
            Vector3 pos = WallPoint(r, s.wall, s.along, s.y);
            PinSheet(s.kind, pos, Opposite(s.wall), s.w, s.roll);
            pins.Add(pos);
        }
        // Hilo rojo entre hojas (las que enlazan la puerta con la hora)
        void Link(int a, int b) { if (a < pins.Count && b < pins.Count) Thread(pins[a], pins[b]); }
        Link(0, 2); Link(2, 5); Link(3, 5); Link(3, 4); Link(6, 8); Link(7, 8); Link(9, 11); Link(10, 12); Link(13, 15); Link(14, 16);

        // Garabatos grandes sobre la pared
        WallDecal('D', "Scrawl_Spiral", Level03Art.WallScrawl("Spiral"), WallPoint(rD, 'W', 17.6f, 1.55f), 'E', 1.9f, new Color(1, 1, 1, 1));
        WallDecal('D', "Scrawl_Tally", Level03Art.WallScrawl("Tally"), WallPoint(rD, 'S', 7.1f, 1.5f), 'N', 1.8f, new Color(1, 1, 1, 1));
        WallDecal('D', "Scrawl_Hands", Level03Art.WallScrawl("Hands"), WallPoint(rD, 'E', 19.25f, 0.95f), 'W', 0.8f, new Color(1, 1, 1, 1));
        WallDecal('D', "Scrawl_NotTheSame", Level03Art.WallScrawl("NoEsElMismo"), WallPoint(rD, 'N', 7.2f, 1.1f), 'S', 1.8f, new Color(1, 1, 1, 1));

        // Hojas por el suelo (el paciente seguía dibujando cuando se llevaron todo)
        foreach (string kind in new[] { "Spiral", "Faces", "Eyes", "TwoFigures", "Corridor" })
            FloorSheet(kind, new Vector3(Rand(rD.xMin + 1f, rD.xMax - 1f), 0.014f + Rand(0, 0.004f), Rand(rD.yMin + 1f, rD.yMax - 1.5f)));
    }

    static Material PaperMat(string kind) => DecalMat("Sheet_" + kind, Level03Art.Sheet(kind), Color.white, 0.05f);

    static void PinSheet(string kind, Vector3 pos, char facing, float width, float roll)
    {
        float height = width * 704f / 512f;
        Quad("Drawing_" + kind, RoomDetails('D'), pos + Dir(facing) * Rand(0.0f, 0.006f), Facing(Dir(facing)) * Quaternion.Euler(0, 0, roll), new Vector2(width, height), PaperMat(kind));
    }

    static void FloorSheet(string kind, Vector3 pos)
    {
        Quad("FloorDrawing_" + kind, EnvDetails, pos, Quaternion.Euler(90, Rand(0, 360), 0), new Vector2(0.34f, 0.468f), PaperMat(kind));
    }

    static GameObject Quad(string name, Transform parent, Vector3 pos, Quaternion rot, Vector2 size, Material mat)
    {
        GameObject q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name;
        Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(parent, false);
        q.transform.SetPositionAndRotation(pos, rot);
        q.transform.localScale = new Vector3(size.x, size.y, 1f);
        q.GetComponent<Renderer>().sharedMaterial = mat;
        return q;
    }

    // Garabato o rótulo grande sobre una pared (cuadrado de "size" metros)
    static void WallDecal(char room, string name, Texture2D tex, Vector3 pos, char facing, float size, Color tint)
    {
        Quad(name, RoomDetails(room), pos, Facing(Dir(facing)), new Vector2(size, size * tex.height / (float)tex.width), DecalMat(name, tex, tint, 0.05f));
    }

    static void Thread(Vector3 a, Vector3 b)
    {
        GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        t.name = "Drawing_RedThread";
        Object.DestroyImmediate(t.GetComponent<Collider>());
        t.transform.SetParent(RoomDetails('D'), false);
        Vector3 mid = (a + b) / 2f;
        Vector3 d = b - a;
        // el hilo se separa un poco de la pared, hacia la sala
        mid += (new Vector3(rD.center.x, mid.y, rD.center.y) - mid).normalized * 0.02f;
        t.transform.position = mid;
        t.transform.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
        t.transform.localScale = new Vector3(0.006f, d.magnitude / 2f, 0.006f);
        t.GetComponent<Renderer>().sharedMaterial = Mat("RedThread", new Color(0.45f, 0.04f, 0.03f), 0.2f, "Prop");
    }

    // ---------------------------------------------------------------- Rótulos (pintados a mano sobre la pared)

    static readonly Color SignWhite = new Color(0.86f, 0.82f, 0.7f);
    static readonly Color SignDark = new Color(0.12f, 0.08f, 0.06f);
    static readonly Color SignRust = new Color(0.55f, 0.18f, 0.08f);

    static void Sign(char room, string text, Rect r, char wall, float along, float y, float width, Color color, int drip = 5, string group = null)
    {
        Texture2D tex = Level03Art.SignTexture(text, drip);
        float height = width * tex.height / (float)tex.width;
        Vector3 pos = WallPoint(r, wall, along, y);
        Quad("Sign_" + System.Text.RegularExpressions.Regex.Replace(text, "[^A-Za-z0-9]+", "_"), RoomDetails(room), pos, Facing(Dir(Opposite(wall))), new Vector2(width, height), DecalMat("Sign_" + text, tex, color, 0.1f));
    }

    static void BuildSigns()
    {
        Sign('M', "INVESTIGACION", rM, 'S', 31.55f, 2.35f, 2.0f, SignWhite, 0);
        Sign('M', "CONDUCTUAL", rM, 'S', 31.55f, 2.0f, 1.75f, SignWhite, 0);
        Sign('R', "RECEPCION", rR, 'S', 25.0f, 2.55f, 2.2f, SignWhite, 0);
        Sign('R', "TARJETA OBLIGATORIA", rR, 'N', 24.6f, 2.3f, 2.2f, SignRust, 3);
        Sign('R', "AREA CLAUSURADA", rR, 'W', 5.0f, 2.45f, 1.5f, SignRust, 4);
        Sign('O', "OBSERVACION", rOh, 'S', 21.6f, 2.35f, 2.1f, SignWhite, 0);
        Sign('O', "SECTOR CLAUSURADO", rOh, 'S', 19.6f, 1.55f, 1.2f, SignRust, 4);
        WallDecal('O', "Scrawl_NotTheRoof", Level03Art.WallScrawl("NoFueElTecho"), WallPoint(rOv, 'W', 9.6f, 1.6f), 'E', 1.9f, Color.white);
        Sign('P', "PROYECCION", rP, 'S', 31.0f, 2.5f, 1.5f, SignWhite, 0);
        Sign('C', "CAMARA DE PRUEBAS", rC, 'N', 23.2f, 4.85f, 3.4f, SignWhite, 0);
        Sign('C', "PROHIBIDO RESPONDER", rC, 'N', 23.2f, 4.15f, 2.6f, SignRust, 4);
        Sign('L', "LABORATORIO", rL, 'S', 14.6f, 2.55f, 1.9f, SignWhite, 0);
        Sign('L', "CAMARA FRIA", rL, 'N', 11.0f, 2.45f, 1.5f, SignWhite, 0);
        Sign('L', "CUSTODIA", rL, 'N', 17.0f, 2.8f, 1.1f, SignWhite, 0);
        Sign('L', "HORA DE SESION", rL, 'N', 17.0f, 2.48f, 1.4f, SignWhite, 0);
        Sign('D', "PACIENTE 214", rD, 'S', 6.0f, 2.45f, 1.5f, SignRust, 0);
        Sign('X', "DIRECCION", rX, 'S', 8.2f, 2.6f, 2.0f, SignWhite, 0);
        Sign('V', "S-9", rV, 'E', 27.25f, 1.8f, 0.6f, SignRust, 2);
        WallDecal('V', "Scrawl_NotFromThisSide", Level03Art.WallScrawl("NoDesdeEsteLado"), WallPoint(rV, 'E', 25.0f, 1.45f), 'W', 3.4f, Color.white);
    }

    // ---------------------------------------------------------------- Detalles de suelo y escombros

    static void BuildDetails()
    {
        Texture2D stain = Level03Art.Stain();
        Material stainMat = DecalMat("Stain", stain, new Color(0.9f, 0.8f, 0.7f, 1f), 0.15f);
        Material markMat = DecalMat("FloorMark", Level03Art.FloorMark(), Color.white, 0.2f);

        void StainAt(string name, float x, float z, float s) =>
            Quad(name, EnvDetails, new Vector3(x, 0.013f, z), Quaternion.Euler(90, Rand(0, 360), 0), new Vector2(s, s * Rand(0.6f, 0.9f)), stainMat);
        StainAt("Reception_Stain_1", 24.5f, 4.4f, 1.8f);
        StainAt("Reception_Stain_2", 28.0f, 1.4f, 1.2f);
        StainAt("Corridor_Stain_1", 22.0f, 13.0f, 1.2f);
        StainAt("Corridor_Stain_2", 29.0f, 9.0f, 1.3f);
        StainAt("Projection_Stain", 28.0f, 21.0f, 1.6f);
        StainAt("Chamber_Stain_1", 22.0f, 17.4f, 2.8f);
        StainAt("Chamber_Stain_2", 20.5f, 20.0f, 1.4f);
        StainAt("Lab_Stain_1", 13.8f, 18.4f, 1.6f);
        StainAt("Lab_Stain_2", 11.2f, 15.6f, 1.2f);
        StainAt("Drawing_Stain", 6.5f, 16.0f, 2.0f);
        StainAt("Office_Stain", 13.5f, 22.5f, 1.8f);

        // Marcas secas de doble círculo: bajo la silla de la cámara de pruebas y ante la puerta de S-9
        Quad("Chamber_FloorMark", EnvDetails, new Vector3(22.0f, 0.016f, 18.0f), Quaternion.Euler(90, 20, 0), new Vector2(3.6f, 3.6f), markMat);
        Quad("Vestibule_FloorMark", EnvDetails, new Vector3(18.0f, 0.016f, 25.2f), Quaternion.Euler(90, 70, 0), new Vector2(3.3f, 3.3f), markMat);

        BuildRubble();
    }

    // TEMP: derrumbe del fondo del corredor (losas de hormigón y cascotes con la textura del nivel). Reemplazar por un modelo de escombros.
    static void BuildRubble()
    {
        Transform p = RoomDetails('O');
        Material conc = ConcreteMat(ZCorridor, "Rubble", ZCorridor.plaster * 1.1f);
        // Losa caída, apoyada en diagonal contra el fondo
        GameObject slab = Box("TEMP_FallenSlab", p, new Vector3(18.9f, 1.0f, 13.0f), new Vector3(2.1f, 0.3f, 2.1f), conc, true);
        slab.transform.rotation = Quaternion.Euler(0, 0, 58f);
        GameObject slab2 = Box("TEMP_FallenSlab_2", p, new Vector3(19.7f, 0.5f, 12.5f), new Vector3(1.4f, 0.25f, 1.2f), conc, true);
        slab2.transform.rotation = Quaternion.Euler(14f, 30f, 24f);
        for (int i = 0; i < 16; i++)
        {
            Vector3 size = new Vector3(Rand(0.12f, 0.5f), Rand(0.1f, 0.35f), Rand(0.12f, 0.45f));
            GameObject c = Box("TEMP_Rubble_" + (i + 1).ToString("00"), p, new Vector3(Rand(18.3f, 21.2f), size.y / 2f, Rand(12.2f, 13.8f)), size, conc, i < 6);
            c.transform.rotation = Quaternion.Euler(Rand(-20, 20), Rand(0, 360), Rand(-20, 20));
        }
        // Tubería rota colgando del techo, sobre el derrumbe
        GameObject pipe = Spawn(AS + "Pipe.prefab", p);
        if (pipe != null)
        {
            pipe.name = "BrokenPipe";
            pipe.transform.rotation = Quaternion.Euler(0, 0, 18f);
            SnapXZ(pipe, 20.4f, 13.4f);
            SnapY(pipe, WallH, true);
        }
    }
}
