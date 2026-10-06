using System.Collections.Generic;
using System.Threading;
using Game.Attention.API;
using NUnit.Framework;

namespace Game.Attention.Tests.Editor
{
    [TestFixture]
    internal sealed class AttentionServiceTests
    {
        private const string IdKey = "test.ids";
        private const string FlagKey = "test.flag";

        [Test]
        public void Constructor_RegistersSaveHook()
        {
            var save = new FakeSaveService();
            _ = Build(save, new FakeAttentionRepository());

            Assert.That(save.RegisteredHooks, Has.Count.EqualTo(1));
        }

        [Test]
        public void Constructor_WithNoSources_DoesNotThrow()
        {
            var save = new FakeSaveService();
            Assert.DoesNotThrow(() => new AttentionService(save, new FakeAttentionRepository()));
        }

        [Test]
        public void HasUnseen_UnknownKey_IsFalse()
        {
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), new FakeAttentionSource(IdKey, "a"));
            Load(service);

            Assert.That(service.HasUnseen("nope"), Is.False);
            Assert.That(service.HasUnseen(null), Is.False);
        }

        [Test]
        public void HasUnseen_IdNotInSeenSet_IsTrue()
        {
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), new FakeAttentionSource(IdKey, "a"));
            Load(service);

            Assert.That(service.HasUnseen(IdKey), Is.True);
        }

        [Test]
        public void HasUnseen_AllIdsSeen_IsFalse()
        {
            var repository = new FakeAttentionRepository
            {
                Loaded = new SavedAttention
                {
                    SeenIdsByKey = new Dictionary<string, HashSet<string>> { [IdKey] = new() { "a" } }
                }
            };
            var service = Build(new FakeSaveService(), repository, new FakeAttentionSource(IdKey, "a"));
            Load(service);

            Assert.That(service.HasUnseen(IdKey), Is.False);
        }

        [Test]
        public void MarkSeen_ClearsUnseenAndMarksDirty()
        {
            var save = new FakeSaveService();
            var service = Build(save, new FakeAttentionRepository(), new FakeAttentionSource(IdKey, "a", "b"));
            Load(service);

            service.MarkSeen(IdKey);

            Assert.That(service.HasUnseen(IdKey), Is.False);
            Assert.That(save.MarkDirtyCount, Is.EqualTo(1));
        }

        [Test]
        public void MarkSeen_WhenNothingNew_DoesNotMarkDirty()
        {
            var save = new FakeSaveService();
            var service = Build(save, new FakeAttentionRepository(), new FakeAttentionSource(IdKey, "a"));
            Load(service);
            service.MarkSeen(IdKey);
            var dirtyAfterFirst = save.MarkDirtyCount;

            service.MarkSeen(IdKey);

            Assert.That(save.MarkDirtyCount, Is.EqualTo(dirtyAfterFirst));
        }

        [Test]
        public void BeforeSave_WhenClean_DoesNotWrite()
        {
            var repository = new FakeAttentionRepository();
            var service = Build(new FakeSaveService(), repository, new FakeAttentionSource(IdKey, "a"));
            Load(service);

            service.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void BeforeSave_AfterMarkSeen_WritesOnceThenGoesClean()
        {
            var repository = new FakeAttentionRepository();
            var service = Build(new FakeSaveService(), repository, new FakeAttentionSource(IdKey, "a"));
            Load(service);
            service.MarkSeen(IdKey);

            service.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            service.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.That(repository.SaveCount, Is.EqualTo(1));
            Assert.That(repository.LastSaved.SeenIdsByKey[IdKey], Does.Contain("a"));
        }

        [Test]
        public void FlagSource_MarkSeen_DelegatesAndNeverDirtiesThisModule()
        {
            var save = new FakeSaveService();
            var repository = new FakeAttentionRepository();
            var flag = new FakeAttentionFlagSource(FlagKey, hasUnseen: true);
            var service = Build(save, repository, flagSources: new IAttentionFlagSource[] { flag });
            Load(service);

            Assert.That(service.HasUnseen(FlagKey), Is.True);

            service.MarkSeen(FlagKey);

            Assert.That(flag.MarkSeenCount, Is.EqualTo(1));
            Assert.That(service.HasUnseen(FlagKey), Is.False);
            Assert.That(save.MarkDirtyCount, Is.Zero, "flag sources persist elsewhere");

            service.BeforeSaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            Assert.That(repository.SaveCount, Is.Zero);
        }

        [Test]
        public void Changed_FiresOnlyWhenABooleanFlips()
        {
            var source = new FakeAttentionSource(IdKey);
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), source);
            Load(service);

            var raised = 0;
            service.Changed += () => raised++;

            source.RaiseChanged();
            Assert.That(raised, Is.Zero, "no flip — nothing new");

            source.SetIds("a");
            Assert.That(raised, Is.EqualTo(1), "false -> true");

            source.SetIds("a", "b");
            Assert.That(raised, Is.EqualTo(1), "still true — no flip");

            service.MarkSeen(IdKey);
            Assert.That(raised, Is.EqualTo(2), "true -> false");
        }

        [Test]
        public void Changed_DoesNotFireBeforeLoad()
        {
            var source = new FakeAttentionSource(IdKey);
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), source);

            var raised = 0;
            service.Changed += () => raised++;
            source.SetIds("a");

            Assert.That(raised, Is.Zero);
        }

        [Test]
        public void HasAnyUnseen_IsTrueWhenAnyKeyIsUnseen()
        {
            var clean = new FakeAttentionSource("a.key");
            var dirty = new FakeAttentionSource("b.key", "x");
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), clean, dirty);
            Load(service);

            Assert.That(service.HasAnyUnseen(new[] { "a.key" }), Is.False);
            Assert.That(service.HasAnyUnseen(new[] { "a.key", "b.key" }), Is.True);
            Assert.That(service.HasAnyUnseen(null), Is.False);
        }

        [Test]
        public void Dispose_StopsNotifications()
        {
            var source = new FakeAttentionSource(IdKey);
            var service = Build(new FakeSaveService(), new FakeAttentionRepository(), source);
            Load(service);

            var raised = 0;
            service.Changed += () => raised++;
            service.Dispose();
            source.SetIds("a");

            Assert.That(raised, Is.Zero);
        }

        private static AttentionService Build(
            FakeSaveService save,
            FakeAttentionRepository repository,
            params IAttentionSource[] sources)
            => new(save, repository, sources);

        private static AttentionService Build(
            FakeSaveService save,
            FakeAttentionRepository repository,
            IReadOnlyList<IAttentionFlagSource> flagSources)
            => new(save, repository, null, flagSources);

        private static void Load(AttentionService service)
            => service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();
    }
}
