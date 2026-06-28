using System.Threading;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Services
{
    public interface ISalesGoldCollector
    {
        void Reset();
        void CollectSaleGold(int day, string bookId, int amount, string source);
        UniTask FlushAsync(CancellationToken ct);
    }
}
