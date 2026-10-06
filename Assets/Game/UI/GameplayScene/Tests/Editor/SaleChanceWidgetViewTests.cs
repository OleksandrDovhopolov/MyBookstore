using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using Game.Localization;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace GameplayUI.Tests.Editor
{
    public sealed class SaleChanceWidgetViewTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            LocalizationLocator.SetService(null);

            if (_root != null)
                Object.DestroyImmediate(_root);
        }

        [Test]
        public void Setup_FormatsPercentThroughLocalization()
        {
            LocalizationLocator.SetService(new FakeLocalization(
                ("ui.gameplay.sale_chance", "Sale chance: {0}%")));

            var view = CreateView(out var label);

            var ok = view.Setup(new SaleChanceWidgetData(BookGenre.Fantasy, 40, null));

            Assert.IsTrue(ok);
            Assert.AreEqual("Sale chance: 40%", label.text);
        }

        private SaleChanceWidgetView CreateView(out TMP_Text label)
        {
            _root = new GameObject("SaleChanceWidget", typeof(RectTransform));
            var view = _root.AddComponent<SaleChanceWidgetView>();

            var labelGo = new GameObject("PercentLabel", typeof(RectTransform));
            labelGo.transform.SetParent(_root.transform, false);
            label = labelGo.AddComponent<TextMeshProUGUI>();

            SetPrivateField(view, "_percentLabel", label);
            return view;
        }

        private static void SetPrivateField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private sealed class FakeLocalization : ILocalizationService
        {
            private readonly Dictionary<string, string> _texts = new(StringComparer.Ordinal);

            public FakeLocalization(params (string key, string value)[] texts)
            {
                for (var i = 0; i < texts.Length; i++)
                    _texts[texts[i].key] = texts[i].value;
            }

            public string CurrentLocale => "en";
            public event Action<string> LocaleChanged;

            public UniTask WarmupAsync(System.Threading.CancellationToken ct) => UniTask.CompletedTask;

            public string Get(string key)
                => _texts.TryGetValue(key, out var value) ? value : key;

            public string Get(string key, params object[] args)
                => string.Format(CultureInfo.InvariantCulture, Get(key), args);

            public bool TryGet(string key, out string value) => _texts.TryGetValue(key, out value);

            public void SetLocale(string locale) => LocaleChanged?.Invoke(locale);
        }
    }
}
