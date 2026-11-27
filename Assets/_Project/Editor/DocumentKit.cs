using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Añade documentos nuevos a las escenas YA GUARDADAS de los niveles (sin regenerarlas). Cada documento nuevo se clona de uno
// existente de la misma escena (así lleva el mismo papel, brillo e interacción) y se coloca en la misma superficie, junto a él.
// Es idempotente: si ya existe un documento con ese título, solo se actualiza su texto.
// Para añadir o cambiar un documento se edita solo la tabla de abajo.
public static class DocumentKit
{
    struct NewDoc
    {
        public string title, reference, date, author, text, near;
        public DocumentStyle style;
        public NewDoc(string title, DocumentStyle style, string reference, string date, string author, string near, string text)
        { this.title = title; this.style = style; this.reference = reference; this.date = date; this.author = author; this.near = near; this.text = text; }
    }

    // Documentos que conectan la historia (paciente 214, la entidad que imita, la 03:07) con las reglas del juego:
    //  Nivel 1: la luz falla antes de que aparezca algo (respiración detrás / figura delante: no moverse, no girarse).
    //  Nivel 2: las normas de la planta y el testimonio de un guardia (la figura delante: no apartar la vista; solo avanza cuando la luz falla).
    //  Nivel 3: la nota de quien consiguió salir (no mirar atrás), que enlaza con la escena final.
    static readonly Dictionary<string, NewDoc[]> ByScene = new Dictionary<string, NewDoc[]>
    {
        { "Assets/_Project/Scenes/Level01_Ward.unity", new[]
            {
                new NewDoc("Los fluorescentes del fondo", DocumentStyle.StaffReport, "MNT-0941", "12/11/1993", "J. Ortega, Mantenimiento", "Acceso clausurado",
                    "Seguí vigilando los fluorescentes del fondo. No fallan al azar: parpadean justo antes de que aparezca algo en el pasillo.\n\n" +
                    "Si la luz falla, quédese donde está. Si respira alguien detrás, no se mueva. Si de pronto hay una figura de pie delante, tampoco, y no se gire a mirar atrás: lo que está delante solo quiere que se gire.\n\n" +
                    "Cuando la luz vuelve, a veces ya no está. A veces sí."),
            } },
        { "Assets/_Project/Scenes/Level02_Service.unity", new[]
            {
                new NewDoc("Normas para el turno de noche", DocumentStyle.StaffReport, "SEG-0420", "25/11/1993", "Jefatura de Seguridad", "Avería en el tablero general",
                    "Tras los hechos del 18/11, el personal que deba circular por la planta de servicios cumplirá estas normas:\n\n" +
                    "1. Si oye respirar detrás de usted, no se mueva hasta que cese. No se gire.\n" +
                    "2. Si ve una figura de pie a lo lejos, no la pierda de vista. Mientras la vea, no se acerca. Solo da un paso cuando la luz falla.\n" +
                    "3. No apague la linterna. La oscuridad no le protege: es donde deja de parecerse a nadie.\n" +
                    "4. Si reconoce a la figura, no la salude.\n\n" +
                    "Quien no pueda cumplirlas debe solicitar el traslado de planta."),
                new NewDoc("Lo mantuve a la vista", DocumentStyle.PatientNote, "", "", "", "Parte de enfermería",
                    "Lo vi al fondo del pasillo de servicio. Quieto. Con mi uniforme.\n\n" +
                    "Lo mantuve a la vista casi una hora. Cada vez que parpadeaba la lámpara estaba un paso más cerca.\n\n" +
                    "Cuando me temblaron las manos y bajé la vista, lo tuve delante de la cara.\n\n" +
                    "No apartes la vista. Aunque llores."),
            } },
        { "Assets/_Project/Scenes/Level03_ResearchWing.unity", new[]
            {
                new NewDoc("Para quien salga", DocumentStyle.PatientNote, "", "", "", "Orden de sellado: S-9",
                    "Si has llegado hasta aquí, escucha.\n\n" +
                    "La llave abre la puerta de S-9. Al otro lado no hay nada que puedas mirar, solo el camino de salida.\n\n" +
                    "Cruza el portón sin detenerte. Si te llaman, no contestes. Si algo respira detrás de ti, sigue andando.\n\n" +
                    "No mires atrás.\n\n" +
                    "Yo me giré una vez. Por eso sigo aquí, escribiendo en esta hoja."),
            } },
    };

