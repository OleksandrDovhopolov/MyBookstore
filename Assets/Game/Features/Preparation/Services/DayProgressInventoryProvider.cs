using System;
using System.Collections.Generic;
using Game.Configs;
using Game.Configs.Models;
using Game.DayCycle.Day;
using UnityEngine;

namespace Game.Preparation.Services
{
    /// <summary>
    /// Real player inventory: returns only books whose ids are stored in
    /// <c>DayProgressState.OwnedBookIds</c>. The starter set is seeded by
    /// <c>FtueBootstrapper</c> on first launch.
    /// Fallback: if OwnedBookIds is empty (FTUE skipped, dev scenario, manual wipe),
    /// returns the full catalog with a warning — so Preparation doesn't stall on an empty list.
    /// </summary>
    public sealed class DayProgressInventoryProvider : IPreparationInventoryProvider
    {
        private const string LogPrefix = "[Preparation.Inventory]";

        private readonly IDayProgressService _dayProgress;
        private readonly IConfigsService _configs;

        public DayProgressInventoryProvider(IDayProgressService dayProgress, IConfigsService configs)
        {
            _dayProgress = dayProgress ?? throw new ArgumentNullException(nameof(dayProgress));
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public IReadOnlyList<BookConfig> GetOwnedBooks()
        {
            var ownedIds = _dayProgress.Current?.OwnedBookIds;
            if (ownedIds == null || ownedIds.Count == 0)
            {
                Debug.LogWarning($"{LogPrefix} OwnedBookIds is empty — falling back to the full catalog.");
                return _configs.GetAll<BookConfig>();
            }

            var result = new List<BookConfig>(ownedIds.Count);
            for (int i = 0; i < ownedIds.Count; i++)
            {
                if (_configs.TryGet<BookConfig>(ownedIds[i], out var book))
                    result.Add(book);
                else
                    Debug.LogWarning($"{LogPrefix} BookConfig '{ownedIds[i]}' not found in catalog — skipping.");
            }
            return result;
        }
    }
}
