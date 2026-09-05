using System;
using System.Collections.Generic;
using System.Threading;
using Analytics;
using Cysharp.Threading.Tasks;
using Game.Characters.API;
using Game.Decor;
using Game.LocationUnlock.API;
using Game.Quest.API;
using NUnit.Framework;
using VContainer;
using SavePlayerIdentityProvider = Save.Identity.IPlayerIdentityProvider;

namespace Game.Bootstrap.Tests.Editor
{
    public sealed class AnalyticsRegistrationSmokeTests
    {
        [Test]
        public void RegisterGameAnalytics_ResolvesCompositeAnalyticsService()
        {
            var builder = new ContainerBuilder();
            builder.RegisterInstance<IQuestsService>(new FakeQuestsService());
            builder.RegisterInstance<ICharactersService>(new FakeCharactersService());
            builder.RegisterInstance<ILocationUnlockService>(new FakeLocationUnlockService());
            builder.RegisterInstance<IDecorPlacementService>(new FakeDecorPlacementService());
            builder.RegisterConsent(string.Empty);
            builder.RegisterSave();
            builder.RegisterGameAnalytics(null, null, null);

            using var container = builder.Build();

            Assert.That(container.Resolve<IAnalyticsService>(), Is.Not.Null);
            Assert.That(container.Resolve<global::Analytics.IPlayerIdentityProvider>(), Is.Not.Null);
            Assert.That(container.Resolve<SavePlayerIdentityProvider>(), Is.Not.Null);
        }

        private sealed class FakeQuestsService : IQuestsService
        {
            public IQuest TryGetQuest(string questId) => null;
            public Game.Configs.Models.QuestConfig GetQuestConfig(string questId) => null;
            public QuestState GetQuestState(string questId) => QuestState.Pending;
            public IReadOnlyList<IQuest> GetAllQuests() => Array.Empty<IQuest>();
            public IEnumerable<IQuest> GetActiveQuests() => Array.Empty<IQuest>();
            public IQuestChain GetChain(string chainId) => null;
            public IQuestChain GetChainByQuestId(string questId) => null;
            public UniTask<bool> TryActivateAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryAwardAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> TryFailAsync(string questId, CancellationToken ct) => UniTask.FromResult(false);
            public event Action<IQuest> QuestStarted { add { } remove { } }
            public event Action<IQuest> QuestCompleted { add { } remove { } }
            public event Action<IQuest> QuestAwarded { add { } remove { } }
            public event Action<IQuest> QuestFailed { add { } remove { } }
            public event Action<IQuestTask> TaskCompleted { add { } remove { } }
            public event Action<IQuestTask> TaskProgressChanged { add { } remove { } }
        }

        private sealed class FakeCharactersService : ICharactersService
        {
            public ICharacter TryGetCharacter(string characterId) => null;
            public IEnumerable<ICharacter> GetAllCharacters() => Array.Empty<ICharacter>();
            public IEnumerable<ICharacter> GetDiscoveredCharacters() => Array.Empty<ICharacter>();
            public bool IsDiscovered(string characterId) => false;
            public bool IsMemoryUnlocked(string characterId, string memoryId) => false;
            public bool TryUnlockMemory(string characterId, string memoryId) => false;
            public int UnseenMemoryCount => 0;
            public bool HasUnseenMemories => false;
            public void MarkAllMemoriesSeen() { }
            public CharacterJournalEntry GetJournalEntry(string characterId) => null;
            public event Action<ICharacter> CharacterDiscovered { add { } remove { } }
            public event Action<ICharacterMemory> MemoryUnlocked { add { } remove { } }
            public event Action UnseenMemoriesChanged { add { } remove { } }
        }

        private sealed class FakeLocationUnlockService : ILocationUnlockService
        {
            public bool IsUnlocked(string locationId) => false;
            public LocationUnlockStatus GetStatus(string locationId) => null;
            public IReadOnlyList<LocationUnlockCostProgress> GetCost(string locationId)
                => Array.Empty<LocationUnlockCostProgress>();
            public UniTask<UnlockResult> TryUnlockAsync(string locationId, CancellationToken ct)
                => UniTask.FromResult(UnlockResult.UnknownLocation);
            public UniTask<bool> ForceUnlockAsync(string locationId, CancellationToken ct) => UniTask.FromResult(false);
            public UniTask<bool> ForceLockAsync(string locationId, CancellationToken ct) => UniTask.FromResult(false);
            public event Action<string> Unlocked { add { } remove { } }
            public event Action<string> StatusChanged { add { } remove { } }
        }

        private sealed class FakeDecorPlacementService : IDecorPlacementService
        {
            public IReadOnlyList<DecorPlacementEntry> GetAllPlacements() => Array.Empty<DecorPlacementEntry>();
            public string GetDecorInSlot(string slotId) => null;
            public IReadOnlyList<string> GetActiveDecorIds() => Array.Empty<string>();
            public UniTask<DecorPlacementResult> PlaceAsync(string decorId, string slotId, CancellationToken ct)
                => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask<DecorPlacementResult> ReplaceAsync(string decorId, string slotId, CancellationToken ct)
                => UniTask.FromResult(DecorPlacementResult.Success);
            public UniTask UnplaceAsync(string slotId, CancellationToken ct) => UniTask.CompletedTask;
            public UniTask ClearAllAsync(CancellationToken ct) => UniTask.CompletedTask;
            public event Action PlacementChanged { add { } remove { } }
            public event Action<DecorPlacementChange> PlacementActionPerformed { add { } remove { } }
        }
    }
}
