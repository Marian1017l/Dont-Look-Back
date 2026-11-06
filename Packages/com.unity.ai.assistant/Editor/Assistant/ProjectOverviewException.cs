using System;
using Unity.AI.Assistant.Socket.Workflows.Chat;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// A project overview refresh that failed its server round-trip, as opposed to an unexpected defect.
    /// Callers use this distinction to choose the log severity, and <see cref="Reason"/> to choose the
    /// message shown to the user.
    /// </summary>
    class ProjectOverviewException : Exception
    {
        public CloseReason.ReasonType Reason { get; }

        public ProjectOverviewException(string message, CloseReason.ReasonType reason)
            : base(message)
        {
            Reason = reason;
        }
    }
}
