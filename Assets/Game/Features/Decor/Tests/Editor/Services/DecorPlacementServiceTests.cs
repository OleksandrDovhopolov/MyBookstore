using System.Threading;
using Game.Configs.Models;
using Game.Decor.Services;
using Game.Decor.Tests.Editor.Fakes;
using Game.Inventory.API;
using NUnit.Framework;

namespace Game.Decor.Tests.Editor.Services
{
    public sealed class DecorPlacementServiceTests
    {
        private FakeConfigsService _configs;
        private FakeInventoryService _inventory;
        private FakeSaveService _save;
        private DecorPlacementService _service;

        [SetUp]
        public void SetUp()
        {
            _configs = new FakeConfigsService();
            _configs.SetAll(new[]
            {
                new BookShopConfig
                {
                    Id = DecorPlacementService.HardcodedBookShopId,
                    DecorSlots = new[]
                    {
                        new DecorSlot { Id = "s_stand_small", PositionType = DecorPositionType.Standing, MaxSize = DecorSize.Small },
                        new DecorSlot { Id = "s_stand_med",   PositionType = DecorPositionType.Standing, MaxSize = DecorSize.Medium },
                        new DecorSlot { Id = "s_wall_large",  PositionType = DecorPositionType.Wall,     MaxSize = DecorSize.Large },
                    }
                }
            });
            _configs.SetAll(new[]
            {
                new DecorConfig { Id = "globe",     PositionType = DecorPositionType.Standing, Size = DecorSize.Small },
                new DecorConfig { Id = "coffeepot", PositionType = DecorPositionType.Standing, Size = DecorSize.Small },
                new DecorConfig { Id = "painting",  PositionType = DecorPositionType.Wall,     Size = DecorSize.Medium },
                new DecorConfig { Id = "bigtable",  PositionType = DecorPositionType.Standing, Size = DecorSize.Large },
            });
            _inventory = new FakeInventoryService();
            _save = new FakeSaveService();
            var storage = new SaveBackedDecorPlacementStorage(_save);
            _service = new DecorPlacementService(storage, _inventory, _configs, _save);
        }

        [Test]
        public void Place_DecorNotInInventory_Fails()
        {
            var result = _service.PlaceAsync("globe", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.DecorNotInInventory, result);
        }

        [Test]
        public void Place_Success_StoresEntry()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            var result = _service.PlaceAsync("globe", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.Success, result);
            Assert.AreEqual("globe", _service.GetDecorInSlot("s_stand_small"));
            CollectionAssert.AreEqual(new[] { "globe" }, _service.GetActiveDecorIds());
        }

        [Test]
        public void Place_PositionTypeMismatch_Fails()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            var result = _service.PlaceAsync("globe", "s_wall_large", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.PositionTypeMismatch, result);
        }

        [Test]
        public void Place_SizeMismatch_Fails()
        {
            _inventory.Seed("bigtable", InventoryCategories.Decor);
            var result = _service.PlaceAsync("bigtable", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.SizeMismatch, result);
        }

        [Test]
        public void Place_SlotNotFound_Fails()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            var result = _service.PlaceAsync("globe", "non_existent", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.SlotNotFound, result);
        }

        [Test]
        public void Place_SlotOccupied_Fails()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            _inventory.Seed("coffeepot", InventoryCategories.Decor);
            _service.PlaceAsync("globe", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            var result = _service.PlaceAsync("coffeepot", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(DecorPlacementResult.SlotOccupied, result);
        }

        [Test]
        public void Unplace_RemovesEntry()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            _service.PlaceAsync("globe", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            _service.UnplaceAsync("s_stand_small", CancellationToken.None).GetAwaiter().GetResult();
            Assert.IsNull(_service.GetDecorInSlot("s_stand_small"));
        }

        [Test]
        public void SaveRoundTrip_Persists()
        {
            _inventory.Seed("globe", InventoryCategories.Decor);
            _service.PlaceAsync("globe", "s_stand_small", CancellationToken.None).GetAwaiter().GetResult();

            var storage = new SaveBackedDecorPlacementStorage(_save);
            var loaded = storage.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(1, loaded.Placements.Count);
            Assert.AreEqual("globe", loaded.Placements[0].DecorId);
            Assert.AreEqual("s_stand_small", loaded.Placements[0].SlotId);
        }

        [Test]
        public void AfterLoad_OrphanedDecorId_IsRemoved()
        {
            _save.Store[DecorSaveKeys.Placement] = Newtonsoft.Json.JsonConvert.SerializeObject(new DecorPlacementState
            {
                Placements = new System.Collections.Generic.List<DecorPlacementEntry>
                {
                    new() { SlotId = "s_stand_small", DecorId = "ghost_decor" }
                }
            });

            _service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, _service.GetAllPlacements().Count, "orphaned placement should be dropped");
        }

        [Test]
        public void AfterLoad_OrphanedSlotId_IsRemoved()
        {
            _save.Store[DecorSaveKeys.Placement] = Newtonsoft.Json.JsonConvert.SerializeObject(new DecorPlacementState
            {
                Placements = new System.Collections.Generic.List<DecorPlacementEntry>
                {
                    new() { SlotId = "deleted_slot", DecorId = "globe" }
                }
            });

            _service.AfterLoadAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, _service.GetAllPlacements().Count);
        }
    }
}
