using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    // A package update can leave a theme's imports pointing at a destroyed StyleSheet for newly added USS files until it is reimported.
    [InitializeOnLoad]
    static class ThemeStyleSheetRefresher
    {
        const string k_PackagePath = "Packages/" + AssistantConstants.PackageName;
        const string k_ThemeExtension = ".tss";
        const string k_RefreshPendingSessionKey = "ThemeStyleSheetRefresher.RefreshPending";

        static HashSet<string> s_ReimportedThemePaths;

        static ThemeStyleSheetRefresher()
        {
            if (Application.isBatchMode || AssetDatabase.IsAssetImportWorkerProcess())
                return;

            UnityEditor.PackageManager.Events.registeredPackages -= OnPackagesRegistered;
            UnityEditor.PackageManager.Events.registeredPackages += OnPackagesRegistered;

            if (SessionState.GetBool(k_RefreshPendingSessionKey, false))
                ScheduleRefresh();
        }

        static void OnPackagesRegistered(UnityEditor.PackageManager.PackageRegistrationEventArgs args)
        {
            if (!args.changedTo.Any(p => p.name == AssistantConstants.PackageName))
                return;

            SessionState.SetBool(k_RefreshPendingSessionKey, true);
            ScheduleRefresh();
        }

        static void ScheduleRefresh()
        {
            s_ReimportedThemePaths = null;
            EditorApplication.update -= RefreshThemes;
            EditorApplication.update += RefreshThemes;
        }

        static void RefreshThemes()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            if (s_ReimportedThemePaths == null)
            {
                s_ReimportedThemePaths = ReimportThemes();
                return;
            }

            EditorApplication.update -= RefreshThemes;
            SessionState.EraseBool(k_RefreshPendingSessionKey);

            foreach (var window in Resources.FindObjectsOfTypeAll<EditorWindow>())
                ReapplyThemes(window.rootVisualElement, s_ReimportedThemePaths);

            s_ReimportedThemePaths = null;
        }

        static HashSet<string> ReimportThemes()
        {
            var styleSheetPaths = AssetDatabase.FindAssets("t:StyleSheet", new[] { k_PackagePath })
                .Select(AssetDatabase.GUIDToAssetPath)
                .ToList();

            // The reimport only relinks imports to stylesheets that are already loaded.
            foreach (var styleSheetPath in styleSheetPaths)
                AssetDatabase.LoadAssetAtPath<StyleSheet>(styleSheetPath);

            var themePaths = styleSheetPaths.Where(path => path.EndsWith(k_ThemeExtension)).ToHashSet();
            foreach (var themePath in themePaths)
                AssetDatabase.ImportAsset(themePath, ImportAssetOptions.ForceUpdate);

            return themePaths;
        }

        internal static int ReapplyThemes(VisualElement element, ICollection<string> themePaths)
        {
            var reappliedCount = 0;
            if (HostsTheme(element.styleSheets, themePaths))
            {
                ReapplyStyleSheets(element.styleSheets);
                reappliedCount++;
            }

            for (var i = 0; i < element.hierarchy.childCount; i++)
                reappliedCount += ReapplyThemes(element.hierarchy[i], themePaths);

            return reappliedCount;
        }

        static bool HostsTheme(VisualElementStyleSheetSet styleSheets, ICollection<string> themePaths)
        {
            for (var i = 0; i < styleSheets.count; i++)
            {
                if (themePaths.Contains(AssetDatabase.GetAssetPath(styleSheets[i])))
                    return true;
            }

            return false;
        }

        static void ReapplyStyleSheets(VisualElementStyleSheetSet styleSheets)
        {
            var orderedStyleSheets = new StyleSheet[styleSheets.count];
            for (var i = 0; i < orderedStyleSheets.Length; i++)
                orderedStyleSheets[i] = styleSheets[i];

            styleSheets.Clear();
            foreach (var styleSheet in orderedStyleSheets)
            {
                if (styleSheet != null)
                    styleSheets.Add(styleSheet);
            }
        }
    }
}
