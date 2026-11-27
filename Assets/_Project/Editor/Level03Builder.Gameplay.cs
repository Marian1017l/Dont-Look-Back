using UnityEditor;
using UnityEngine;
using static BuildKit;

// Level03Builder, parte 3: progresión, objetos interactivos y documentos.
// Cadena de progresión (solo con Interactable, Door, Padlock, Document, Inventory y LevelExit ya existentes):
//   tarjeta de investigación (escritorio de la sala de proyección) -> puerta del laboratorio (lector de tarjeta)
//   -> casillero de custodia con candado "0307" (hora fija de las sesiones: reloj de recepción + registro de sesiones + hoja de custodia)
//   -> llave de Dirección -> puerta de Dirección -> llave de S-9 (escritorio) -> puerta sellada de S-9 -> fin del nivel.
// La puerta de S-9 nunca se muestra abierta: usar la llave solo activa el LevelExit.
public static partial class Level03Builder
{
    static HingedDoor gateLab, gateOffice;
    static GameObject exitSlab;

    static void BuildGameplayObjects()
    {
        interactablesGroup = T("Interactables", gameplayRoot);
        keysGroup = T("Keys", gameplayRoot);
        documentsGroup = T("Documents", gameplayRoot);
        doorsGroup = T("Doors", gameplayRoot);
        triggersGroup = null;
        exitGroup = T("LevelExit", gameplayRoot);

        BuildGates();
        BuildCustodyLocker();
        BuildKeys();
        BuildPickups();
        BuildDocuments();
        BuildSealedDoor();
    }

    // ---------------------------------------------------------------- Puertas

    // Puerta doble con bisagras en un hueco de 2 m. Se abre girando las hojas hacia "swing" (dirección del mundo).
    static HingedDoor Gate(string name, string prefab, int c, int r, char d, string required, string prompt, string locked, Vector3 swing)
    {
        if (!edgeMap.TryGetValue(Canon(c, r, d), out Edge e)) { Debug.LogWarning("Level03Builder: no hay hueco para " + name); return null; }
        GameObject gate = Spawn(prefab, doorsGroup);
        if (gate == null) return null;
        gate.name = name;
        gate.transform.rotation = Quaternion.Euler(0, e.v ? 90f : 0f, 0);
        // El prefab trae el flag Static: con static batching la hoja se movería (collider) sin que se viera moverse. Se desactiva en toda la puerta.
        foreach (Transform st in gate.GetComponentsInChildren<Transform>(true)) st.gameObject.isStatic = false;
        gate.transform.localScale = Vector3.one * 0.95f;   // la puerta mide 2,11 x 2,22 m; el hueco, 2 m. Escala uniforme: las hojas giran sin deformarse
        SnapXZ(gate, e.Mid.x, e.Mid.z);
        SnapY(gate, 0f, false);

        // TEMP: panel de madera sobre la puerta (la hoja llega a 2,1 m y la viga empieza a 2,4 m)
        Material wood = Mat("DoorTransom", new Color(0.20f, 0.13f, 0.09f), 0.25f, "Prop");
        Box("Transom_" + name, doorsGroup, new Vector3(e.Mid.x, 2.26f, e.Mid.z), e.v ? new Vector3(0.19f, 0.3f, 2.0f) : new Vector3(2.0f, 0.3f, 0.19f), wood, false);

        Interactable it = gate.AddComponent<Interactable>();
        it.prompt = prompt;
        it.requiredItem = required;
        it.lockedMessage = locked;
        HingedDoor hinged = gate.AddComponent<HingedDoor>();
        hinged.leftLeaf = gate.transform.Find("DoorD_Left");
        hinged.rightLeaf = gate.transform.Find("DoorD_Right");
        hinged.swingDirection = swing;
        if (hinged.leftLeaf == null || hinged.rightLeaf == null) Debug.LogWarning("Level03Builder: " + name + " no tiene hojas DoorD_Left/DoorD_Right");
        Hook(it.onInteract, hinged.Open);
        return hinged;
    }

