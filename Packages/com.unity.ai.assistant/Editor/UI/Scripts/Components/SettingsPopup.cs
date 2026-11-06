using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// A popup menu for quick access to some assistant user settings.
    /// </summary>
    class SettingsPopup : ManagedTemplate
    {
        const string k_RefreshErrorVisibleClass = "settings-popup-error--visible";
        const string k_RefreshConnectionError = "Couldn't refresh the project overview. Check your network connection and try again.";
        const string k_RefreshAuthenticationError = "Couldn't refresh the project overview. Sign in again and retry.";
        const string k_RefreshCapacityError = "Couldn't refresh the project overview. The AI Assistant server is at capacity. Try again shortly.";
        const string k_RefreshUnexpectedError = "Couldn't refresh the project overview. See the Console for details.";

        Button m_OpenPreferencesButton;
        Toggle m_AutoRunToggle;
        Label m_AutoRunLabel;
        Toggle m_CollapseReasoningToggle;
        Toggle m_AutoApproveUnsafeToggle;
        VisualElement m_AutoRunRow;
        VisualElement m_CollapseReasoningRow;
        VisualElement m_AutoApproveUnsafeRow;
        VisualElement m_SessionOverridesRow;
        VisualElement m_OverrideSeparator;
        Foldout m_SessionOverridesFoldout;
        ScrollView m_SessionOverridesContent;
        Button m_RefreshProjectOverviewButton;
        VisualElement m_LoadingSpinnerContainer;
        LoadingSpinner m_LoadingSpinner;
        VisualElement m_RefreshErrorContainer;
        Label m_RefreshErrorText;

        public SettingsPopup() : base(AssistantUIConstants.UIModulePath)
        {
            style.display = DisplayStyle.None;
        }

        public void ShowWithPermissions(IList<IToolPermissions.TemporaryPermission> permissions)
        {
            UpdateAutoRunVisibility();
            UpdateRefreshProjectOverviewVisibility();
            SetRefreshError(null);
            Show();

            // Update toggle values from preferences
            m_AutoRunToggle.SetValueWithoutNotify(AssistantEditorPreferences.AutoRun);
            m_CollapseReasoningToggle.SetValueWithoutNotify(AssistantEditorPreferences.CollapseReasoningWhenComplete);
            m_AutoApproveUnsafeToggle.SetValueWithoutNotify(AssistantEditorPreferences.AutoApproveUnsafeOperations);

            SetSessionOverrides(permissions);
        }

        public void SetAutoRunEnabled(bool enabled)
        {
            m_AutoRunToggle.SetEnabled(enabled);
            m_AutoRunLabel.EnableInClassList("disabled", !enabled);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var root = view.Q<VisualElement>("settingsPopupRoot");

            // Query elements
            m_OpenPreferencesButton = root.Q<Button>("openPreferencesButton");
            m_AutoRunToggle = root.Q<Toggle>("autoRunToggle");
            m_CollapseReasoningToggle = root.Q<Toggle>("collapseReasoningToggle");
            m_AutoApproveUnsafeToggle = root.Q<Toggle>("autoApproveUnsafeToggle");
            m_AutoRunRow = root.Q<VisualElement>("autoRunRow");
            m_AutoRunLabel = m_AutoRunRow.Q<Label>();
            m_CollapseReasoningRow = root.Q<VisualElement>("collapseReasoningRow");
            m_AutoApproveUnsafeRow = root.Q<VisualElement>("autoApproveUnsafeRow");
            m_SessionOverridesRow = root.Q<VisualElement>("sessionOverridesRow");
            m_SessionOverridesFoldout = root.Q<Foldout>("sessionOverridesFoldout");
            m_SessionOverridesContent = root.Q<ScrollView>("sessionOverridesContent");
            m_OverrideSeparator = root.Q("overridesSeparator");

            m_RefreshProjectOverviewButton = root.Q<Button>("refreshProjectOverviewButton");
            m_LoadingSpinnerContainer = view.Q("loadingSpinnerContainer");
            m_LoadingSpinner = new LoadingSpinner();
            m_LoadingSpinner.style.marginRight = 4;
            m_LoadingSpinnerContainer.Add(m_LoadingSpinner);
            m_LoadingSpinner.Hide();

            m_RefreshErrorContainer = view.Q<VisualElement>("refreshProjectOverviewError");
            m_RefreshErrorText = view.Q<Label>("refreshProjectOverviewErrorText");

            // Setup event handlers
            m_OpenPreferencesButton.clicked += OnOpenPreferencesClicked;
            m_RefreshProjectOverviewButton.clicked += OnRefreshProjectOverviewButtonClicked;
            m_AutoRunToggle.RegisterValueChangedCallback(OnAutoRunChanged);
            m_CollapseReasoningToggle.RegisterValueChangedCallback(OnCollapseReasoningChanged);
            m_AutoApproveUnsafeToggle.RegisterValueChangedCallback(OnAutoApproveUnsafeChanged);

            // Make entire row clickable to toggle checkbox
            m_AutoRunRow.RegisterCallback<ClickEvent>(OnAutoRunRowClicked);
            m_CollapseReasoningRow.RegisterCallback<ClickEvent>(OnCollapseReasoningRowClicked);
            m_AutoApproveUnsafeRow.RegisterCallback<ClickEvent>(OnAutoApproveUnsafeRowClicked);

            // Session Overrides foldout setup
            m_SessionOverridesFoldout.value = false;
            m_SessionOverridesContent.style.display = DisplayStyle.None; // Initially collapsed
            m_SessionOverridesFoldout.RegisterValueChangedCallback(OnSessionOverridesFoldoutChanged);
            m_SessionOverridesRow.RegisterCallback<ClickEvent>(OnSessionOverridesRowClicked);

            // Prevent foldout toggle click from bubbling to row
            var foldoutToggle = m_SessionOverridesFoldout.Q<Toggle>();
            foldoutToggle.RegisterCallback<ClickEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);

            // Listen to preference changes from other sources (e.g., Settings page)
            AssistantEditorPreferences.AutoRunChanged += OnAutoRunPreferenceChanged;
            AssistantEditorPreferences.CollapseReasoningWhenCompleteChanged += OnCollapseReasoningPreferenceChanged;
            AssistantEditorPreferences.AutoApproveUnsafeOperationsChanged += OnAutoApproveUnsafePreferenceChanged;

            // Initialize toggle values
            m_AutoRunToggle.SetValueWithoutNotify(AssistantEditorPreferences.AutoRun);
            m_CollapseReasoningToggle.SetValueWithoutNotify(AssistantEditorPreferences.CollapseReasoningWhenComplete);
            m_AutoApproveUnsafeToggle.SetValueWithoutNotify(AssistantEditorPreferences.AutoApproveUnsafeOperations);
            UpdateAutoRunVisibility();
            UpdateRefreshProjectOverviewVisibility();

            // Unregister when element is removed from hierarchy
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            AssistantEditorPreferences.AutoRunChanged -= OnAutoRunPreferenceChanged;
            AssistantEditorPreferences.CollapseReasoningWhenCompleteChanged -= OnCollapseReasoningPreferenceChanged;
            AssistantEditorPreferences.AutoApproveUnsafeOperationsChanged -= OnAutoApproveUnsafePreferenceChanged;
        }

        void OnOpenPreferencesClicked()
        {
            AIAssistantAnalytics.ReportUITriggerLocalOpenAssistantSettingsEvent();
            SettingsService.OpenUserPreferences("Preferences/AI/Assistant");
            Hide();
        }

        async void OnRefreshProjectOverviewButtonClicked() => await RefreshProjectOverview();

        internal async Task RefreshProjectOverview()
        {
            SetRefreshError(null);
            m_LoadingSpinner.Show();
            m_RefreshProjectOverviewButton.SetEnabled(false);

            try
            {
                await Context.API.Provider.RefreshProjectOverview();
            }
            catch (ProjectOverviewException ex)
            {
                Debug.LogWarning($"Refresh Project Overview failed. {ex.Message}");
                SetRefreshError(GetRefreshErrorMessage(ex.Reason));
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                SetRefreshError(k_RefreshUnexpectedError);
            }
            finally
            {
                m_LoadingSpinner.Hide();
                m_RefreshProjectOverviewButton.SetEnabled(true);
            }
        }

        static string GetRefreshErrorMessage(CloseReason.ReasonType reason)
        {
            return reason switch
            {
                CloseReason.ReasonType.CouldNotConnect
                    or CloseReason.ReasonType.UnderlyingWebSocketWasClosed
                    or CloseReason.ReasonType.ChatResponseTimeout
                    or CloseReason.ReasonType.DiscussionInitializationTimeout => k_RefreshConnectionError,
                CloseReason.ReasonType.AuthenticationFailed => k_RefreshAuthenticationError,
                CloseReason.ReasonType.ServerNoCapacity => k_RefreshCapacityError,
                _ => k_RefreshUnexpectedError
            };
        }

        void SetRefreshError(string message)
        {
            m_RefreshErrorText.text = message;
            m_RefreshErrorContainer.EnableInClassList(k_RefreshErrorVisibleClass, !string.IsNullOrEmpty(message));
        }

        void UpdateAutoRunVisibility()
        {
            var showAutoRun = Context.API.Provider.AutoRunSettingAvailable;
            m_AutoRunRow.style.display = showAutoRun ? DisplayStyle.Flex : DisplayStyle.None;
            m_AutoApproveUnsafeRow.style.display = showAutoRun ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void UpdateRefreshProjectOverviewVisibility()
        {
            var showRefresh = Context.IsUnityProvider;
            m_RefreshProjectOverviewButton.style.display = showRefresh ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnAutoRunPreferenceChanged(bool newValue) => m_AutoRunToggle.SetValueWithoutNotify(newValue);

        void OnCollapseReasoningPreferenceChanged(bool newValue) => m_CollapseReasoningToggle.SetValueWithoutNotify(newValue);

        void OnAutoApproveUnsafePreferenceChanged(bool newValue) => m_AutoApproveUnsafeToggle.SetValueWithoutNotify(newValue);

        void OnAutoRunChanged(ChangeEvent<bool> evt)
        {
            AssistantEditorPreferences.AutoRun = evt.newValue;
            AIAssistantAnalytics.ReportUITriggerLocalAutoRunSettingChangedEvent(evt.newValue);
        }

        void OnCollapseReasoningChanged(ChangeEvent<bool> evt)
        {
            AssistantEditorPreferences.CollapseReasoningWhenComplete = evt.newValue;
            AIAssistantAnalytics.ReportUITriggerLocalCollapseReasoningSettingChangedEvent(evt.newValue, Context.Blackboard.ActiveMode.ToString());
        }

        void OnAutoApproveUnsafeChanged(ChangeEvent<bool> evt)
        {
            AssistantEditorPreferences.AutoApproveUnsafeOperations = evt.newValue;
            AIAssistantAnalytics.ReportUITriggerLocalAutoApproveUnsafeSettingChangedEvent(evt.newValue);
        }

        void OnAutoRunRowClicked(ClickEvent evt)
        {
            // If the click was directly on the toggle, it will handle itself
            if (evt.target is Toggle)
                return;

            // Don't toggle if disabled
            if (!m_AutoRunToggle.enabledSelf)
                return;

            m_AutoRunToggle.value = !m_AutoRunToggle.value;
        }

        void OnCollapseReasoningRowClicked(ClickEvent evt)
        {
            // If the click was directly on the toggle, it will handle itself
            if (evt.target is Toggle)
                return;

            m_CollapseReasoningToggle.value = !m_CollapseReasoningToggle.value;
        }

        void OnAutoApproveUnsafeRowClicked(ClickEvent evt)
        {
            // If the click was directly on the toggle, it will handle itself
            if (evt.target is Toggle)
                return;

            m_AutoApproveUnsafeToggle.value = !m_AutoApproveUnsafeToggle.value;
        }

        void OnSessionOverridesRowClicked(ClickEvent evt)
        {
            // If the click was directly on the foldout toggle, it will handle itself
            if (evt.target is Toggle)
                return;

            m_SessionOverridesFoldout.value = !m_SessionOverridesFoldout.value;
        }

        void OnSessionOverridesFoldoutChanged(ChangeEvent<bool> evt)
        {
            m_SessionOverridesContent.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// Sets the list of session overrides to display
        /// </summary>
        /// <param name="overrides">List of override labels to display</param>
        void SetSessionOverrides(IList<IToolPermissions.TemporaryPermission> overrides)
        {
            m_SessionOverridesContent.Clear();

            if (overrides == null || overrides.Count == 0)
            {
                // Hide the entire session overrides section if empty
                SetSessionOverridesVisibility(false);
                return;
            }

            // Show the session overrides section
            SetSessionOverridesVisibility(true);

            foreach (var overridePermission in overrides)
            {
                var item = CreateOverrideItem(overridePermission.Name, overridePermission.ResetFunction);
                m_SessionOverridesContent.Add(item);
            }
        }

        void SetSessionOverridesVisibility(bool isVisible)
        {
            var displayStyle = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
            m_SessionOverridesRow.style.display = displayStyle;
            m_OverrideSeparator.style.display = displayStyle;
        }

        /// <summary>
        /// Called when the remove button (X) is clicked for an override item
        /// Override this method to handle removal
        /// </summary>
        protected virtual void OnRemoveClicked(VisualElement item, Action resetCallback)
        {
            resetCallback();

            m_SessionOverridesContent.Remove(item);

            // Hide the section if no items remain
            if (m_SessionOverridesContent.childCount == 0)
                SetSessionOverridesVisibility(false);
        }

        VisualElement CreateOverrideItem(string label, Action resetCallback)
        {
            var item = new VisualElement();
            item.AddToClassList("settings-popup-override-item");

            var labelElement = new Label(label);
            labelElement.AddToClassList("settings-popup-override-label");

            var removeButton = new Image();
            removeButton.AddToClassList("settings-popup-override-remove");
            removeButton.AddToClassList("mui-icon-close");
            removeButton.RegisterCallback<ClickEvent>(_ => OnRemoveClicked(item, resetCallback));

            item.Add(labelElement);
            item.Add(removeButton);

            return item;
        }
    }
}
