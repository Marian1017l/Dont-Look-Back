using System;
using AiEditorToolsSdk.Components.Organization.Responses;

namespace Unity.AI.Toolkit.Accounts.Services.Data
{
    [Serializable]
    record SettingsRecord
    {
        public string OrgId;
        public bool IsAiAssistantEnabled;
        public bool IsAiGeneratorsEnabled;
        public bool IsDataSharingEnabled;
        public bool IsTermsOfServiceAccepted;
        public bool IsMcpProEnabled;
        public bool CanSpendPoints;

        // Frictionless free credits: the user needs no seat to spend them, so this passes the seat
        // gate alongside CanSpendPoints.
        public bool SeatExempt;

        public enum AiSubscription { Unknown, No, Yes }

        // Unity's serializer drops Nullable<T>, and this record is persisted through a
        // ScriptableSingleton, so the tri-state is held in an enum that survives a domain reload.
        public AiSubscription AiSubscriptionState;

        // Whether the org holds a paid plan that covers AI (a Pro/Enterprise/Industry licence or an
        // active AI subscription). Null means the backend could not determine it — distinct from a
        // definite "no", so callers must not treat it as one.
        public bool? HasAiSubscription
        {
            get => AiSubscriptionState switch
            {
                AiSubscription.Yes => true,
                AiSubscription.No => false,
                _ => null
            };
            set => AiSubscriptionState = value switch
            {
                true => AiSubscription.Yes,
                false => AiSubscription.No,
                _ => AiSubscription.Unknown
            };
        }

        // The org the user has chosen to bill AI usage to, when that differs from the working org.
        // Null when there is no override, in which case the working org pays.
        public string OptedInBillingOrgId;

        // Per-pool connection caps. 0 is a legitimate entitlement value under
        // the post-2026-04 business model ("free tier: no connections"). These
        // SDK properties are non-nullable, so backend values are always treated
        // as candidates once settings exist and then maxed with local licensing.
        public int AllowedGatewayConnections;
        public int AllowedMcpConnections;

        public SettingsRecord(SettingsResult result)
        {
            OrgId = result?.OrgId;
            IsAiAssistantEnabled = result is { IsAiAssistantEnabled: true };
            IsAiGeneratorsEnabled = result is { IsAiGeneratorsEnabled: true };
            IsDataSharingEnabled = result is { IsDataSharingEnabled: true };
            IsTermsOfServiceAccepted = result is { IsTermsOfServiceAccepted: true };
            IsMcpProEnabled = result is { IsMcpProEnabled: true };
            CanSpendPoints = result is { CanSpendPoints: true };
            SeatExempt = result is { SeatExempt: true };
            HasAiSubscription = result?.HasAiSubscription;
            OptedInBillingOrgId = result?.OptedInBillingOrgId;
            AllowedGatewayConnections = result?.AllowedGatewayConnections ?? 0;
            AllowedMcpConnections = result?.AllowedMcpConnections ?? 0;
        }
    }

    [Serializable]
    record PointsBalanceRecord
    {
        public string OrgId;
        public long PointsAllocated;
        public long PointsAvailable;

        public PointsBalanceRecord(PointsBalanceResult result)
        {
            OrgId = result?.OrgId;
            PointsAllocated = result?.PointsAllocated ?? 0;
            PointsAvailable = result?.PointsAvailable ?? 0;
        }
    }

    [Serializable]
    enum SignInStatus
    {
        NotReady,
        SignedIn,
        SignedOut,
    }

    [Serializable]
    enum ProjectStatus
    {
        NotReady,
        Connected,
        NotConnected,
        OfflineConnected,
    }
}
