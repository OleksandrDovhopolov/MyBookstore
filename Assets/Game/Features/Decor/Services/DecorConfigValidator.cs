using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;
using VContainer.Unity;

namespace Game.Decor.Services
{
    /// <summary>
    /// Validates DecorConfig and LocationConfig.DecorSlots at boot. Fires as an IStartable entry
    /// point. In Editor, errors throw to block Play mode; in runtime builds, errors are logged
    /// and the affected entries get effectively ignored downstream.
    /// See docs/INPROGRESS/Decor.md §11.5 for the rule list.
    /// </summary>
    public sealed class DecorConfigValidator : IStartable
    {
        private const string LogTag = "[DecorValidator]";

        private readonly IConfigsService _configs;

        public DecorConfigValidator(IConfigsService configs)
        {
            _configs = configs ?? throw new ArgumentNullException(nameof(configs));
        }

        public void Start()
        {
            //TODO check this bug.
            /*
             * 
            var decors = _configs.GetAll<DecorConfig>(); before  public async UniTask WarmupAsync(CancellationToken ct)
            and data is not loaded after
             */
            DelayedStart().Forget();
            return;
            var report = Validate();

            for (var i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning($"{LogTag} {report.Warnings[i]}");

            if (!report.HasErrors) return;

            for (var i = 0; i < report.Errors.Count; i++)
                Debug.LogError($"{LogTag} {report.Errors[i]}");

#if UNITY_EDITOR
            throw new InvalidOperationException(
                $"{LogTag} {report.Errors.Count} decor config error(s). See console.\n{report.FormatErrors()}");
#endif
        }

        private async UniTask DelayedStart()
        {
            await UniTask.WaitForSeconds(1f);
            
            var report = Validate();

            for (var i = 0; i < report.Warnings.Count; i++)
                Debug.LogWarning($"{LogTag} {report.Warnings[i]}");

            if (!report.HasErrors) return;

            for (var i = 0; i < report.Errors.Count; i++)
                Debug.LogError($"{LogTag} {report.Errors[i]}");

#if UNITY_EDITOR
            throw new InvalidOperationException(
                $"{LogTag} {report.Errors.Count} decor config error(s). See console.\n{report.FormatErrors()}");
#endif
        }
        
        public ValidationReport Validate()
        {
            var report = new ValidationReport();
            ValidateDecors(report);
            ValidateLocations(report);
            return report;
        }

        private void ValidateDecors(ValidationReport report)
        {
            var decors = _configs.GetAll<DecorConfig>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var knownGenres = CollectKnownGenres();

            for (var i = 0; i < decors.Count; i++)
            {
                var decor = decors[i];

                if (string.IsNullOrEmpty(decor.Id))
                {
                    report.Errors.Add($"DecorConfig at index {i} has empty Id.");
                    continue;
                }

                if (!ids.Add(decor.Id))
                {
                    report.Errors.Add($"Duplicate DecorConfig Id '{decor.Id}'.");
                }

                if (string.IsNullOrEmpty(decor.DisplayName))
                    report.Warnings.Add($"Decor '{decor.Id}' has empty DisplayName.");

                if (!Enum.IsDefined(typeof(DecorPositionType), decor.PositionType))
                    report.Errors.Add($"Decor '{decor.Id}' has invalid PositionType ({(int)decor.PositionType}).");

                if (!Enum.IsDefined(typeof(DecorSize), decor.Size))
                    report.Errors.Add($"Decor '{decor.Id}' has invalid Size ({(int)decor.Size}).");

                if (!Enum.IsDefined(typeof(DecorRarity), decor.Rarity))
                    report.Warnings.Add($"Decor '{decor.Id}' has invalid Rarity, will default to Common.");

                if (decor.BasePrice < 0)
                    report.Warnings.Add($"Decor '{decor.Id}' has negative BasePrice ({decor.BasePrice}).");

                if (decor.GenreMultipliers != null)
                {
                    for (var j = 0; j < decor.GenreMultipliers.Length; j++)
                    {
                        var mod = decor.GenreMultipliers[j];
                        if (mod == null)
                        {
                            report.Errors.Add($"Decor '{decor.Id}' has null entry at GenreMultipliers[{j}].");
                            continue;
                        }
                        if (mod.Multiplier <= 0f)
                        {
                            report.Errors.Add($"Decor '{decor.Id}' GenreMultipliers[{j}] has non-positive Multiplier ({mod.Multiplier}) — entry ignored.");
                            continue;
                        }
                        if (string.IsNullOrEmpty(mod.Genre))
                        {
                            report.Errors.Add($"Decor '{decor.Id}' GenreMultipliers[{j}] has empty Genre.");
                            continue;
                        }
                        if (knownGenres.Count > 0 && !knownGenres.Contains(mod.Genre))
                        {
                            report.Warnings.Add($"Decor '{decor.Id}' references unknown genre '{mod.Genre}' (not present in any BookConfig).");
                        }
                    }
                }
            }
        }

        private void ValidateLocations(ValidationReport report)
        {
            var locations = _configs.GetAll<LocationConfig>();
            for (var i = 0; i < locations.Count; i++)
            {
                var location = locations[i];
                if (location?.DecorSlots == null) continue;

                var slotIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (var j = 0; j < location.DecorSlots.Length; j++)
                {
                    var slot = location.DecorSlots[j];
                    if (slot == null)
                    {
                        report.Errors.Add($"Location '{location.Id}' DecorSlots[{j}] is null.");
                        continue;
                    }

                    if (string.IsNullOrEmpty(slot.Id))
                    {
                        report.Errors.Add($"Location '{location.Id}' DecorSlots[{j}] has empty Id.");
                        continue;
                    }

                    if (!slotIds.Add(slot.Id))
                        report.Errors.Add($"Location '{location.Id}' has duplicate slot Id '{slot.Id}'.");

                    if (!Enum.IsDefined(typeof(DecorPositionType), slot.PositionType))
                        report.Errors.Add($"Location '{location.Id}' slot '{slot.Id}' has invalid PositionType.");

                    if (!Enum.IsDefined(typeof(DecorSize), slot.MaxSize))
                        report.Errors.Add($"Location '{location.Id}' slot '{slot.Id}' has invalid MaxSize.");
                }
            }
        }

        private HashSet<string> CollectKnownGenres()
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var books = _configs.GetAll<BookConfig>();
            for (var i = 0; i < books.Count; i++)
            {
                if (!string.IsNullOrEmpty(books[i].Genre))
                    set.Add(books[i].Genre);
            }
            return set;
        }
    }
}
