using System;
using Book.Sell.Domain;
using Game.Configs;
using Game.Configs.Models;

namespace Book.Sell.Services
{
    public interface ICustomerVisualSpriteResolver
    {
        string ResolveFigureSpriteKey(Customer customer);
        string ResolveAvatarSpriteKey(Customer customer);
    }

    public sealed class CustomerVisualSpriteResolver : ICustomerVisualSpriteResolver
    {
        private readonly IConfigsService _configs;

        public CustomerVisualSpriteResolver(IConfigsService configs)
            => _configs = configs ?? throw new ArgumentNullException(nameof(configs));

        public string ResolveFigureSpriteKey(Customer customer)
        {
            if (customer == null)
                return null;

            var characterKey = ResolveCharacterPortraitKey(customer.CharacterId);
            if (!string.IsNullOrWhiteSpace(characterKey))
                return characterKey;

            return ResolveNpcVisual(customer.NpcVisualId)?.FigureSpriteKey;
        }

        public string ResolveAvatarSpriteKey(Customer customer)
        {
            if (customer == null)
                return null;

            var characterKey = ResolveCharacterPortraitKey(customer.CharacterId);
            if (!string.IsNullOrWhiteSpace(characterKey))
                return characterKey + "_avatar";

            return ResolveNpcVisual(customer.NpcVisualId)?.AvatarSpriteKey;
        }

        private string ResolveCharacterPortraitKey(string characterId)
        {
            if (string.IsNullOrWhiteSpace(characterId))
                return null;

            if (_configs.TryGet<CharacterConfig>(characterId, out var character)
                && !string.IsNullOrWhiteSpace(character?.PortraitKey))
            {
                return character.PortraitKey;
            }

            return characterId;
        }

        private CustomerVisualConfig ResolveNpcVisual(string npcVisualId)
        {
            if (string.IsNullOrWhiteSpace(npcVisualId))
                return null;

            return _configs.TryGet<CustomerVisualConfig>(npcVisualId, out var visual)
                ? visual
                : null;
        }
    }
}
