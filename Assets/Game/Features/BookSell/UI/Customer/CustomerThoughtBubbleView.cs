using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Book.Sell.UI.Customer
{
    // Holds references to the four sub-views inside the bubble prefab. Each sub-view is a child
    // GameObject with its own CanvasGroup so the controller can crossfade between them.
    public sealed class CustomerThoughtBubbleView : MonoBehaviour
    {
        [Header("Sub-views (each with its own CanvasGroup)")]
        [SerializeField] private CanvasGroup _dotsGroup;
        [SerializeField] private CanvasGroup _bookGroup;
        [SerializeField] private CanvasGroup _commentGroup;
        [SerializeField] private CanvasGroup _rejectionGroup;

        [Header("Book sub-view content")]
        [SerializeField] private Image _bookIcon;
        [SerializeField] private Transform _bookScaleTarget;

        [Header("Comment sub-view content")]
        [SerializeField] private TextMeshProUGUI _stateText;
        [SerializeField] private TextMeshProUGUI _commentText;

        [Header("Rejection sub-view content")]
        [SerializeField] private Image _rejectedBookIcon;
        [SerializeField] private Image _replacementBookIcon;
        [SerializeField] private Transform _rejectionScaleTarget;

        [Header("Sale result icons")]
        [SerializeField] private Image _successIcon;
        [SerializeField] private Image _failIcon;

        public CanvasGroup DotsGroup => _dotsGroup;
        public CanvasGroup BookGroup => _bookGroup;
        public CanvasGroup CommentGroup => _commentGroup;
        public CanvasGroup RejectionGroup => _rejectionGroup;

        public Image BookIcon => _bookIcon;
        public Transform BookScaleTarget => _bookScaleTarget;

        public TextMeshProUGUI CommentText => _commentText;
        public TextMeshProUGUI StateText => _stateText != null ? _stateText : _commentText;

        public Image RejectedBookIcon => _rejectedBookIcon;
        public Image ReplacementBookIcon => _replacementBookIcon;
        public Transform RejectionScaleTarget => _rejectionScaleTarget;

        public Image SuccessIcon => _successIcon;
        public Image FailIcon => _failIcon;
    }
}
