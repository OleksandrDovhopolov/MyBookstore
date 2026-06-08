using System;

namespace Game.Commands {
    /// <summary>
    /// Порт репортинга ошибок (аналог багтрекера). Реализуй поверх Sentry/Crashlytics/своего стека,
    /// либо используй no-op реализацию. Команды не зависят от конкретного багтрекера.
    /// </summary>
    public interface ICommandErrorReporter {
        void Report(Exception exception, string message = null);
    }
}
