using System;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class RunCommandTool
    {
        internal const string k_FunctionId = "Unity.RunCommand";
        internal const string k_CodeRequiredMessage = "Code parameter cannot be empty.";
        internal const string k_CommandBuildFailedMessage = "Failed to build agent command.";
        
        internal const string k_CommandExecutionFailedMessage = "Execution failed:\n{0}";
        internal const string k_CommandExecutionWarningsMessage = "Command was executed partially, but reported warnings or errors:\n{0}\nConsider reverting changes that may have happened if you retry.";
        internal const string k_PlayModeWritesDiscardedNote = "\n\nThis command ran in Play Mode. Scene changes it made are discarded when Play Mode exits and do not persist.";

        [Serializable]
        public struct ExecutionOutput
        {
            [JsonProperty("isExecutionSuccessful")]
            public bool IsExecutionSuccessful;

            [JsonProperty("executionId")]
            public int ExecutionId;

            [JsonProperty("executionLogs")]
            public string ExecutionLogs;
        }

        [AgentTool(
            "Execute a C# script in the Unity environment. The script will be compiled and executed, returning the results.",
            k_FunctionId)]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode | ToolCallEnvironment.PlayMode,
            tags: FunctionCallingUtilities.k_CodeExecutionTag)]
        public static async Task<ExecutionOutput> ExecuteCommand(
            ToolExecutionContext context,
            [ToolParameter("The C# script code to execute. Should implement IRunCommand interface or be a valid C# script.")]
            string code,
            [ToolParameter("Title for the execution command")]
            string title)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException(k_CodeRequiredMessage);

            var agentCommand = RunCommandUtils.BuildRunCommand(code);
            if (agentCommand == null)
                throw new InvalidOperationException(k_CommandBuildFailedMessage);

            await context.Permissions.CheckCodeExecution(code);

            if (!agentCommand.CompilationSuccess)
            {
                var errorMessage = agentCommand.UnauthorizedNamespaceError
                    ?? $"{k_CommandBuildFailedMessage}\n{agentCommand.CompilationErrors}";
                throw new InvalidOperationException(
                    RunCommandUtils.WithDeprecationGuidance(errorMessage, agentCommand));
            }

            // Only auto-bypass the unsafe prompt where the interaction surface can actually present the approval dialog to a user.
            var canAutoApprove = context.Interactions.CanPromptUser && Unity.AI.Assistant.Editor.AssistantEditorPreferences.AutoApproveUnsafeOperations;
            if (agentCommand.Unsafe && !canAutoApprove)
            {
                var approvalInteraction = new UnsafeCommandApprovalInteraction(agentCommand.Script, title, agentCommand.UnsafeOperations);
                var approved = await context.Interactions.WaitForUser(approvalInteraction);

                if (!approved)
                    throw new OperationCanceledException("User declined to execute the unsafe command.");
            }
            
            await EnforcePlayModeConsentAsync(context, agentCommand, EditorApplication.isPlaying);
            var ranInPlayMode = EditorApplication.isPlaying;

            var executionResult = RunCommandUtils.Execute(agentCommand, title);
            var formattedLogs = FormatLogs(executionResult);

            if (!executionResult.SuccessfullyStarted)
            {
                var logs = string.IsNullOrEmpty(formattedLogs) ? "No logs available" : formattedLogs;
                throw new InvalidOperationException(RunCommandUtils.WithDeprecationGuidance(
                    string.Format(k_CommandExecutionFailedMessage, logs), agentCommand));
            }

            var hasWarningsOrErrors = executionResult.Logs != null && executionResult.Logs.Any(log =>
                log.LogType == LogType.Warning ||
                log.LogType == LogType.Error ||
                log.LogType == LogType.Exception);

            if (hasWarningsOrErrors)
            {
                var logs = string.IsNullOrEmpty(formattedLogs) ? "No logs available" : formattedLogs;
                throw new InvalidOperationException(RunCommandUtils.WithDeprecationGuidance(
                    string.Format(k_CommandExecutionWarningsMessage, logs), agentCommand));
            }

            formattedLogs += InputSystemAnalyzer.Analyze(code);
            if (ranInPlayMode && agentCommand.HasWriteOperations)
                formattedLogs += k_PlayModeWritesDiscardedNote;
            formattedLogs = RunCommandUtils.WithDeprecationGuidance(formattedLogs, agentCommand);

            return new ExecutionOutput
            {
                IsExecutionSuccessful = true,
                ExecutionId = executionResult.Id,
                ExecutionLogs = formattedLogs
            };
        }

        internal static bool RequiresEditMode(AgentRunCommand agentCommand) =>
            agentCommand.Unsafe || agentCommand.HasEditModeOnlyOperations || agentCommand.RequestsPlayModeExit;

        internal static async Task EnforcePlayModeConsentAsync(ToolExecutionContext context, AgentRunCommand agentCommand, bool isPlaying)
        {
            if (isPlaying && RequiresEditMode(agentCommand))
            {
                try
                {
                    await FunctionCallingUtilities.ExitPlayModeWithConsentAsync(context);
                }
                catch (Exception e)
                {
                    // No inner exception: the function caller reports InnerException.Message first, which would drop this guidance.
                    throw new InvalidOperationException(
                        $"This command cannot run in Play Mode because it performs {DescribeEditModeOperations(agentCommand)}. Play Mode was not exited ({e.Message}). Do not retry this command while in Play Mode.");
                }

                isPlaying = false;
            }

            // A script that starts Play Mode needs the same consent as the EnterPlayMode tool.
            if (!isPlaying && agentCommand.RequestsPlayModeEnter)
                await context.Permissions.CheckPlayMode(PermissionPlayModeOperation.Enter);
        }

        static string DescribeEditModeOperations(AgentRunCommand agentCommand)
        {
            var operations = agentCommand.UnsafeOperations.Concat(agentCommand.EditModeOperations).ToList();
            return operations.Count > 0 ? string.Join(", ", operations) : "operations that require Edit Mode";
        }

        static string FormatLogs(ExecutionResult executionResult)
        {
            if (executionResult?.Logs == null || executionResult.Logs.Count == 0)
                return string.Empty;

            return string.Join("\n", executionResult.GetFormattedLogs());
        }
    }
}
