using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;
#if UNITY_EDITOR
using UnityEditor;
#endif


namespace Unity.AI.Assistant.FunctionCalling
{
    static class FunctionCallingUtilities
    {
        internal const string k_SmartContextTag = "smart-context";
        internal const string k_AgentToolTag = "agent-tool";
        internal const string k_StaticContextTag = "static-context";
        internal const string k_CodeCorrectionTag = "code-correction";
        internal const string k_CodeExecutionTag = "code-execution";
        internal const string k_UITag = "ui";
        internal const string k_GameObjectTag = "game-object";
        internal const string k_CodeEditTag = "code-edit";
        internal const string k_PlayModeTag = "play-mode";
        internal const string k_ProjectOverviewTag = "project-overview";
        internal const string k_PlanningTag = "planning";

        /// <summary>
        /// Validates that the current Unity editor mode matches the required mode for a function.
        /// Throws an InvalidOperationException with detailed error message if validation fails.
        /// </summary>
        /// <param name="requiredModes">The required editor modes for the function (as flags)</param>
        /// <param name="forceModeSwitch">If true (Editor only), switch from PlayMode to EditMode when needed, after the user consents.</param>
        /// <param name="context">The tool execution context used to request play-mode-exit consent before switching.</param>
        /// <exception cref="InvalidOperationException">Thrown when the current editor mode doesn't match required modes and cannot auto-switch</exception>
        internal static async Task ValidateEnvironmentOrThrow(ToolCallEnvironment requiredModes, bool forceModeSwitch, ToolExecutionContext context)
        {
            if (requiredModes == 0)
                LogAndThrow("Tool does not declare required modes");

            UnityEnvironment currentMode = EnvironmentUtils.GetEnvironment();

            // Check if current mode matches any of the required modes using flags
            switch (currentMode)
            {
                case UnityEnvironment.EditMode:
                    if (requiredModes.HasFlag(ToolCallEnvironment.EditMode))
                        return; // Valid
                    break;

                case UnityEnvironment.PlayMode:
                    if (requiredModes.HasFlag(ToolCallEnvironment.PlayMode))
                        return; // Valid
                    
#if UNITY_EDITOR
                    // In PlayMode but the tool requires EditMode: exit play mode after the user consents (Editor only).
                    if (forceModeSwitch &&
                        (requiredModes == ToolCallEnvironment.EditMode ||
                         (requiredModes.HasFlag(ToolCallEnvironment.EditMode) && !requiredModes.HasFlag(ToolCallEnvironment.PlayMode))))
                    {
                        await ExitPlayModeWithConsentAsync(context);
                        return;
                    }
#endif
                    break;

                case UnityEnvironment.Runtime:
                    if (requiredModes.HasFlag(ToolCallEnvironment.Runtime))
                        return; // Valid
                    break;
            }

            // Generate error message using common logic
            string errorMessage = GenerateEnvironmentErrorMessage(currentMode, requiredModes);
            LogAndThrow(errorMessage);
        }

        /// <summary>
        /// Requests the user's consent to leave Play Mode, then exits and waits for the editor to be out of
        /// Play Mode. Throws if the user declines or the editor does not leave Play Mode in time.
        /// </summary>
        internal static async Task ExitPlayModeWithConsentAsync(ToolExecutionContext context)
        {
#if UNITY_EDITOR
            await context.Permissions.CheckPlayMode(PermissionPlayModeOperation.Exit);

            InternalLog.Log("[Environment] Exiting Play Mode...");
            EditorApplication.ExitPlaymode();

            const int maxWaitTimeMs = 10000;
            const int pollIntervalMs = 100;
            var elapsedMs = 0;
            while (EditorApplication.isPlaying && elapsedMs < maxWaitTimeMs)
            {
                await Task.Delay(pollIntervalMs);
                elapsedMs += pollIntervalMs;
            }

            if (EditorApplication.isPlaying)
                LogAndThrow("Timed out waiting for Unity to exit play mode. Please try again.");

            InternalLog.Log("[Environment] Successfully exited Play Mode. Continuing with tool execution.");
#else
            await Task.CompletedTask;
#endif
        }

        /// <summary>
        /// Generates a detailed error message for environment validation failures.
        /// </summary>
        static string GenerateEnvironmentErrorMessage(UnityEnvironment currentMode, ToolCallEnvironment requiredModes)
        {
            var modeList = new List<string>();
            if (requiredModes.HasFlag(ToolCallEnvironment.Runtime))
                modeList.Add("Runtime Mode");
            if (requiredModes.HasFlag(ToolCallEnvironment.PlayMode))
                modeList.Add("Play Mode");
            if (requiredModes.HasFlag(ToolCallEnvironment.EditMode))
                modeList.Add("Edit Mode");
            string requiredModesText = modeList.Count == 1 ? modeList[0] : string.Join(" or ", modeList);

            if (currentMode == UnityEnvironment.PlayMode && requiredModes == ToolCallEnvironment.EditMode)
            {
                return "The Unity Editor is currently in Play Mode but this tool requires the editor to be in Edit Mode. ";
            }
            else if (currentMode == UnityEnvironment.EditMode && requiredModes == ToolCallEnvironment.PlayMode)
            {
                return "The Unity Editor is currently in Edit Mode but this tool requires the editor to be in Play Mode. ";
            }
            else if (currentMode == UnityEnvironment.Runtime)
            {
                return $"This tool requires Unity to be in {requiredModesText}, but the game is currently running in Runtime Mode. " +
                       "The tool needs to run in the Unity Editor.";
            }
            else
            {
                // Generic fallback
                return $"This tool requires Unity to be in {requiredModesText}, but Unity is currently in {currentMode.ToString()}. ";
            }
        }

        internal static void LogAndThrow(String errorMessage)
        {
            InternalLog.LogError(errorMessage);
            throw new InvalidOperationException(errorMessage);
        }
    }
}
