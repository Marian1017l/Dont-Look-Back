using UnityEditor;
using UnityEngine;
using static BuildKit;

// Level02Builder, parte 3: progresión y objetos interactivos.
// Cadena de progresión (solo con Interactable, Door, Document e Inventory ya existentes):
//   fusible (cofre del taller) -> tablero de la sala eléctrica (restaura la luz y abre la reja de aislamiento)
//   -> tarjeta del archivo (celda 3) -> reja del archivo -> llave del montacargas (escritorio) -> montacargas -> salida del nivel.
public static partial class Level02Builder
{
    static Door gateIsolation, gateArchive, elevatorDoor;
    static Interactable fuseInteract;

    static void BuildGameplayObjects()
    {
        interactablesGroup = T("Interactables", gameplayRoot);
        pickups = T("Pickups", interactablesGroup);
        keysGroup = T("Keys", gameplayRoot);
        documentsGroup = T("Documents", gameplayRoot);
        doorsGroup = T("Doors", gameplayRoot);
        exitGroup = T("Level_Exit", gameplayRoot);

        BuildGates();
        BuildCellDoors();
        BuildFuseBox();
        BuildKeys();
        BuildPickups();
        BuildDocuments();
        BuildElevator();
    }

    // ---------------------------------------------------------------- Rejas y tablero

    static Door Gate(string name, int c, int r, char d, string required, string prompt, string locked)
    {
        if (!edgeMap.TryGetValue(Canon(c, r, d), out Edge e)) { Debug.LogWarning("Level02Builder: no hay hueco para " + name); return null; }
        GameObject gate = Spawn(FH + "barred_cell_door.prefab", doorsGroup);
        if (gate == null) return null;
        gate.name = name;
        gate.transform.rotation = Quaternion.Euler(0, e.v ? 90f : 0f, 0);
        gate.transform.localScale = new Vector3(U / 1.08f, 2.4f / 2.16f, 1f);   // se ajusta al hueco de 2 m bajo la viga
        SnapXZ(gate, e.Mid.x, e.Mid.z);
        SnapY(gate, 0f, false);
        EnsureCollider(gate);

        Interactable it = gate.AddComponent<Interactable>();
        it.prompt = prompt;
        it.requiredItem = required;
        it.lockedMessage = locked;
        Door slide = gate.AddComponent<Door>();
        slide.openOffset = new Vector3(0, 2.6f, 0);
        Hook(it.onInteract, slide.Open);
        return slide;
    }

    static void BuildGates()
    {
        // Sin corriente: la reja no se abre con nada; el tablero general la libera
        gateIsolation = Gate("Gate_Isolation", 15, 12, 'S', "_sin_corriente", "Reja eléctrica", "La reja está sin corriente. El motor no responde");
        gateArchive = Gate("Gate_Archive", 22, 8, 'S', "tarjeta_archivo", "Abrir reja del archivo", "Un lector de tarjetas. Necesita una tarjeta de acceso");
    }

    // TEMP: puertas metálicas pesadas de las celdas, con ranura de observación a la altura de los ojos.
    // Cerradas (la 2 y la 5, se ve el interior por la ranura) o entreabiertas hacia dentro. Reemplazar por un modelo de puerta de aislamiento.
    static void BuildCellDoors()
    {
        Material metal = Tinted(AssetDatabase.LoadAssetAtPath<Material>(FHMat + "hospital_floor_slab_Exposed_slab_concrete.mat"), ZIsolation, "DoorMetal", new Color(0.50f, 0.58f, 0.60f), 0.35f, 0.55f);
        float[] angle = { 86f, 0f, 78f, 88f, 0f };   // 0 = cerrada; las abiertas dejan >1 m de paso
        float z = (Rows - 14) * U;                   // pared entre el pasillo (fila 13) y las celdas (fila 14)
        for (int i = 0; i < 5; i++)
        {
            float cx = XC(10 + 2 * i) + U / 2f;      // centro del hueco de puerta
            GameObject root = new GameObject("TEMP_Door_Cell_" + (i + 1));
            root.transform.SetParent(doorsGroup, false);
            root.transform.position = new Vector3(cx - 0.66f, 0, z);
            root.transform.rotation = Quaternion.Euler(0, angle[i], 0);

            void Part(string n, Vector3 local, Vector3 size)
            {
                GameObject p = Box(n, root.transform, Vector3.zero, size, metal, true);
                p.transform.localPosition = local;
                p.transform.localRotation = Quaternion.identity;
            }
            Part("Panel_Lower", new Vector3(0.66f, 0.70f, 0f), new Vector3(1.30f, 1.40f, 0.07f));
            Part("Panel_Upper", new Vector3(0.66f, 2.04f, 0f), new Vector3(1.30f, 0.62f, 0.07f));
            Part("Panel_SlotLeft", new Vector3(0.28f, 1.565f, 0f), new Vector3(0.50f, 0.33f, 0.07f));
            Part("Panel_SlotRight", new Vector3(1.04f, 1.565f, 0f), new Vector3(0.50f, 0.33f, 0.07f));
            Part("Bolt", new Vector3(1.15f, 1.0f, -0.06f), new Vector3(0.22f, 0.04f, 0.04f));
        }
    }

