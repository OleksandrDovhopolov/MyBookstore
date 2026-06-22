using System.Threading;
using Game.DayCycle.Day;
using Game.DayCycle.Tests.Editor.Fakes;
using NUnit.Framework;

namespace Game.DayCycle.Tests.Editor
{
    public sealed class DayProgressServiceTests
    {
        [Test]
        public void MarkCurrentDayCompleted_WhenAlreadyCompletedResults_DoesNotEmitPhaseChanged()
        {
            var save = new FakeSaveService();
            var progress = new DayProgressService(save);
            progress.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
            progress.Current.CompletedDays.Add(progress.Current.CurrentDay);
            progress.Current.CurrentPhase = DayPhase.Results;
            progress.SaveAsync(CancellationToken.None).GetAwaiter().GetResult();

            var phaseChangedCount = 0;
            progress.PhaseChanged += _ => phaseChangedCount++;

            progress.MarkCurrentDayCompletedAsync(CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(0, phaseChangedCount);
        }
    }
}
