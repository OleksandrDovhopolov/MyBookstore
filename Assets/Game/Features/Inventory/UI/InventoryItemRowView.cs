using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;
using SpriteService;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Inventory.UI
{
    public sealed class InventoryItemRowView : MonoBehaviour
    {
        [SerializeField] private Image _image;
        [SerializeField] private TextMeshProUGUI _amountText;

        public void Bind(BookGenre genre, int count, IUiSpriteProvider sprites, CancellationToken ct)
        {
            if (_amountText != null)
                _amountText.text = count.ToString();

            SetIcon(null);

            var genreId = genre.ToConfigValue();
            if (sprites == null || string.IsNullOrEmpty(genreId)) return;
            LoadIconAsync(genreId, sprites, ct).Forget();
        }

        private async UniTaskVoid LoadIconAsync(string genreId, IUiSpriteProvider sprites, CancellationToken ct)
        {
            try
            {
                var sprite = await sprites.GetSpriteAsync(genreId, ct);
                if (ct.IsCancellationRequested) return;
                SetIcon(sprite);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[InventoryItemRowView] Failed to load sprite '{genreId}': {e.Message}");
            }
        }

        private void SetIcon(Sprite sprite)
        {
            if (_image == null) return;
            _image.sprite = sprite;
            _image.enabled = sprite != null;
        }
    }
}
