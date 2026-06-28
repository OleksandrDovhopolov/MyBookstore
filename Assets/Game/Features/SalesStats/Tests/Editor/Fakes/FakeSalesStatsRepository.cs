using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Game.SalesStats.API;

namespace Game.SalesStats.Tests.Editor.Fakes
{
    public sealed class FakeSalesStatsRepository : ISalesStatsRepository
    {
        public SalesStatsStateDto Stored { get; set; } = new();
        public int SaveCallCount { get; private set; }

        public UniTask<SalesStatsStateDto> LoadAsync(CancellationToken ct)
            => UniTask.FromResult(Clone(Stored));

        public UniTask SaveAsync(SalesStatsStateDto state, CancellationToken ct)
        {
            Stored = Clone(state);
            SaveCallCount++;
            return UniTask.CompletedTask;
        }

        private static SalesStatsStateDto Clone(SalesStatsStateDto source)
        {
            var dto = new SalesStatsStateDto();
            if (source?.SoldByGenre != null)
                dto.SoldByGenre = new Dictionary<string, int>(source.SoldByGenre, StringComparer.OrdinalIgnoreCase);
            return dto;
        }
    }
}