    static void BuildGates()
    {
        // El laboratorio se abre hacia dentro (hacia el oeste); Dirección, hacia el norte (hacia la sala)
        gateLab = Gate("Door_Lab_CardReader", AS + "DoorD_V1.prefab", 8, 5, 'E', "tarjeta_investigacion", "Abrir la puerta del laboratorio", "Lector de tarjetas apagado. Pide una tarjeta de investigación", Vector3.left);
        gateOffice = Gate("Door_DirectorOffice", AS + "DoorD_V1.prefab", 6, 3, 'S', "llave_direccion", "Abrir la puerta de Dirección", "Cerrada con llave. La placa dice: «DIRECCIÓN DE INVESTIGACIÓN»", Vector3.forward);
    }

    // ---------------------------------------------------------------- Casillero con candado (código 0307)

    // El casillero de custodia usa Padlock tal cual: fixedCode es texto, así que conserva el cero inicial y el campo admite 4 caracteres.
    static void BuildCustodyLocker()
    {
        if (labLocker == null) return;
        labLocker.name = "ColdLab_CustodyLocker";
        labLocker.transform.SetParent(interactablesGroup, true);
        EnsureCollider(labLocker);

        Interactable it = labLocker.AddComponent<Interactable>();
        it.prompt = "Abrir el casillero de custodia (candado)";
        Padlock padlock = labLocker.AddComponent<Padlock>();
        padlock.fixedCode = "0307";
        padlock.giveItem = "llave_direccion";
        padlock.unlockedMessage = "El candado cede. Dentro, una llave con una etiqueta: «DIRECCIÓN».";
        Hook(it.onInteract, padlock.Open);

        // TEMP: candado de latón colgado del casillero
        Bounds b = GetBounds(labLocker);
        GameObject lockGo = new GameObject("TEMP_Padlock");
        lockGo.transform.SetParent(labLocker.transform, true);
        lockGo.transform.position = new Vector3(b.center.x + 0.3f, 1.05f, b.min.z - 0.02f);
        Material brass = Mat("Brass", new Color(0.55f, 0.38f, 0.16f), 0.5f, "Metal", 0.8f);
        Box("TEMP_PadlockBody", lockGo.transform, Vector3.zero, new Vector3(0.07f, 0.07f, 0.03f), brass, false).transform.position = lockGo.transform.position;
        GameObject shackle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        shackle.name = "TEMP_PadlockShackle";
        Object.DestroyImmediate(shackle.GetComponent<Collider>());
        shackle.transform.SetParent(lockGo.transform, true);
        shackle.transform.position = lockGo.transform.position + new Vector3(0, 0.05f, 0);
        shackle.transform.localScale = new Vector3(0.012f, 0.025f, 0.012f);
        shackle.GetComponent<Renderer>().sharedMaterial = brass;
    }

    // ---------------------------------------------------------------- Objetos clave