    static void BuildFuseBox()
    {
        if (panelMain == null) return;
        panelMain.name = "FuseBox_Main";
        panelMain.transform.SetParent(interactablesGroup, true);
        EnsureCollider(panelMain);
        fuseInteract = panelMain.AddComponent<Interactable>();
        fuseInteract.prompt = "Colocar fusible en el tablero";
        fuseInteract.requiredItem = "fusible";
        fuseInteract.lockedMessage = "El tablero está muerto. Falta un fusible de 30 A";
        if (gateIsolation != null) Hook(fuseInteract.onInteract, gateIsolation.Open);
        // La iluminación restaurable se engancha al final de BuildLighting (WirePower)
    }

    // ---------------------------------------------------------------- Objetos clave y recogibles

    static void BuildKeys()
    {
        // Fusible: cofre de herramientas del taller (TEMP: cilindro de porcelana con casquillos metálicos)
        if (chestMaint != null)
        {
            Vector3 top = TopOf(chestMaint);
            Vector3 pos = new Vector3(top.x, top.y, top.z);
            GameObject fuse = new GameObject("Fuse_30A");
            fuse.transform.SetParent(keysGroup, false);
            fuse.transform.position = pos + Vector3.up * 0.03f;
            Material porcelain = Mat("Porcelain", new Color(0.42f, 0.33f, 0.26f), 0.7f, "Metal", 0f);
            Material copper = Mat("Copper", new Color(0.55f, 0.32f, 0.18f), 0.5f, "Metal", 0.8f);
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            body.name = "TEMP_FuseBody";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(fuse.transform, false);
            body.transform.localRotation = Quaternion.Euler(0, 0, 90);
            body.transform.localScale = new Vector3(0.03f, 0.05f, 0.03f);
            body.GetComponent<Renderer>().sharedMaterial = porcelain;
            foreach (float s in new[] { -1f, 1f })
            {
                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                cap.name = "TEMP_FuseCap";
                Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.transform.SetParent(fuse.transform, false);
                cap.transform.localPosition = new Vector3(s * 0.055f, 0, 0);
                cap.transform.localRotation = Quaternion.Euler(0, 0, 90);
                cap.transform.localScale = new Vector3(0.034f, 0.012f, 0.034f);
                cap.GetComponent<Renderer>().sharedMaterial = copper;
            }
            BoxCollider col = fuse.AddComponent<BoxCollider>();
            col.size = new Vector3(0.18f, 0.08f, 0.08f);
            Interactable it = fuse.AddComponent<Interactable>();
            it.prompt = "Coger fusible";
            it.giveItem = "fusible";
            it.destroyAfterUse = true;
            Glint(fuse);
            reserved.Add(new Vector2(pos.x, pos.z));
        }

        // Tarjeta de acceso del archivo: sobre la cama de la celda 3 (TEMP: tarjeta de plástico con banda magnética)
        if (bedCellK != null)
        {
            Vector3 top = TopOf(bedCellK);
            GameObject card = new GameObject("AccessCard_Archive");
            card.transform.SetParent(keysGroup, false);
            card.transform.position = top + Vector3.up * 0.006f;
            card.transform.rotation = Quaternion.Euler(0, 35f, 0);
            Box("TEMP_CardBody", card.transform, Vector3.zero, new Vector3(0.12f, 0.008f, 0.075f), Mat("CardPlastic", new Color(0.55f, 0.52f, 0.42f), 0.4f, "Metal"), false).transform.localPosition = Vector3.zero;
            Box("TEMP_CardStripe", card.transform, Vector3.zero, new Vector3(0.12f, 0.009f, 0.014f), Mat("CardStripe", new Color(0.05f, 0.05f, 0.05f), 0.5f, "Metal"), false).transform.localPosition = new Vector3(0, 0, 0.022f);
            BoxCollider col = card.AddComponent<BoxCollider>();
            col.size = new Vector3(0.18f, 0.05f, 0.12f);
            Interactable it = card.AddComponent<Interactable>();
            it.prompt = "Coger tarjeta de acceso";
            it.giveItem = "tarjeta_archivo";
            it.destroyAfterUse = true;
            Glint(card);
            reserved.Add(new Vector2(top.x, top.z));
        }

        // Llave del montacargas: cajón del escritorio del archivo
        if (deskArchive != null)
        {
            Vector3 top = TopOf(deskArchive);
            GameObject key = Item(SP + "key1.prefab", "Key_Freight_Elevator", new Vector3(top.x + 0.45f, top.y, top.z - 0.2f), 0.16f, "Coger llave del montacargas", keysGroup);
            if (key != null) key.GetComponent<Interactable>().giveItem = "llave_montacargas";
        }
    }

