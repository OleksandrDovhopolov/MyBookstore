using Game.Ftue.Services;
using VContainer;

namespace Game.Bootstrap
{
    // Registers the day-1 entry mode (Hub vs Location) as a global settings instance so the
    // GameplayScene bootstrap can branch on it. Mirror of RegisterFtue(WelcomeWindowStartupSettings).
    public static class FirstDayEntryVContainerBindings
    {
        public static void RegisterFirstDayEntry(this IContainerBuilder builder, FirstDayEntryMode mode)
        {
            builder.RegisterInstance(new FirstDayEntrySettings(mode));
        }
    }
}
