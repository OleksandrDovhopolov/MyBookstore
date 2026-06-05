using System;

namespace Game.Commands {
    /// <summary>
    /// Заглушка репортера ошибок: ничего не отправляет наружу.
    /// Подмени на интеграцию с Sentry/Crashlytics/своим багтрекером.
    /// </summary>
    public class NoOpCommandErrorReporter : ICommandErrorReporter {
        public void Report(Exception exception, string message = null) {
            // намеренно пусто
        }
    }
}