    static void BuildPickups()
    {
        Battery(FreePos(W(6, 8, 0.5f, 0.3f)), "Battery_Maintenance");
        Battery(FreePos(W(11, 10, -0.5f, 0.5f)), "Battery_Storage");
        Battery(FreePos(W(16, 13, 0.3f, 0f)), "Battery_Isolation");
        Battery(FreePos(W(23, 6, 0.8f, 0.7f)), "Battery_Archive");
        Bandage(FreePos(W(0, 7, 0.3f, 0.3f)), "Bandage_Transit");
        Bandage(FreePos(W(17, 15, 0.5f, 0.5f)), "Bandage_Isolation");
        Bandage(FreePos(W(23, 6, -0.6f, 0.3f)), "Bandage_Archive");
    }

    static void Battery(Vector3 pos, string name)
    {
        GameObject go = Item(SP + "battery big.prefab", name, pos, 0.14f, "Recoger batería", pickups);
        if (go != null) Hook(go.GetComponent<Interactable>().onInteract, playerFlashlight.AddBattery);
    }

    static void Bandage(Vector3 pos, string name)
    {
        GameObject go = Item(SP + "bandage.prefab", name, pos, 0.2f, "Usar vendaje", pickups);
        if (go != null) Hook(go.GetComponent<Interactable>().onInteract, playerHealth.UseBandage);
    }

    // Busca un punto libre (sin muebles ni pasos) cerca del deseado
    static Vector3 FreePos(Vector3 desired)
    {
        char letter = CharAt(desired.x, desired.z);
        for (int i = 0; i < 40; i++)
        {
            Vector3 p = desired + new Vector3(Rand(-1f, 1f) * i * 0.06f, 0, Rand(-1f, 1f) * i * 0.06f);
            Rect r = new Rect(p.x - 0.2f, p.z - 0.2f, 0.4f, 0.4f);
            if (FitsRoom(r, letter) && IsFree(r)) return p;
        }
        return desired;
    }

