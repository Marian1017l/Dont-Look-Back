#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

// Makes the pack "just work" across Built-in / URP / HDRP: when the package is
// imported into a project whose render pipeline differs from the one it was built
// for, this regenerates every material with the correct shader + property mapping
// for the active pipeline (from the shipped manifest). Runs once per pipeline change,
// guarded by Assets/FunctionalHorrorProps/pipeline.sentinel.
namespace FunctionalHorrorProps
{
 [InitializeOnLoad]
 public static class PipelineAutoFix {
  static PipelineAutoFix(){ EditorApplication.delayCall += Run; }

  [MenuItem("Tools/Functional Horror Props/Fix Materials For This Pipeline")]
  public static void Run(){
   try { CrossPipelinePackBuild.RebuildMaterialsForCurrentPipeline(); }
   catch (System.Exception e){ Debug.LogWarning("[Functional Horror Props] PipelineAutoFix: "+e.Message); }
  }
 }
}
#endif
