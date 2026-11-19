using System;
using System.Collections.Generic;

// Datos de un objeto para mostrarlo en el inventario
public struct ItemInfo
{
    public string name;
    public string description;
    public string hint;        // para qué sirve / dónde usarlo (una línea)
    public ItemIcon icon;
}

// Registro de un documento leído (el inventario no guarda el texto, solo que se leyó)
public struct DocumentEntry
{
    public string title, text, reference, date, author;
    public DocumentStyle style;
}

// Lo que lleva el jugador: objetos (llaves, tarjetas, fusibles...) y los documentos que ha leído.
// Cada objeto es solo un nombre ("llave_escalera"); quien lo pide y quien lo da usan el mismo texto.
// Para que un objeto nuevo se vea bien en el inventario, añádelo a "catalog".
public static class Inventory
{
    static readonly List<string> items = new List<string>();
    static readonly Dictionary<string, int> stacks = new Dictionary<string, int>();   // objetos apilables: baterías, vendajes
    static readonly List<DocumentEntry> documents = new List<DocumentEntry>();

    static readonly Dictionary<string, ItemInfo> catalog = new Dictionary<string, ItemInfo>
    {
        ["linterna"] = new ItemInfo
        {
            name = "Linterna",
            description = "Tu única luz. La batería se gasta mientras está encendida y, cuando queda poca, la luz parpadea. Las baterías que encuentres la recargan.",
            hint = "F  ·  encender o apagar",
            icon = ItemIcon.Flashlight
        },
        ["bateria"] = new ItemInfo
        {
            name = "Batería",
            description = "Pila de repuesto para la linterna. Se guardan las que recojas con la batería llena.",
            hint = "R  ·  recargar la linterna",
            icon = ItemIcon.Generic
        },
        ["vendaje"] = new ItemInfo
        {
            name = "Vendaje",
            description = "Gasa limpia para curar heridas. Se guardan los que recojas con la salud llena.",
            hint = "H  ·  curarte",
            icon = ItemIcon.Generic
        },
        ["llave_escalera"] = new ItemInfo
        {
            name = "Llave de la reja",
            description = "Llave de latón con una etiqueta: «REJA». Era la copia única y estaba guardada bajo candado en el archivador del almacén.",
            hint = "Úsala en la reja del pasillo principal",
            icon = ItemIcon.Key
        },
        ["fusible"] = new ItemInfo
        {
            name = "Fusible",
            description = "Cilindro de porcelana con un hilo de cobre intacto. Lleva grabado «30 A». Estaba guardado en el cofre del taller de mantenimiento.",
            hint = "Colócalo en el tablero de fusibles de la sala eléctrica",
            icon = ItemIcon.Generic
        },
        ["tarjeta_archivo"] = new ItemInfo
        {
            name = "Tarjeta del archivo",
            description = "Tarjeta de acceso con la banda magnética gastada. Impreso en el borde: «ARCHIVO RESTRINGIDO · SUPERVISIÓN ESPECIAL».",
            hint = "Úsala en la reja del archivo restringido",
            icon = ItemIcon.Key
        },
        ["llave_montacargas"] = new ItemInfo
        {
            name = "Llave del montacargas",
            description = "Llave de hierro con una etiqueta de cartón: «MONTACARGAS · NIVEL INFERIOR». La tinta está corrida por la humedad.",
            hint = "Úsala en el panel del montacargas",
            icon = ItemIcon.Key
        },
        ["tarjeta_investigacion"] = new ItemInfo
        {
            name = "Tarjeta de investigación",
            description = "Tarjeta de acceso del personal del ala de investigación, con la banda magnética gastada. Estaba sobre el escritorio de la sala de proyección.",
            hint = "Úsala en la puerta del laboratorio",
            icon = ItemIcon.Key
        },
        ["llave_direccion"] = new ItemInfo
        {
            name = "Llave de Dirección",
            description = "Llave de latón con una etiqueta de cartón: «DIRECCIÓN». Estaba bajo candado en el casillero de custodia de la cámara fría.",
            hint = "Úsala en la puerta de Dirección de Investigación",
            icon = ItemIcon.Key
        },
        ["llave_s9"] = new ItemInfo
        {
            name = "Llave de S-9",
            description = "Llave pesada con una placa grabada: «S-9». Según el acta, cada mañana aparecía fuera de la caja fuerte. Nadie debía usarla.",
            hint = "Úsala en la puerta sellada del vestíbulo",
            icon = ItemIcon.Key
        },
    };

    // Avisan a la interfaz (aviso de "nuevo objeto"); no hace falta suscribirse desde otros scripts
    public static event Action<string> ItemAdded;
    public static event Action<DocumentEntry> DocumentAdded;

    public static IReadOnlyList<string> Items => items;
    public static IReadOnlyList<DocumentEntry> Documents => documents;

    public static void Add(string id)
    {
        if (string.IsNullOrEmpty(id) || items.Contains(id)) return;
        items.Add(id);
        ItemAdded?.Invoke(id);
    }

    public static bool Has(string id) => items.Contains(id);

    // Objetos apilables (baterías, vendajes): se guardan en cantidad y se gastan de uno en uno
    public static IReadOnlyDictionary<string, int> Stacks => stacks;
    public static int Count(string id) => stacks.TryGetValue(id, out int n) ? n : 0;

    public static void AddStack(string id)
    {
        stacks[id] = Count(id) + 1;
        ItemAdded?.Invoke(id);
    }

    public static bool TakeStack(string id)
    {
        int n = Count(id);
        if (n <= 0) return false;
        if (n == 1) stacks.Remove(id); else stacks[id] = n - 1;
        return true;
    }

    // Al morir: el nivel se reinicia, así que se pierden los objetos; el registro de documentos leídos se conserva
    public static void Clear()
    {
        items.Clear();
        stacks.Clear();
    }

    // Empezar de cero (volver a jugar desde el principio)
    public static void ClearAll()
    {
        Clear();
        documents.Clear();
    }

    public static void AddDocument(DocumentEntry entry)
    {
        foreach (DocumentEntry d in documents)
            if (d.title == entry.title && d.reference == entry.reference) return;   // ya estaba guardado
        documents.Add(entry);
        DocumentAdded?.Invoke(entry);
    }

    public static ItemInfo GetInfo(string id)
    {
        if (catalog.TryGetValue(id, out ItemInfo info)) return info;
        string pretty = id.Replace('_', ' ');
        return new ItemInfo { name = char.ToUpper(pretty[0]) + pretty.Substring(1), description = "", hint = "", icon = ItemIcon.Generic };
    }
}
