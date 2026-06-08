using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Configs
{
    /// <summary>
    /// Единая точка доступа ко всем data-driven конфигам. Потребитель не знает,
    /// откуда пришло значение (локальная папка / сервер / Firebase RC override) —
    /// это решает реализация. Конфиги считаются immutable после <see cref="WarmupAsync"/>.
    /// </summary>
    public interface IConfigsService
    {
        /// <summary>
        /// Подготавливает источник (скачивает/читает сырые файлы, активирует RC).
        /// Вызывается один раз на бутстрапе до первого Get.
        /// </summary>
        UniTask WarmupAsync(CancellationToken ct);

        /// <summary>Возвращает конфиг по id или null, если не найден (graceful fail + warning).</summary>
        T Get<T>(string id) where T : class, IConfig;

        bool TryGet<T>(string id, out T config) where T : class, IConfig;

        /// <summary>Гарантирует прогрев источника, затем возвращает конфиг.</summary>
        UniTask<T> GetAsync<T>(string id) where T : class, IConfig;

        bool IsExists<T>(string id) where T : class, IConfig;

        /// <summary>Все конфиги данного типа (например, все книги).</summary>
        IReadOnlyList<T> GetAll<T>() where T : class, IConfig;
    }
}
