using Book.Sell.Domain;
using UnityEngine;

namespace Book.Sell.UI.Customer
{
    // Placeholder visualization for a Customer POCO. Phase 0: a static sprite at a hardcoded
    // position with a child Transform that the world-space bubble can attach to.
    // Real customer art / movement / queue arrives in a later phase.
    public sealed class CustomerVisual : MonoBehaviour
    {
        [SerializeField] private SpriteRenderer _figure;
        [SerializeField] private Transform _bubbleAnchor;

        public Book.Sell.Domain.Customer Customer { get; private set; }
        public Transform BubbleAnchor => _bubbleAnchor != null ? _bubbleAnchor : transform;

        public void Initialize(Book.Sell.Domain.Customer customer)
        {
            Customer = customer;
            gameObject.name = $"CustomerVisual({customer.Id})";
        }
    }
}
