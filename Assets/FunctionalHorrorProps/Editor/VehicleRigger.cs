using UnityEditor;
using UnityEngine;

namespace FunctionalHorrorProps.EditorTools
{
    /// <summary>
    /// One-click rigging for imported vehicles: adds <see cref="FunctionalHorrorVehicle"/>
    /// to the selected objects and auto-wires wheels + exhaust smoke from the GLB node names.
    /// Select the vehicle root(s) in the Hierarchy (or open a prefab) and run the menu item.
    /// </summary>
    public static class VehicleRigger
    {
        [MenuItem("Tools/Functional Horror Props/Rig Selected Vehicles (wheels + exhaust)")]
        private static void RigSelected()
        {
            int n = 0;
            foreach (var go in Selection.gameObjects)
            {
                if (go == null) continue;
                var v = go.GetComponent<FunctionalHorrorVehicle>();
                if (v == null) v = Undo.AddComponent<FunctionalHorrorVehicle>(go);
                v.AutoWire();
                EditorUtility.SetDirty(go);
                n++;
            }
            Debug.Log($"[FunctionalHorrorProps] Rigged {n} vehicle(s): wheels now spin from 'Speed Kmh' and the tailpipe emits smoke. Enter Play (or tick 'Run In Edit Mode') to see them turn.");
        }

        [MenuItem("Tools/Functional Horror Props/Rig Selected Vehicles (wheels + exhaust)", true)]
        private static bool RigSelectedValidate() => Selection.gameObjects.Length > 0;

        [MenuItem("Tools/Functional Horror Props/Un-rig Selected Vehicles")]
        private static void UnrigSelected()
        {
            foreach (var go in Selection.gameObjects)
            {
                var v = go != null ? go.GetComponent<FunctionalHorrorVehicle>() : null;
                if (v == null) continue;
                v.Unrig();
                EditorUtility.SetDirty(go);
            }
        }

        [MenuItem("Tools/Functional Horror Props/Un-rig Selected Vehicles", true)]
        private static bool UnrigSelectedValidate() => Selection.gameObjects.Length > 0;
    }
}
