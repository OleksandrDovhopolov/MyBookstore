using System.Threading;
using Game.Characters.Services.Persistence;
using Game.Characters.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.Characters.Tests.Editor
{
    public sealed class SaveBackedCharactersRepositoryTests
    {
        [Test]
        public void Discovered_SurvivesSaveLoadRoundTrip()
        {
            var save = new FakeSaveService();
            var repo = new SaveBackedCharactersRepository(save);

            var state = new SavedCharacters();
            state.Characters["harper"] = new SavedCharacter { Discovered = true };
            repo.SaveAsync(state, CancellationToken.None).GetAwaiter().GetResult();

            var loaded = repo.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsTrue(loaded.Characters.ContainsKey("harper"));
            Assert.IsTrue(loaded.Characters["harper"].Discovered);
        }

        [Test]
        public void Load_MissingModule_ReturnsEmptyNotNull()
        {
            var repo = new SaveBackedCharactersRepository(new FakeSaveService());

            var loaded = repo.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.IsNotNull(loaded);
            Assert.IsNotNull(loaded.Characters);
            CollectionAssert.IsEmpty(loaded.Characters);
        }
    }
}
