using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.Editor.Utils.Event
{
    /// <summary>
    /// The backend started or finished compacting (summarizing) a conversation's context mid-turn.
    /// Sent from the streamed <c>&lt;COMPACTION&gt;</c> status frame; never part of the stored transcript.
    /// </summary>
    class EventCompactionStateChanged : IAssistantEvent
    {
        public EventCompactionStateChanged(AssistantConversationId conversationId, string agent, bool active)
        {
            ConversationId = conversationId;
            Agent = agent;
            Active = active;
        }

        public AssistantConversationId ConversationId { get; }

        /// <summary>The agent compacting; the backend only sends the core agent's.</summary>
        public string Agent { get; }

        public bool Active { get; }
    }
}
