using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    class RunCommandMetadata
    {
        public bool IsUnsafe { get; set; }
        public bool HasWriteOperations { get; set; }
        public bool RequiresEditMode { get; set; }
        public bool RequestsPlayModeExit { get; set; }
        public bool RequestsPlayModeEnter { get; set; }
        public List<string> UnsafeOperations { get; } = new();
        public List<string> EditModeOperations { get; } = new();
    }
}
