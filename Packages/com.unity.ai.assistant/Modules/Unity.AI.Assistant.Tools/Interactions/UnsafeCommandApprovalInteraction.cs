using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    class UnsafeCommandApprovalInteraction : IInteractionSource<bool>, IApprovalInteraction
    {
        public string Script { get; }
        public string Action { get; }
        public string Detail { get; }
        public string AllowLabel => "Proceed";
        public string DenyLabel => "Cancel";
        public bool ShowScope => false;

        public event Action<bool> OnCompleted;
        public TaskCompletionSource<bool> TaskCompletionSource { get; } = new();

        const string k_BaseDetail = "This command performs non-revertable actions.";

        public UnsafeCommandApprovalInteraction(string script, string title, IReadOnlyList<string> unsafeOperations)
        {
            Script = script;
            // The headline is rendered as rich text; strip any markup so a model-supplied title can't forge the gate's chrome.
            var plainTitle = string.IsNullOrEmpty(title) ? title : Regex.Replace(title, "<.*?>", string.Empty, RegexOptions.Singleline);
            Action = string.IsNullOrEmpty(plainTitle) ? "Run unsafe command" : $"Run unsafe command: {plainTitle}";
            Detail = BuildDetail(unsafeOperations);
        }

        static string BuildDetail(IReadOnlyList<string> unsafeOperations)
        {
            if (unsafeOperations == null || unsafeOperations.Count == 0)
                return k_BaseDetail;

            var operations = string.Join(", ", unsafeOperations.Select(Shorten));
            return $"Non-revertable: {operations}";
        }

        static string Shorten(string operation)
        {
            var segments = operation.Split('.');
            return segments.Length <= 2 ? operation : string.Join(".", segments.Skip(segments.Length - 2));
        }

        public void Respond(PermissionUserAnswer answer)
        {
            var approved = answer == PermissionUserAnswer.AllowOnce
                || answer == PermissionUserAnswer.AllowAlways;
            Complete(approved);
        }

        public void Complete(bool approved)
        {
            TaskCompletionSource.TrySetResult(approved);
            OnCompleted?.Invoke(approved);
        }

        public void CancelInteraction()
        {
            TaskCompletionSource.TrySetCanceled();
        }
    }
}
