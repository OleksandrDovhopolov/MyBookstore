using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.Configs
{
    /// <summary>
    /// Поставщик сырого JSON-текста конфиг-файлов. Абстрагирует место хранения:
    /// локальная папка (Editor), .NET сервер + disk snapshot (build).
    /// После <see cref="WarmupAsync"/> весь сырой текст доступен синхронно через
    /// <see cref="GetRaw"/> — десериализация ленивая и делается уже в ConfigsService.
    /// </summary>
    public interface IConfigSource
    {
        /// <summary>Загружает/обновляет все доступные конфиг-файлы в память.</summary>
        UniTask WarmupAsync(CancellationToken ct);

        /// <summary>
        /// Сырой JSON файла по имени (без расширения), или null если файла нет.
        /// Валиден только после успешного WarmupAsync.
        /// </summary>
        string GetRaw(string fileName);
    }
}
