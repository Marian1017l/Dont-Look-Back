using System;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Utils
{
    /// <summary>
    /// Helper class to keep track of and dismiss popup style visual elements
    /// Can also auto-align the popup to an anchor control, and limit an upward-growing popup to
    /// the space above it
    /// </summary>
    class PopupTracker : IDisposable
    {
        /// <summary>
        /// Floor for the clamp, below which the popup would be too short to interact with.
        /// Overflowing the bounds beats collapsing out of sight.
        /// </summary>
        internal const float MinClampedHeight = 80f;

        readonly ManagedTemplate m_Root;
        readonly VisualElement m_PopupButtonElement;
        readonly VisualElement m_Anchor;
        readonly bool m_AutoAlign;
        readonly VisualElement m_SpaceAboveBounds;

        Vector2Int m_AlignOffset = new(-7, 28);
        Vector2Int m_MinMargin = new(8, 8);

        /// <summary>
        /// <paramref name="spaceAboveBoundsElement"/>, when set, limits the popup's height to the space
        /// between that element's top edge and the popup's own bottom edge. It is only meaningful for a
        /// popup whose bottom edge is fixed independently of its height, as
        /// <c>.selectionpopup__element</c> is by its USS <c>bottom: 100%</c>. Auto-alignment to an
        /// anchor moves the bottom edge with the height, so the two cannot be combined.
        /// </summary>
        public PopupTracker(
            ManagedTemplate root,
            VisualElement popupButtonElement,
            VisualElement anchorElement = null,
            bool autoAlignToAnchor = true,
            Vector2Int? alignOffset = null,
            VisualElement spaceAboveBoundsElement = null
        ) {
            if (spaceAboveBoundsElement != null && anchorElement != null && autoAlignToAnchor)
            {
                throw new ArgumentException(
                    $"{nameof(spaceAboveBoundsElement)} needs a bottom edge that does not move with the popup's height, which {nameof(autoAlignToAnchor)} does not give it.",
                    nameof(spaceAboveBoundsElement));
            }

            m_Root = root;
            m_PopupButtonElement = popupButtonElement;
            m_Anchor = anchorElement;
            m_AutoAlign = autoAlignToAnchor;
            m_SpaceAboveBounds = spaceAboveBoundsElement;

            if (alignOffset.HasValue)
            {
                m_AlignOffset = alignOffset.Value;
            }

            InitializePopupTracker();
        }

        void InitializePopupTracker()
        {
            m_Root.RegisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
            m_Root.panel.visualTree.RegisterCallback<PointerDownEvent>(OnTreeDown, TrickleDown.TrickleDown);
            m_Root.panel.visualTree.RegisterCallback<GeometryChangedEvent>(OnTreeGeometryChanged);
            if (m_Anchor != null)
            {
                m_Anchor.RegisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
                m_Anchor.parent.RegisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
            }
            if (m_AutoAlign)
            {
                RealignPopup();
            }

            ClampToSpaceAbove();
        }

        public event Action Dismiss;

        public Vector2Int AlignOffset
        {
            get => m_AlignOffset;
            set
            {
                if (m_AlignOffset != value)
                {
                    m_AlignOffset = value;
                    RealignPopup();
                }
            }
        }

        public Vector2Int MinMargin
        {
            get => m_MinMargin;
            set
            {
                if (m_MinMargin != value)
                {
                    m_MinMargin = value;
                    RealignPopup();
                    ClampToSpaceAbove();
                }
            }
        }

        public void Dispose()
        {
            m_Root.UnregisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
            m_Root.panel?.visualTree.UnregisterCallback<PointerDownEvent>(OnTreeDown, TrickleDown.TrickleDown);
            m_Root.panel?.visualTree.UnregisterCallback<GeometryChangedEvent>(OnTreeGeometryChanged);

            if (m_Anchor != null)
            {
                m_Anchor.UnregisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
                m_Anchor.parent.UnregisterCallback<GeometryChangedEvent>(OnAlignGeometryChanged);
            }

            if (m_SpaceAboveBounds != null)
            {
                m_Root.style.maxHeight = StyleKeyword.Null;
            }
        }

        bool CheckDismissalFromClick(VisualElement clickedElement)
        {
            if (clickedElement == null)
            {
                return true;
            }

            if (m_Anchor != null && clickedElement.FindCommonAncestor(m_Anchor) == m_Anchor)
            {
                return false;
            }

            if (clickedElement.FindCommonAncestor(m_Root) == m_Root)
            {
                return false;
            }

            return true;
        }

        void OnTreeDown(PointerDownEvent evt)
        {
            if (!CheckDismissalFromClick(evt.target as VisualElement))
            {
                return;
            }

            if (evt.target == m_PopupButtonElement)
            {
                return;
            }

            Dismiss?.Invoke();
        }

        void OnTreeGeometryChanged(GeometryChangedEvent evt)
        {
            Dismiss?.Invoke();
        }

        void OnAlignGeometryChanged(GeometryChangedEvent evt)
        {
            RealignPopup();
            ClampToSpaceAbove();
        }

        public void RealignPopup()
        {
            if (!m_AutoAlign || m_Anchor == null)
            {
                return;
            }

            var popupBounds = m_Root.worldBound;
            if (popupBounds.width == 0 || popupBounds.height == 0)
            {
                return;
            }

            var anchorBounds = m_Anchor.worldBound;
            m_Root.style.left = Math.Max(m_MinMargin.x, anchorBounds.xMax - anchorBounds.width + m_AlignOffset.x);
            m_Root.style.top = Math.Max(m_MinMargin.y, anchorBounds.yMax - popupBounds.height - m_AlignOffset.y);
        }

        /// <summary>
        /// Height the popup may take without crossing <paramref name="boundsAbove"/>'s top edge.
        /// </summary>
        internal static float ComputeMaxHeight(Rect popupBounds, Rect boundsAbove, float margin)
        {
            return Mathf.Max(MinClampedHeight, popupBounds.yMax - boundsAbove.yMin - margin);
        }

        void ClampToSpaceAbove()
        {
            if (m_SpaceAboveBounds == null)
            {
                return;
            }

            var popupBounds = m_Root.worldBound;
            if (popupBounds.height == 0)
            {
                return;
            }

            // Reading back yMax is safe only because the caller guarantees a height-independent
            // bottom edge, so the write below cannot change the next pass's input.
            m_Root.style.maxHeight = ComputeMaxHeight(popupBounds, m_SpaceAboveBounds.worldBound, m_MinMargin.y);
        }
    }
}
