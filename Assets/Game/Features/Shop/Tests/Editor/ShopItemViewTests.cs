using System.Reflection;
using Game.Shop.UI;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Newspaper.Tests.Editor
{
    public sealed class ShopItemViewTests
    {
        [Test]
        public void UpdateOfferState_DecorAvailable_ShowsPriceAndEnablesButton()
        {
            var h = Build();
            try
            {
                h.Card.Bind(Offer(isDecor: true, isAvailable: true, price: "50"), null);

                Assert.IsFalse(h.SoldRoot.activeSelf);
                Assert.IsTrue(h.PriceRoot.activeSelf);
                Assert.IsTrue(h.Button.interactable);
                Assert.AreEqual("50", h.PriceLabel.text);
            }
            finally
            {
                Object.DestroyImmediate(h.Root);
            }
        }

        [Test]
        public void UpdateOfferState_DecorSold_ShowsSoldAndDisablesButton()
        {
            var h = Build();
            try
            {
                h.Card.Bind(Offer(isDecor: true, isAvailable: true, price: "50"), null);
                h.Card.UpdateOfferState(Offer(isDecor: true, isAvailable: false, price: "50"));

                Assert.IsTrue(h.SoldRoot.activeSelf);
                Assert.IsFalse(h.PriceRoot.activeSelf);
                Assert.IsFalse(h.Button.interactable);
                Assert.AreEqual("50", h.PriceLabel.text);
            }
            finally
            {
                Object.DestroyImmediate(h.Root);
            }
        }

        [Test]
        public void UpdateOfferState_BookUnavailable_DoesNotShowSold()
        {
            var h = Build();
            try
            {
                h.Card.Bind(Offer(isDecor: false, isAvailable: true, price: "30"), null);
                h.Card.UpdateOfferState(Offer(isDecor: false, isAvailable: false, price: "30"));

                Assert.IsFalse(h.SoldRoot.activeSelf);
                Assert.IsTrue(h.PriceRoot.activeSelf);
                Assert.IsFalse(h.Button.interactable);
            }
            finally
            {
                Object.DestroyImmediate(h.Root);
            }
        }

        [Test]
        public void UpdateOfferState_DoesNotReplaceIcon()
        {
            var h = Build();
            var texture = new Texture2D(1, 1);
            var sprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), Vector2.zero);
            try
            {
                h.Card.Bind(Offer(isDecor: true, isAvailable: true, price: "50"), null, sprite);
                h.Card.UpdateOfferState(Offer(isDecor: true, isAvailable: false, price: "50"));

                Assert.AreSame(sprite, h.Icon.sprite);
            }
            finally
            {
                Object.DestroyImmediate(sprite);
                Object.DestroyImmediate(texture);
                Object.DestroyImmediate(h.Root);
            }
        }

        private static ShopOffer Offer(bool isDecor, bool isAvailable, string price) =>
            new(
                isDecor ? "decor_lot" : "book_lot",
                isDecor ? "decor_icon" : "book_box",
                "Name",
                "Description",
                price,
                isAvailable,
                isAvailable ? "NEW!" : "SOLD",
                isDecor);

        private static Harness Build()
        {
            var root = new GameObject("card");
            var card = root.AddComponent<ShopItemView>();

            var icon = new GameObject("icon").AddComponent<Image>();
            icon.transform.SetParent(root.transform);

            var priceLabel = new GameObject("priceLabel").AddComponent<TextMeshProUGUI>();
            priceLabel.transform.SetParent(root.transform);

            var soldRoot = new GameObject("sold");
            soldRoot.transform.SetParent(root.transform);

            var priceRoot = new GameObject("price");
            priceRoot.transform.SetParent(root.transform);

            var button = new GameObject("button").AddComponent<Button>();
            button.transform.SetParent(root.transform);

            SetField(card, "_icon", icon);
            SetField(card, "_priceLabel", priceLabel);
            SetField(card, "_soldRoot", soldRoot);
            SetField(card, "_priceRoot", priceRoot);
            SetField(card, "_buyButton", button);

            return new Harness(root, card, icon, priceLabel, soldRoot, priceRoot, button);
        }

        private static void SetField(object target, string name, object value)
        {
            target.GetType()
                .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(target, value);
        }

        private readonly struct Harness
        {
            public Harness(
                GameObject root,
                ShopItemView card,
                Image icon,
                TextMeshProUGUI priceLabel,
                GameObject soldRoot,
                GameObject priceRoot,
                Button button)
            {
                Root = root;
                Card = card;
                Icon = icon;
                PriceLabel = priceLabel;
                SoldRoot = soldRoot;
                PriceRoot = priceRoot;
                Button = button;
            }

            public GameObject Root { get; }
            public ShopItemView Card { get; }
            public Image Icon { get; }
            public TextMeshProUGUI PriceLabel { get; }
            public GameObject SoldRoot { get; }
            public GameObject PriceRoot { get; }
            public Button Button { get; }
        }
    }
}