    static void BuildKeys()
    {
        // Tarjeta de investigación: escritorio de la sala de proyección (TEMP: tarjeta de plástico con banda magnética)
        if (projDesk != null)
        {
            Vector3 top = TopOf(projDesk);
            GameObject card = new GameObject("ResearchCard");
            card.transform.SetParent(keysGroup, false);
            card.transform.position = top + new Vector3(0.1f, 0.006f, -0.15f);
            card.transform.rotation = Quaternion.Euler(0, 62f, 0);
            Box("TEMP_CardBody", card.transform, Vector3.zero, new Vector3(0.12f, 0.008f, 0.075f), Mat("CardPlastic", new Color(0.45f, 0.42f, 0.30f), 0.4f, "Metal"), false).transform.localPosition = Vector3.zero;
            Box("TEMP_CardStripe", card.transform, Vector3.zero, new Vector3(0.12f, 0.009f, 0.014f), Mat("CardStripe", new Color(0.05f, 0.05f, 0.05f), 0.5f, "Metal"), false).transform.localPosition = new Vector3(0, 0, 0.022f);
            card.AddComponent<BoxCollider>().size = new Vector3(0.18f, 0.05f, 0.12f);
            Interactable it = card.AddComponent<Interactable>();
            it.prompt = "Coger tarjeta de investigación";
            it.giveItem = "tarjeta_investigacion";
            it.destroyAfterUse = true;
            Glint(card);
        }

        // Llave de S-9: escritorio de Dirección, a la vista
        if (officeDesk != null)
        {
            Vector3 top = TopOf(officeDesk);
            GameObject key = Item(SP + "key tag.prefab", "Key_S9", new Vector3(top.x + 0.1f, top.y, top.z - 0.3f), 0.17f, "Coger llave de S-9", keysGroup);
            if (key != null) key.GetComponent<Interactable>().giveItem = "llave_s9";
        }
    }

    // ---------------------------------------------------------------- Recursos de supervivencia

    static void BuildPickups()
    {
        Pickup("Battery_Reception", true, receptionBench, 0.55f, 0f);
        Pickup("Battery_ObservationCorridor", true, new Vector3(20.6f, 0, 12.9f));
        Pickup("Battery_Gallery", true, galleryDesk, 0.1f, 0.5f);
        Pickup("Battery_ColdLab", true, new Vector3(15.6f, 0, 19.1f));
        Pickup("Battery_DrawingRoom", true, drawBedside, 0f, 0f);
        Pickup("Bandage_Reception", false, counterR, -0.5f, 0.2f);
        Pickup("Bandage_ProjectionRoom", false, projTable, 0f, 0.15f);
        Pickup("Bandage_ColdLab", false, new Vector3(13.2f, 0, 14.5f));
        Pickup("Bandage_DirectorOffice", false, new Vector3(8.2f, 0, 25.5f));
    }

    static void Pickup(string name, bool battery, GameObject surface, float dx, float dz)
    {
        if (surface == null) return;
        Vector3 top = TopOf(surface);
        Pickup(name, battery, new Vector3(top.x + dx, top.y, top.z + dz));
    }

    static void Pickup(string name, bool battery, Vector3 pos)
    {
        GameObject go = battery
            ? Item(SP + "battery big.prefab", name, pos, 0.14f, "Recoger batería", interactablesGroup)
            : Item(SP + "bandage.prefab", name, pos, 0.2f, "Usar vendaje", interactablesGroup);
        if (go == null) return;
        if (battery) Hook(go.GetComponent<Interactable>().onInteract, playerFlashlight.AddBattery);
        else Hook(go.GetComponent<Interactable>().onInteract, playerHealth.UseBandage);
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
        EnsureCollider(go);
        Interactable it = go.AddComponent<Interactable>();
        it.prompt = prompt;
        it.destroyAfterUse = true;
        Glint(go);
        return go;
    }

    static void Glint(GameObject go)
    {
        GameObject glint = new GameObject("Glint");
        glint.transform.SetParent(go.transform, true);
        glint.transform.position = GetBounds(go).center + Vector3.up * 0.15f;
        Light l = glint.AddComponent<Light>();
        l.type = LightType.Point;
        l.range = 1.2f;
        l.intensity = 0.3f;
        l.color = new Color(1f, 0.9f, 0.7f);
        l.shadows = LightShadows.None;
    }

    // ---------------------------------------------------------------- Documentos