    [MenuItem("Don't Look Back/Añadir documentos de historia a los niveles 1, 2 y 3 (escenas guardadas)")]
    static void ApplyToAll()
    {
        if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("DocumentKit: detén el juego antes de añadir los documentos.");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        foreach (KeyValuePair<string, NewDoc[]> level in ByScene)
        {
            var scene = EditorSceneManager.OpenScene(level.Key, OpenSceneMode.Single);
            int added = 0;
            foreach (NewDoc d in level.Value) if (AddOrUpdate(d)) added++;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Documentos de " + scene.name + ": " + added + " nuevos, " + (level.Value.Length - added) + " actualizados.");
        }
    }

    // true = creado; false = ya existía y solo se actualizó
    static bool AddOrUpdate(NewDoc d)
    {
        Document existing = null, template = null;
        foreach (Document doc in Object.FindObjectsByType<Document>(FindObjectsInactive.Include))
        {
            if (doc.title == d.title) existing = doc;
            if (doc.title == d.near) template = doc;
        }
        if (existing != null) { Fill(existing, d); return false; }
        if (template == null) { Debug.LogError("DocumentKit: no se encontró el documento '" + d.near + "' para colocar '" + d.title + "' junto a él."); return false; }

        GameObject go = Object.Instantiate(template.gameObject, template.transform.parent);
        go.name = "Note_" + d.title.Replace(' ', '_').Replace(':', '_').Replace('.', '_');
        Physics.SyncTransforms();
        PlaceNear(go.transform, template.transform);
        Fill(go.GetComponent<Document>(), d);
        Undo.RegisterCreatedObjectUndo(go, "Añadir documento");
        return true;
    }

    static void Fill(Document doc, NewDoc d)
    {
        doc.title = d.title;
        doc.style = d.style;
        doc.reference = d.reference;
        doc.date = d.date;
        doc.author = d.author;
        doc.text = d.text;
        EditorUtility.SetDirty(doc);
    }

    // Busca un sitio de la misma superficie (misma altura) cerca de la plantilla, libre de otros documentos
    static void PlaceNear(Transform note, Transform template)
    {
        float lift = template.position.y - SurfaceY(template.position, 1.2f, template);   // cuánto flota la hoja sobre la superficie
        foreach (float radius in new[] { 0.30f, 0.40f, 0.52f, 0.65f })
            for (int a = 0; a < 360; a += 30)
            {
                Vector3 p = template.position + Quaternion.Euler(0, a, 0) * Vector3.forward * radius;
                float y = SurfaceY(p, 1.2f, null);
                if (float.IsNaN(y) || Mathf.Abs(y + lift - template.position.y) > 0.05f) continue;   // otra altura: no es la misma mesa
                if (NearOtherDocument(p, note)) continue;
                note.position = new Vector3(p.x, y + lift, p.z);
                note.rotation = Quaternion.Euler(0f, template.eulerAngles.y + Random.Range(-40f, 40f), 0f);
                return;
            }
        Debug.LogWarning("DocumentKit: no hay hueco libre junto a '" + template.name + "'; el documento '" + note.name + "' se deja encima del original.");
        note.position = template.position + Vector3.up * 0.01f;
    }

    // Altura de la primera superficie que no sea un documento, mirando hacia abajo desde 'above' metros por encima de 'p'
    static float SurfaceY(Vector3 p, float above, Transform ignore)
    {
        RaycastHit[] hits = Physics.RaycastAll(p + Vector3.up * above, Vector3.down, above + 1f, ~0, QueryTriggerInteraction.Ignore);
        float best = float.NaN, bestDist = float.MaxValue;
        foreach (RaycastHit h in hits)
        {
            if (h.collider.GetComponentInParent<Document>() != null) continue;
            if (ignore != null && h.collider.transform.IsChildOf(ignore)) continue;
            if (h.distance < bestDist) { bestDist = h.distance; best = h.point.y; }
        }
        return best;
    }

    static bool NearOtherDocument(Vector3 p, Transform self)
    {
        foreach (Document other in Object.FindObjectsByType<Document>(FindObjectsInactive.Include))
        {
            if (other.transform == self) continue;
            Vector3 d = other.transform.position - p; d.y = 0f;
            if (d.magnitude < 0.28f) return true;
        }
        return false;
    }
}
