using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ProgressElement : ManagedTemplate
    {
        VisualElement m_IconElement;
        Label m_StatusLabel;
        Label m_DetailLabel;
        bool m_Running;
        double m_StartTime;
        int m_CurrentFrame;
        double m_LastFrameTime;
        string[] m_FrameClassNames;
        bool m_WaitingForFirstMessage;
        BaseEventSubscriptionTicket m_InteractionQueueSubscription;
        BaseEventSubscriptionTicket m_InlineInteractionSubscription;
        BaseEventSubscriptionTicket m_InlineInteractionCompletedSubscription;
        BaseEventSubscriptionTicket m_CompactionSubscription;

        // The backend is compacting the active conversation; only the core agent's
        // compaction is sent, since that is what the user is waiting on.
        bool m_Compacting;

        // Blocks in the last message when compaction began. The backend streams nothing
        // during a fold, so any new block means it is over -- which ends "Compacting..."
        // even if the end frame itself was lost.
        int m_BlockCountAtCompaction;

        const float k_FrameInterval = 0.125f;
        const int k_FrameCount = 6;
        const string k_IconClassName = "mui-icon-progress-frame-"; // Add the number at the end to pick an icon
        const string k_PreparingMessage = "Preparing...";
        const string k_ReasoningMessage = "Reasoning...";
        const string k_RespondingMessage = "Responding...";
        const string k_CancelingMessage = "Canceling";
        const string k_WaitingForInputMessage = "Awaiting input...";
        const string k_CompactingMessage = "Compacting...";

        public ProgressElement() : base(AssistantUIConstants.UIModulePath) { }

        public void Start()
        {
            if (m_Running)
                return;

            // Ensure we are on the first frame
            for (var i = 0; i < k_FrameCount; ++i)
            {
                m_IconElement.RemoveFromClassList(k_IconClassName + i);
            }
            m_IconElement.AddToClassList(k_IconClassName + 0);

            m_Running = true;
            m_StartTime = ResumeOrCaptureStartTime();
            m_CurrentFrame = 0;
            m_LastFrameTime = m_StartTime;
            m_WaitingForFirstMessage = true;
            m_Compacting = ResumeCompacting();
            if (m_Compacting)
                m_BlockCountAtCompaction = LastMessageBlockCount();
            else
                AssistantUISessionState.instance.ClearCompactingState();

            EditorApplication.update += Update;
            UpdateMessage();
            Show();
        }

        public void Stop()
        {
            if (!m_Running)
                return;

            m_Running = false;
            EditorApplication.update -= Update;

            // Reset state
            m_WaitingForFirstMessage = false;
            m_Compacting = false;
            AssistantUISessionState.instance.ClearProgressStartTime();
            AssistantUISessionState.instance.ClearCompactingState();

            Hide();
        }

        // Reuses the persisted start time so the elapsed counter does not reset on domain reload.
        static double ResumeOrCaptureStartTime()
        {
            var persisted = AssistantUISessionState.instance.ProgressStartTime;
            if (persisted > 0 && persisted <= EditorApplication.timeSinceStartup)
                return persisted;

            var now = EditorApplication.timeSinceStartup;
            AssistantUISessionState.instance.ProgressStartTime = (float)now;
            return now;
        }

        // Restores "Compacting..." after a domain reload. A turn that writes a script triggers
        // one, and the element is rebuilt without the end frame having arrived yet. Only for the
        // same conversation AND the same in-flight turn: a reload or window close can also lose
        // the end frame, and a key left behind then must not resurface on a later turn, or on a
        // conversation whose turn has since finished.
        bool ResumeCompacting()
        {
            var session = AssistantUISessionState.instance;
            var persistedConversation = session.CompactingConversationId;
            var persistedTurn = session.CompactingTurnId;
            var active = Context.Blackboard.ActiveConversationId;
            return !string.IsNullOrEmpty(persistedConversation)
                && !string.IsNullOrEmpty(persistedTurn)
                && active.IsValid && persistedConversation == active.Value
                && persistedTurn == session.IncompleteMessageId;
        }

        int LastMessageBlockCount()
        {
            var messages = Context.Blackboard.ActiveConversation?.Messages;
            if (messages == null || messages.Count == 0)
                return 0;

            return messages[^1].Blocks?.Count ?? 0;
        }

        void EndCompacting()
        {
            m_Compacting = false;
            AssistantUISessionState.instance.ClearCompactingState();
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_IconElement = view.Q<VisualElement>("progressIcon");
            m_StatusLabel = view.Q<Label>("progressStatus");
            m_DetailLabel = view.Q<Label>("progressDetail");

            // Pre-cache frame class names to avoid string allocation during animation
            m_FrameClassNames = new string[k_FrameCount];
            for (int i = 0; i < k_FrameCount; i++)
            {
                m_FrameClassNames[i] = k_IconClassName + i;
            }

            Context.Blackboard.ActiveConversationChanged += OnActiveConversationChanged;
            Context.API.APIStateChanged += OnAPIStateChanged;
            Context.API.ConversationChanged += OnConversationChanged;
            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            m_InteractionQueueSubscription = AssistantEvents.Subscribe<EventInteractionQueueChanged>(_ => UpdateMessage());
            m_InlineInteractionSubscription = AssistantEvents.Subscribe<EventInlineInteractionPushed>(_ => UpdateMessage());
            m_InlineInteractionCompletedSubscription = AssistantEvents.Subscribe<EventInlineInteractionCompleted>(_ => UpdateMessage());
            m_CompactionSubscription = AssistantEvents.Subscribe<EventCompactionStateChanged>(OnCompactionStateChanged);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssistantEvents.Unsubscribe(ref m_InteractionQueueSubscription);
            AssistantEvents.Unsubscribe(ref m_InlineInteractionSubscription);
            AssistantEvents.Unsubscribe(ref m_InlineInteractionCompletedSubscription);
            AssistantEvents.Unsubscribe(ref m_CompactionSubscription);
        }

        void OnCompactionStateChanged(EventCompactionStateChanged evt)
        {
            if (evt.ConversationId != Context.Blackboard.ActiveConversationId)
                return;

            if (evt.Active)
            {
                m_Compacting = true;
                m_BlockCountAtCompaction = LastMessageBlockCount();
                var session = AssistantUISessionState.instance;
                if (evt.ConversationId.IsValid && !string.IsNullOrEmpty(session.IncompleteMessageId))
                {
                    session.CompactingConversationId = evt.ConversationId.Value;
                    session.CompactingTurnId = session.IncompleteMessageId;
                }
            }
            else
            {
                EndCompacting();
            }

            UpdateMessage();
        }

        void OnActiveConversationChanged(AssistantConversationId previousId, AssistantConversationId newId)
        {
            if (previousId.IsValid)
            {
                Stop();
            }
        }

        void OnAPIStateChanged()
        {
            if (Context.Blackboard.IsAPIWorking || Context.Blackboard.IsAPICanceling)
            {
                if (!m_Running)
                {
                    Start();
                }
            }
            else
            {
                Stop();
            }

            UpdateMessage();
        }

        void OnConversationChanged(AssistantConversationId conversationId)
        {
            if (m_Compacting && LastMessageBlockCount() > m_BlockCountAtCompaction)
                EndCompacting();

            if (m_WaitingForFirstMessage)
            {
                var conversation = Context.Blackboard.ActiveConversation;
                if (conversation?.Messages?.Count > 0)
                {
                    var lastMessage = conversation.Messages[^1];
                    if (lastMessage.Blocks?.Count > 0)
                        m_WaitingForFirstMessage = false;
                }
            }

            UpdateMessage();
        }

        bool HasBlocksInLastMessage()
        {
            var conversation = Context.Blackboard.ActiveConversation;
            if (conversation?.Messages == null || conversation.Messages.Count == 0)
                return false;

            var lastMessage = conversation.Messages[^1];
            return lastMessage.Blocks?.Count > 0;
        }

        bool IsLastBlockResponseBlock()
        {
            var conversation = Context.Blackboard.ActiveConversation;
            if (conversation?.Messages == null || conversation.Messages.Count == 0)
                return false;

            var lastMessage = conversation.Messages[^1];
            if (lastMessage.Blocks == null || lastMessage.Blocks.Count == 0)
                return false;

            var lastBlock = lastMessage.Blocks[^1];
            return lastBlock is AnswerBlockModel;
        }

        bool IsWaitingForUserInput()
        {
            // Total counts only non-persistent entries (permissions, ask_user, etc.);
            // the persistent todo panel does not count as blocking user input.
            return Context.InteractionQueue?.Total > 0
                || Context.PendingInlineInteractions?.Count > 0;
        }

        void UpdateMessage()
        {
            if (!m_Running)
                return;

            string message;

            // Check canceling first
            if (Context.Blackboard.IsAPICanceling)
            {
                message = k_CancelingMessage;
            }
            // Must beat m_WaitingForFirstMessage so a restored interaction shows "awaiting input".
            else if (IsWaitingForUserInput())
            {
                message = k_WaitingForInputMessage;
            }
            // Must beat the preparing/reasoning states: compaction can run before the first
            // block of a turn or between steps, and blocks the turn until it finishes.
            else if (m_Compacting)
            {
                message = k_CompactingMessage;
            }
            // If waiting for first message after Start(), stay in preparing
            else if (m_WaitingForFirstMessage)
            {
                message = k_PreparingMessage;
            }
            // If no blocks in last message, we're preparing
            else if (!HasBlocksInLastMessage())
            {
                message = k_PreparingMessage;
            }
            // If last block is an AnswerBlockModel, we're responding
            else if (IsLastBlockResponseBlock())
            {
                message = k_RespondingMessage;
            }
            // Otherwise (last block is not AnswerBlockModel), we're reasoning
            else
            {
                message = k_ReasoningMessage;
            }

            m_StatusLabel.text = message;
        }

        void Update()
        {
            if (!m_Running)
                return;

            // Update icon animation
            var currentTime = EditorApplication.timeSinceStartup;
            if (currentTime - m_LastFrameTime >= k_FrameInterval)
            {
                m_CurrentFrame = (m_CurrentFrame + 1) % k_FrameCount;
                m_LastFrameTime = currentTime;
                UpdateIcon();
            }

            // Update elapsed time
            var elapsed = (int)(currentTime - m_StartTime);
            m_DetailLabel.text = $"({elapsed}s)";
        }

        void UpdateIcon()
        {
            // Set the background image to the appropriate cycling frame image
            var previousFrame = (m_CurrentFrame - 1 + k_FrameCount) % k_FrameCount;

            m_IconElement.RemoveFromClassList(m_FrameClassNames[previousFrame]);
            m_IconElement.AddToClassList(m_FrameClassNames[m_CurrentFrame]);
        }
    }
}
