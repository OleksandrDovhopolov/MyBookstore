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

            if (source?.SoldByLocationGenre != null)
            {
                dto.SoldByLocationGenre = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);
                foreach (var location in source.SoldByLocationGenre)
                    dto.SoldByLocationGenre[location.Key] = location.Value == null
                        ? null
                        : new Dictionary<string, int>(location.Value, StringComparer.OrdinalIgnoreCase);
            }

            if (source?.SoldByDayGenre != null)
            {
                dto.SoldByDayGenre = new Dictionary<int, Dictionary<string, int>>();
                foreach (var day in source.SoldByDayGenre)
                    dto.SoldByDayGenre[day.Key] = day.Value == null
                        ? null
                        : new Dictionary<string, int>(day.Value, StringComparer.OrdinalIgnoreCase);
            }

            return dto;
        }
    }
}
