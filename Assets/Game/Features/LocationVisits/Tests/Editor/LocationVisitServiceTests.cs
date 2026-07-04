using Game.LocationVisits.Services;
using NUnit.Framework;

namespace Game.LocationVisits.Tests.Editor
{
    public sealed class LocationVisitServiceTests
    {
        private static (LocationVisitService svc, FakeSaveService save) Build()
        {
            var save = new FakeSaveService();
            var svc = new LocationVisitService(save, new SaveBackedLocationVisitsRepository(save));
            svc.AfterLoadAsync(default).GetAwaiter().GetResult();
            return (svc, save);
        }

        [Test]
        public void RegistersSaveHookInCtor()
        {
            var (svc, save) = Build();
            Assert.Contains(svc, save.RegisteredHooks);
        }

        [Test]
        public void RecordVisit_Increments_PerLocation()
        {
            var (svc, _) = Build();
            Assert.AreEqual(0, svc.GetVisits("far_beach"));

            svc.RecordVisit("far_beach");
            svc.RecordVisit("far_beach");

            Assert.AreEqual(2, svc.GetVisits("far_beach"));
            Assert.AreEqual(0, svc.GetVisits("loc_downtown"));
        }

        [Test]
        public void RecordVisit_SetsCurrent_ClearResets()
        {
            var (svc, _) = Build();
            Assert.IsNull(svc.CurrentLocationId);

            svc.RecordVisit("far_beach");
            Assert.AreEqual("far_beach", svc.CurrentLocationId);

            svc.ClearCurrentLocation();
            Assert.IsNull(svc.CurrentLocationId);
        }

        [Test]
        public void RecordVisit_MarksSaveDirty()
        {
            var (svc, save) = Build();
            svc.RecordVisit("far_beach");
            Assert.GreaterOrEqual(save.MarkDirtyCount, 1);
        }

        [Test]
        public void SaveLoad_Roundtrip_PreservesCounts_CurrentIsRuntimeOnly()
        {
            var (svc, save) = Build();
            svc.RecordVisit("far_beach");
            svc.RecordVisit("far_beach");
            svc.RecordVisit("loc_downtown");
            svc.BeforeSaveAsync(default).GetAwaiter().GetResult();

            // Fresh instance over the same store simulates a relaunch.
            var reloaded = new LocationVisitService(save, new SaveBackedLocationVisitsRepository(save));
            reloaded.AfterLoadAsync(default).GetAwaiter().GetResult();

            Assert.AreEqual(2, reloaded.GetVisits("far_beach"));
            Assert.AreEqual(1, reloaded.GetVisits("loc_downtown"));
            Assert.IsNull(reloaded.CurrentLocationId); // current location is not persisted
        }
    }
}
