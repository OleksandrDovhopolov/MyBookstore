using System.Threading;
using Cysharp.Threading.Tasks;
using Game.Configs.Models;

namespace Game.Preparation.Services
{
    public interface ISaleChancePreviewService
    {
        UniTask<int> GetPercentAsync(BookGenre genre, CancellationToken ct);
    }
}
