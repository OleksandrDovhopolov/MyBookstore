using Book.Sell.API;

namespace Game.Shop.Tests.Editor.Fakes
{
    internal sealed class FakeCurrentDayProvider : ICurrentDayProvider
    {
        public int CurrentDay { get; set; } = 1;
        public bool IsCurrentDayCompleted { get; set; }
    }
}
