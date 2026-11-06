using System;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class CompilerTool
    {
        internal const string k_FunctionId = "Unity.Muse.Chat.Backend.Socket.Tools.Compiler";

        static readonly DynamicAssemblyBuilder k_Builder = new("Unity.Assistant.CodeGen");

        [Serializable]
        public struct CompileOutput
        {
            [JsonProperty("isCompilationSuccessful")] // must match Python code
            public bool IsCompilationSuccessful;

            [JsonProperty("compilationLogs")] // must match Python code
            public string CompilationLogs;

            [JsonProperty("localFixedCode")] // must match Python code
            public string LocalFixedCode;
        }

        [AgentTool("Let's you try to compile code on the frontend from the backend",
            k_FunctionId)]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode | ToolCallEnvironment.PlayMode,
            tags: FunctionCallingUtilities.k_CodeCorrectionTag)]
        public static CompileOutput Compiler([ToolParameter("The code to attempt to compile")] string code)
        {
            var compilationSuccessful = k_Builder.TryCompileCode(code, out var compilationErrors, out var compilation);

            return new CompileOutput
            {
                IsCompilationSuccessful = compilationSuccessful,
                CompilationLogs = compilationErrors.ToString(),
                LocalFixedCode = compilation.GetSourceCode()
            };
        }
    }
}
