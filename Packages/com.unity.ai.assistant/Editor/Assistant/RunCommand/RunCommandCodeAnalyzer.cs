using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using ExpressionEvaluator = Unity.AI.Assistant.Editor.CodeAnalyze.ExpressionEvaluator;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    static class RunCommandCodeAnalyzer
    {
        static readonly string[] k_UnauthorizedNamespaces = { "System.Net", "System.Diagnostics", "System.Runtime.InteropServices", "System.Reflection" };

        static readonly HashSet<string> k_UnsafeMethods = new()
        {
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "System.IO.File.Delete",
            "System.IO.Directory.Delete",
            "System.IO.File.Move",
            "System.IO.Directory.Move"
        };

        // Operations that throw or are meaningless in Play Mode, so the command must run in Edit Mode.
        static readonly HashSet<string> k_EditModeOnlyMethods = new()
        {
            "UnityEditor.SceneManagement.EditorSceneManager.SaveScene",
            "UnityEditor.SceneManagement.EditorSceneManager.SaveOpenScenes",
            "UnityEditor.SceneManagement.EditorSceneManager.NewScene",
            "UnityEditor.SceneManagement.EditorSceneManager.OpenScene"
        };

        static readonly HashSet<string> k_PlayModeExitMethods = new()
        {
            "UnityEditor.EditorApplication.ExitPlaymode"
        };

        static readonly HashSet<string> k_PlayModeEnterMethods = new()
        {
            "UnityEditor.EditorApplication.EnterPlaymode"
        };

        const string k_IsPlayingProperty = "UnityEditor.EditorApplication.isPlaying";

        static readonly HashSet<string> k_WriteOperationMethods = new()
        {
            // Undo operations (indicate state modification)
            "UnityEditor.Undo.RegisterCreatedObjectUndo",
            "UnityEditor.Undo.RecordObject",
            "UnityEditor.Undo.RegisterCompleteObjectUndo",
            "UnityEditor.Undo.DestroyObjectImmediate",

            // AssetDatabase write operations
            "UnityEditor.AssetDatabase.CreateAsset",
            "UnityEditor.AssetDatabase.DeleteAsset",
            "UnityEditor.AssetDatabase.SaveAssets",
            "UnityEditor.AssetDatabase.MoveAsset",
            "UnityEditor.AssetDatabase.CopyAsset",
            "UnityEditor.AssetDatabase.CreateFolder",

            // Prefab operations
            "UnityEditor.PrefabUtility.SaveAsPrefabAsset",
            "UnityEditor.PrefabUtility.SavePrefabAsset",

            // Object lifecycle
            "UnityEngine.Object.Instantiate",
            "UnityEngine.Object.DestroyImmediate",
            "UnityEngine.Object.Destroy",
            "UnityEngine.GameObject.CreatePrimitive",

            // File system write operations
            "System.IO.File.WriteAllText",
            "System.IO.File.WriteAllBytes",
            "System.IO.File.WriteAllLines",
            "System.IO.File.Delete",
            "System.IO.File.Move",
            "System.IO.File.Create",
            "System.IO.File.Copy",
            "System.IO.File.AppendAllText",
            "System.IO.File.AppendAllLines",
            "System.IO.Directory.CreateDirectory",
            "System.IO.Directory.Delete",
            "System.IO.Directory.Move",

            // FileUtil operations
            "UnityEditor.FileUtil.DeleteFileOrDirectory",
            "UnityEditor.FileUtil.MoveFileOrDirectory",
            "UnityEditor.FileUtil.CopyFileOrDirectory",
            "UnityEditor.FileUtil.ReplaceFile",
            "UnityEditor.FileUtil.ReplaceDirectory",

            // Scene management
            "UnityEditor.SceneManagement.EditorSceneManager.SaveScene",
            "UnityEditor.SceneManagement.EditorSceneManager.NewScene",

            // GameObject mutation
            "UnityEngine.GameObject.AddComponent",

            // Editor write patterns
            "UnityEditor.EditorUtility.SetDirty",
            "UnityEditor.SerializedObject.ApplyModifiedProperties",
            "UnityEditor.SerializedObject.ApplyModifiedPropertiesWithoutUndo",
        };

        public static RunCommandMetadata AnalyzeCommandAndExtractMetadata(CSharpCompilation compilation)
        {
            var result = new RunCommandMetadata();

            var commandTree = compilation.SyntaxTrees.FirstOrDefault();
            if (commandTree == null)
                return result;

            var model = compilation.GetSemanticModel(commandTree);
            var root = commandTree.GetCompilationUnitRoot();

            var runCommandInterfaceSymbol = compilation.GetTypeByMetadataName(typeof(IRunCommand).FullName);
            if (runCommandInterfaceSymbol == null)
                return result;

            var walker = new PublicMethodCallWalker(model);
            walker.Visit(root);

            foreach (var methodCall in walker.PublicMethodCalls)
            {
                if (k_UnsafeMethods.Contains(methodCall))
                {
                    result.IsUnsafe = true;
                    if (!result.UnsafeOperations.Contains(methodCall))
                        result.UnsafeOperations.Add(methodCall);
                }

                if (k_WriteOperationMethods.Contains(methodCall))
                    result.HasWriteOperations = true;

                if (k_EditModeOnlyMethods.Contains(methodCall))
                {
                    result.RequiresEditMode = true;
                    if (!result.EditModeOperations.Contains(methodCall))
                        result.EditModeOperations.Add(methodCall);
                }

                if (k_PlayModeExitMethods.Contains(methodCall))
                    result.RequestsPlayModeExit = true;

                if (k_PlayModeEnterMethods.Contains(methodCall))
                    result.RequestsPlayModeEnter = true;
            }

            foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            {
                if (model.GetSymbolInfo(assignment.Left).Symbol is not IPropertySymbol property)
                    continue;

                var propertyName = $"{property.ContainingType?.ToDisplayString()}.{property.Name}";
                if (propertyName != k_IsPlayingProperty)
                    continue;

                // A value only known at run time could go either way, so it needs consent for both directions.
                var value = model.GetConstantValue(assignment.Right);
                if (value.HasValue && value.Value is bool isPlaying)
                {
                    if (isPlaying)
                        result.RequestsPlayModeEnter = true;
                    else
                        result.RequestsPlayModeExit = true;
                }
                else
                {
                    result.RequestsPlayModeEnter = true;
                    result.RequestsPlayModeExit = true;
                }
            }

            return result;
        }

        public static bool HasUnauthorizedNamespaceUsage(string script)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(script);
            return tree.ContainsNamespaces(k_UnauthorizedNamespaces).Valid;
        }

        public static string GetUnauthorizedNamespaceError(string script)
        {
            var tree = SyntaxFactory.ParseSyntaxTree(script);
            var result = tree.ContainsNamespaces(k_UnauthorizedNamespaces);
            if (!result.Valid)
                return null;
            return $"Script uses one or more unauthorized namespaces: {result.Context}. Remove this usage and retry.";
        }
    }
}