    // Nombre de objeto legible: sin tildes ni signos
    static string StripAccents(string s)
    {
        string d = s.Normalize(System.Text.NormalizationForm.FormD);
        var sb = new System.Text.StringBuilder();
        foreach (char c in d)
            if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) != System.Globalization.UnicodeCategory.NonSpacingMark) sb.Append(c);
        return sb.ToString();
    }

    static void Note(Vector3 pos, string title, DocumentStyle style, string reference, string date, string text, string author = "")
    {
        GameObject go = Item(AS + "Paper.prefab", "Note_" + System.Text.RegularExpressions.Regex.Replace(StripAccents(title), "[^A-Za-z0-9]+", "_").Trim('_'), pos, 0.3f, "Leer", documentsGroup);
        if (go == null) return;
        Interactable it = go.GetComponent<Interactable>();
        it.destroyAfterUse = false;
        Document doc = go.AddComponent<Document>();
        doc.title = title; doc.style = style; doc.reference = reference; doc.date = date; doc.author = author; doc.text = text;
        Hook(it.onInteract, doc.Read);
    }

    static void Note(GameObject surface, float dx, float dz, string title, DocumentStyle style, string reference, string date, string text, string author = "")
    {
        if (surface == null) { Debug.LogWarning("Level03Builder: falta la superficie para el documento «" + title + "»"); return; }
        Vector3 top = TopOf(surface);
        Note(new Vector3(top.x + dx, top.y + 0.004f, top.z + dz), title, style, reference, date, text, author);
    }

    static void BuildDocuments()
    {
        // ---- Recepción: protocolo de acceso y el aviso del reloj (primera pista del código)
        Note(counterL, 0.35f, 0.05f, "Protocolo de acceso a Investigación", DocumentStyle.StaffReport, "INV-001", "20/09/1993",
            "Toda persona que acceda a esta ala debe portar tarjeta de investigación. Las tarjetas se custodian en la sala de proyección al terminar cada sesión.\n\nLas sesiones con el paciente 214 comienzan siempre a la misma hora, fijada por el doctor Salas. No se admiten cambios de horario ni sesiones fuera de ella.\n\nQueda prohibido detener la grabación aunque el sujeto deje de responder.\nQueda prohibido responder a cualquier voz que no provenga de la sala.", "Dirección de Investigación");

        Note(counterR, -0.55f, 0.1f, "Reloj de recepción", DocumentStyle.StaffReport, "MNT-1033", "06/12/1993",
            "El reloj de pie de recepción se detiene cada noche a la misma hora. Se ha cambiado el mecanismo dos veces; se vuelve a detener.\n\nDirección ordena dejarlo como está: «es el único reloj de la planta que marca la hora correcta».\n\nNo lo reinicien.", "Mantenimiento");

        // ---- Corredor: el derrumbe
        Note(corridorBench, 0.3f, 0f, "Clausura del tramo oeste", DocumentStyle.StaffReport, "OBR-0212", "08/12/1993",
            "Se clausura el tramo oeste del corredor de observación tras el desplome del techo. El peritaje no halla defectos estructurales.\n\nLos escombros cayeron hacia el pasillo, no hacia la sala: como si algo los hubiera empujado desde el otro lado del cristal.\n\nQueda prohibido retirar el material.", "Obras");

        // ---- Sala de proyección: el registro de sesiones (segunda pista del código) y una nota de paciente
        Note(projDesk, 0.1f, 0.15f, "Registro de sesiones: paciente 214", DocumentStyle.StaffReport, "SES-214", "05/12/1993",
            "Sesión 1  ·  14/11  ·  03:07  ·  41 min\nSesión 2  ·  17/11  ·  03:07  ·  52 min\nSesión 3  ·  21/11  ·  03:07  ·  38 min\nSesión 4  ·  25/11  ·  03:07  ·  1 h 10\nSesión 5  ·  28/11  ·  03:07  ·  49 min\nSesión 6  ·  02/12  ·  03:07  ·  4 min\nSesión 7  ·  05/12  ·  03:07  ·  ———\n\nLa hora de inicio no la fijamos nosotros: si retrasamos la cinta, el paciente se queda callado hasta las 03:07.\n\nLa sesión 6 duró lo mismo que el corte de energía de esa noche.", "Técnico de grabación");

        Note(projTable, 0.35f, -0.2f, "Nota pegada a la pantalla", DocumentStyle.PatientNote, "", "",
            "Cuando pasan la película yo no miro la pantalla. Miro el cristal.\n\nA veces, en el cristal, hay alguien sentado detrás de mí.\n\nNo me giro. Nunca me giro.");

        // ---- Cámara de pruebas y galería
        Note(chamberTrolley, 0f, 0f, "Hoja de sesión 7", DocumentStyle.MedicalRecord, "0214-S7", "05/12/1993",
            "El paciente repite una frase en una lengua que nadie del equipo reconoce. Al tercer minuto, la repiten desde la galería.\n\nEl técnico asegura que nadie habló. La grabación confirma la frase dentro de la sala; no desde la galería ni desde el pasillo.\n\nInterrumpo la sesión. El paciente sonríe: «Ya me oyó.»", "Dr. A. Salas");

        Note(galleryDesk, -0.15f, -0.15f, "Informe del observador", DocumentStyle.StaffReport, "OBS-07", "06/12/1993",
            "Desde la galería se ve toda la cámara y el pasillo. En la sesión 6 conté seis personas aquí arriba. Al volver la luz, siete.\n\nLa séptima estaba sentada a mi lado, en mi silla, y la silla no se había movido. Llevaba mi abrigo. Tenía mi cara, pero más tranquila.\n\nBajé sin avisar. No he vuelto a la galería.", "Obs. P. Navarro");

        // ---- Laboratorio: la hoja de custodia (tercera pista) y las muestras
        Note(labBench, 0.42f, -0.1f, "Custodia de objetos", DocumentStyle.StaffReport, "CUS-12", "04/12/1993",
            "Los objetos de Dirección se guardan en el casillero de custodia de la cámara fría. Se cierra con candado de cuatro cifras.\n\nPara que el personal lo recuerde sin escribirlo: la clave es la hora de las sesiones, horas y minutos, sin separadores.\n\nNo anoten el código en ningún sitio.", "Archivo Central");

        Note(labBench, -0.4f, 0.15f, "Muestras sin paciente", DocumentStyle.MedicalRecord, "LAB-0099", "30/11/1993",
            "Siete muestras de sangre etiquetadas con nombres de personas que nunca ingresaron. Tres de ellos trabajan en este edificio.\n\nCompatibilidad entre las siete: 100 %. Compatibilidad con la del paciente 214: 100 %.\n\nSe solicita no repetir la prueba.", "Laboratorio Clínico");

        // ---- Sala de dibujos: el cuaderno del 214 y la anotación del doctor
        Note(drawDesk, -0.45f, 0.0f, "Cuaderno del 214: página 1", DocumentStyle.PatientNote, "", "",
            "Me dijeron que dibujara lo que oigo.\n\nOigo una puerta que no está. Está al fondo, abajo.\n\nCuando la dibujo bien, se abre un poquito.");
        Note(drawDesk, 0.0f, -0.05f, "Cuaderno del 214: página 2", DocumentStyle.PatientNote, "", "",
            "Ya sé cómo se hace. Es fácil.\n\nSolo hay que llamar a alguien que no esté y esperar.\n\nSiempre contesta alguien.");
        Note(drawDesk, 0.45f, 0.05f, "Cuaderno del 214: página 3", DocumentStyle.PatientNote, "", "",
            "Llamé desde la sala de pruebas. A las 03:07.\n\nNo vino uno. Vinieron todos los que se me parecen.\n\nAhora la cama está ocupada y yo estoy de pie. Ya no sé cuál de los dos firma los dibujos.");
        Note(drawBedside, 0f, 0f, "Anotación a lápiz del doctor", DocumentStyle.MedicalRecord, "0214-A/9", "07/12/1993",
            "Los dibujos del paciente coinciden entre sí con una precisión imposible. La puerta del dibujo 5 es la puerta de S-9. Él no ha bajado nunca al sótano profundo.\n\nPedí a Dirección que clausure S-9. Me respondieron que ya estaba cerrada.\n\nDesde dentro.", "Dr. A. Salas");

        // ---- Dirección
        Note(officeDesk, 0.55f, 0.15f, "Acta de suspensión de la investigación", DocumentStyle.StaffReport, "DIR-1212", "09/12/1993",
            "Se suspende el estudio del paciente 214 y se clausura el ala de investigación. El material queda archivado.\n\nLa llave de la sala S-9 permanece sobre el escritorio de Dirección, a la vista. Nadie debe usarla.\n\n(Se retiró de la caja fuerte porque cada mañana aparecía abierta.)", "Dirección General");
        Note(officeDesk, -0.45f, -0.25f, "Última evaluación del doctor Salas", DocumentStyle.MedicalRecord, "0001-S", "10/12/1993",
            "Insomnio, 19 días. Oigo mi voz dando órdenes en el pasillo del ala.\n\nMañana bajaré a S-9 a comprobar quién la da.\n\nSi vuelvo con mi voz, no me creáis.", "Dr. A. Salas");

        // ---- Vestíbulo de S-9
        Note(vestBench, 0.3f, 0f, "Orden de sellado: S-9", DocumentStyle.StaffReport, "SEL-009", "11/12/1993",
            "La puerta de S-9 se sella desde este vestíbulo: seis pernos y cerradura de Dirección.\n\nNo consta qué hay al otro lado. Consta lo que hubo: Dirección utilizó esa sala hasta 1989.\n\nSi la puerta se encuentra abierta, no se debe cerrar desde este lado.", "Dirección General");
    }

    // ---------------------------------------------------------------- Puerta sellada de S-9 y fin del nivel

    static void BuildSealedDoor()
    {
        if (mainDoorGo == null) return;
        mainDoorGo.name = "Door_SealedS9";
        mainDoorGo.transform.SetParent(doorsGroup, true);
        EnsureCollider(mainDoorGo);

        // TEMP: placa de acero tras el arco de la puerta y seis pernos (los que cita la orden de sellado SEL-009)
        Transform deco = Cat("Decoration");
        Material steel = Mat("SealSteel", new Color(0.30f, 0.24f, 0.21f), 0.4f, "Metal", 0.7f);
        Box("Vestibule_SealPlate", deco, new Vector3(18.0f, 1.32f, 27.872f), new Vector3(1.95f, 2.65f, 0.02f), steel, false);
        for (int i = 0; i < 6; i++)
            Box("Vestibule_SealBolt_" + (i + 1), deco, new Vector3(17.2f + (i % 2) * 1.6f, 0.5f + (i / 2) * 0.95f, 27.69f), new Vector3(0.13f, 0.13f, 0.05f), steel, false);

        // Salida: un volumen delante de la puerta que solo se activa al usar la llave de S-9. La puerta no llega a abrirse en pantalla.
        exitSlab = new GameObject("LevelExit_S9");
        exitSlab.transform.SetParent(exitGroup, false);
        exitSlab.transform.position = new Vector3(18.0f, 1.2f, 25.4f);
        BoxCollider box = exitSlab.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(3.6f, 2.4f, 4.0f);
        LevelExit exit = exitSlab.AddComponent<LevelExit>();
        exit.nextScene = GameFlow.EndingScene;   // tras el Nivel 3 viene la escena final (salida del hospital)
        exit.message = "Fin del nivel 3";
        exitSlab.SetActive(false);

        Interactable it = mainDoorGo.AddComponent<Interactable>();
        it.prompt = "Usar la llave de S-9";
        it.requiredItem = "llave_s9";
        it.lockedMessage = "Seis pernos y una cerradura de Dirección. Sobre la placa, «S-9». No cede";
        HookBool(it.onInteract, exitSlab.SetActive, true);
    }
}