    // Objeto recogible: se escala a un tamaño realista, se apoya en "pos", lleva un Interactable y un destello tenue
    static GameObject Item(string path, string name, Vector3 pos, float size, string prompt, Transform parent)
    {
        GameObject go = Spawn(path, parent);
        if (go == null) return null;
        go.name = name;
        Bounds b = GetBounds(go);
        float longest = Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z));
        if (longest > 0.0001f) go.transform.localScale *= size / longest;
        SnapXZ(go, pos.x, pos.z);
        SnapY(go, pos.y, false);
        AddInteractable(go, prompt, true);
        reserved.Add(new Vector2(pos.x, pos.z));
        Glint(go);
        return go;
    }

    static Interactable AddInteractable(GameObject go, string prompt, bool destroy)
    {
        EnsureCollider(go);
        Interactable it = go.AddComponent<Interactable>();
        it.prompt = prompt;
        it.destroyAfterUse = destroy;
        return it;
    }

    static void Glint(GameObject go)
    {
        GameObject glint = new GameObject("Glint");
        glint.transform.SetParent(go.transform, true);
        glint.transform.position = GetBounds(go).center + Vector3.up * 0.15f;
        Light l = glint.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 1.8f;
        l.intensity = 0.8f;
        l.color = new Color(1f, 0.92f, 0.75f);
        l.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- Documentos

    static void Note(Vector3 pos, string title, DocumentStyle style, string reference, string date, string text, string author = "")
    {
        GameObject go = Item(AS + "Paper.prefab", "Note_" + System.Text.RegularExpressions.Regex.Replace(title, "[^A-Za-z0-9]+", "_"), pos, 0.3f, "Leer", documentsGroup);
        if (go == null) return;
        Interactable it = go.GetComponent<Interactable>();
        it.destroyAfterUse = false;
        Document doc = go.AddComponent<Document>();
        doc.title = title; doc.style = style; doc.reference = reference; doc.date = date; doc.author = author; doc.text = text;
        Hook(it.onInteract, doc.Read);
    }

    static void BuildDocuments()
    {
        if (deskMaint != null)
            Note(TopOf(deskMaint) + new Vector3(-0.45f, 0, 0.2f), "Avería en el tablero general", DocumentStyle.StaffReport, "MNT-0974", "14/11/1993",
                "Quinta avería del mes en el tablero general. Los fusibles de 30 A se funden sin carga conectada.\n\nDejé uno de repuesto en el cofre de herramientas de este taller. El tablero está en la sala eléctrica, al final del pasillo de servicio.\n\nAnoche, mientras lo revisaba, alguien me llamó por mi nombre desde el pasillo de aislamiento. Era la voz de mi hermano. Mi hermano murió en 1989.\n\nNo pienso volver a bajar de noche.", "J. Ortega, Mantenimiento");

        if (deskElectric != null)
            Note(TopOf(deskElectric) + new Vector3(0.45f, 0, 0.1f), "Registro de cortes de energía", DocumentStyle.StaffReport, "ELEC-93", "01/12/1993",
                "Cortes totales de suministro:\n\n18/11  ·  03:07  ·  4 min\n25/11  ·  03:07  ·  4 min\n02/12  ·  03:07  ·  4 min\n\nCausa: no consta.\n\nLa hora se repite en todos los registros. Los relojes de la planta se detienen a las 03:07 mientras dura el corte y se reanudan solos.\n\nSolicito una revisión externa.", "Mantenimiento");

        // Celda 1: nota de paciente (escrita a mano, desesperada)
        Note(FreePos(W(10, 14, 0.6f, -0.4f)) + Vector3.up * 0.0f, "Nota de la celda 1", DocumentStyle.PatientNote, "", "",
            "No me creen cuando digo que hay dos respirando en mi celda.\n\nUno soy yo. El otro se parece tanto a mí que ya no sé cuál se queda cuando cierran la puerta.\n\nSi alguien se te parece demasiado, no le abras. No le hables. No le preguntes cómo se llama.");

        if (surgeryTable != null)
            Note(TopOf(surgeryTable) + new Vector3(0, 0, 0.0f), "Cambio de conducta: paciente 214", DocumentStyle.MedicalRecord, "0214-B/2", "12/11/1993",
                "Hasta el 11/11 el paciente colaboraba y comía con normalidad. Desde el 12/11 no reconoce su propia firma ni sus gestos al comer, y afirma que «el que está en la cama no soy yo».\n\nEl turno de noche asegura haberlo visto en dos salas a la vez.\n\nSe traslada a aislamiento por precaución. No se requiere medicación adicional.", "Dr. A. Salas");

        if (trolleyIso != null)
            Note(TopOf(trolleyIso), "Parte de enfermería", DocumentStyle.StaffReport, "ENF-0318", "29/11/1993",
                "Tres agresiones entre pacientes en una semana. En las tres, el agredido declara que el agresor «era yo mismo». Ninguno recuerda un rostro distinto al suyo.\n\nSe prohíben los traslados nocturnos sin acompañante. Las puertas de aislamiento permanecerán cerradas con llave desde fuera.\n\nNo abra ninguna puerta si alguien llama desde dentro usando su nombre.", "Enf. M. Reyes");

        if (deskArchive != null)
            Note(TopOf(deskArchive) + new Vector3(-0.1f, 0, -0.35f), "Supervisión especial: paciente 214", DocumentStyle.MedicalRecord, "0214-A", "03/12/1993",
                "Los dibujos del paciente coinciden: una sala cerrada en el sótano profundo, nivel S-9, y una figura que repite lo que hacen los demás.\n\nEl paciente describe un ritual que «la voz» le enseñó para que alguien responda.\n\nResolución 12/93: la sala S-9 debe permanecer cerrada. El acceso por montacargas queda restringido a Dirección.", "Archivo Central");

        if (deskArchive2 != null)
            Note(TopOf(deskArchive2), "Incidente nocturno del 02/12", DocumentStyle.StaffReport, "INC-1202", "03/12/1993",
                "Durante el corte de las 03:07, el turno de enfermería pasó de seis a siete personas. Nadie recuerda haber contratado a la séptima.\n\nLa enfermera jefe Reyes ordenó cerrar con llave todas las puertas «para que no salgan» y recogió las copias.\n\nSe recomienda no responder a quien pida que lo dejen salir.", "Jefatura de Seguridad");

        if (boxFinal2 != null)
            Note(TopOf(boxFinal2), "Montacargas fuera de servicio", DocumentStyle.StaffReport, "MNT-1010", "05/12/1993",
                "El montacargas al nivel inferior queda fuera de servicio por orden de Dirección. La llave se custodia en el archivo.\n\nDurante las noches se ha puesto en marcha solo. Quien lo ha oído subir describe pasos dentro de la cabina.", "Dirección");

        if (boxFinal != null)
            Note(TopOf(boxFinal), "No bajes", DocumentStyle.PatientNote, "", "",
                "No bajes.\n\nNo es el mismo que sube.\n\nYo bajé una vez y volví. O eso me dijeron. Ahora nadie me mira a los ojos.");
    }

    // ---------------------------------------------------------------- Montacargas y salida del nivel

    static void BuildElevator()
    {
        Transform g = PropsGroup('G');
        // Cabina contra la pared este de la zona final, con la puerta mirando al oeste (hacia el archivo)
        elevatorGo = Place(AS + "Elevator.prefab", g, new Vector3(54.18f, 0, 18f), 270f, 1f, 0f, false, true, false);   // la cabina ya trae su MeshCollider
        if (elevatorGo == null) return;
        elevatorGo.name = "Elevator_Freight";
        elevatorGo.transform.SetParent(exitGroup, true);
        Bounds cab = GetBounds(elevatorGo);

        GameObject door = Spawn(AS + "Elevator_Door.prefab", doorsGroup);
        if (door != null)
        {
            door.name = "Elevator_Door";
            door.transform.rotation = Quaternion.Euler(0, 270f, 0);
            SnapXZ(door, cab.min.x + 0.05f, cab.center.z);
            SnapY(door, 0f, false);
            EnsureCollider(door);
            Interactable it = door.AddComponent<Interactable>();
            it.prompt = "Abrir el montacargas";
            it.requiredItem = "llave_montacargas";
            it.lockedMessage = "El panel pide una llave. «MONTACARGAS · NIVEL INFERIOR»";
            elevatorDoor = door.AddComponent<Door>();
            elevatorDoor.openOffset = new Vector3(0, 0, 2.4f);
            Hook(it.onInteract, elevatorDoor.Open);
        }

        // Salida: la cabina. Con el siguiente nivel aún sin escena, LevelExit muestra la pantalla final (R reinicia).
        GameObject trigger = new GameObject("LevelExit");
        trigger.transform.SetParent(exitGroup, false);
        trigger.transform.position = new Vector3(cab.center.x, 1.2f, cab.center.z);
        BoxCollider box = trigger.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(Mathf.Max(0.8f, cab.size.x - 0.8f), 2.2f, Mathf.Max(0.8f, cab.size.z - 0.8f));
        LevelExit exit = trigger.AddComponent<LevelExit>();
        exit.nextScene = "Level03_ResearchWing";   // Nivel 3: ala de investigación (Level03Builder)
        exit.message = "Fin del nivel 2";

        reserved.Add(new Vector2(cab.min.x - 0.8f, cab.center.z));
    }
}
