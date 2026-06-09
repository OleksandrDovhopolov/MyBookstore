using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs;
using Game.Configs.Models;
using UnityEngine;
using VContainer.Unity;

namespace Game.Bootstrap
{
    /// <summary>
    /// Диагностический entry point: после прогрева читает book_dune и выводит результат.
    /// Цель — проверить end-to-end флоу:
    ///   сервер отдал данные → RC-override (cfg_books) применился поверх → клиент видит мёрджнутое.
    /// Использовать только для smoke-проверки; убрать регистрацию из ConfigsVContainerBindings, когда не нужен.
    /// </summary>
    public sealed class BookDuneProbeEntryPoint : IAsyncStartable
    {
        private const string LogPrefix = "[BookDuneProbe]";

        private readonly IConfigsService _configs;

        public BookDuneProbeEntryPoint(IConfigsService configs)
        {
            _configs = configs;
        }

        public async UniTask StartAsync(CancellationToken cancellation)
        {
            try
            {
                // GetAsync сам дождётся WarmupAsync, если ConfigsWarmupEntryPoint ещё не отработал.
                var dune = await _configs.GetAsync<BookConfig>("book_dune");

                if (dune == null)
                {
                    Debug.LogError($"{LogPrefix} book_dune NOT found. Проверь, что секция 'books' пришла с сервера / есть в Assets/Configs/books.json.");
                    return;
                }

                Debug.Log(
                    $"{LogPrefix} book_dune resolved: " +
                    $"id={dune.Id} | title='{dune.Title}' | author='{dune.Author}' | " +
                    $"genre='{dune.Genre}' | basePrice={dune.BasePrice} | rarityWeight={dune.RarityWeight}");

                Debug.Log(
                    $"{LogPrefix} Если RC override 'cfg_books' = {{\"book_dune\":{{\"basePrice\":999}}}} опубликован — " +
                    $"basePrice должен быть 999, остальные поля — из сервера.");
            }
            catch (System.OperationCanceledException)
            {
                // shutdown during async — ignore
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"{LogPrefix} ERROR: {ex}");
            }
        }
    }
}
