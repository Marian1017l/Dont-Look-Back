using System.Text.RegularExpressions;
using Unity.AI.Assistant.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    static class AssistantDocsLinks
    {
        const string k_FallbackVersion = "2.18";

        internal static string ManualPageUrl(string relativePath) =>
            $"https://docs.unity3d.com/Packages/{AssistantConstants.PackageName}@{InstalledMajorMinor()}/manual/{relativePath}";

        internal static string MajorMinor(string version)
        {
            if (!string.IsNullOrEmpty(version))
            {
                var match = Regex.Match(version, @"^(\d+)\.(\d+)");
                if (match.Success)
                    return match.Groups[1].Value + "." + match.Groups[2].Value;
            }

            return k_FallbackVersion;
        }

        static string InstalledMajorMinor() =>
            MajorMinor(UnityEditor.PackageManager.PackageInfo.FindForPackageName(AssistantConstants.PackageName)?.version);
    }
}
