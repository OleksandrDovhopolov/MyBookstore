using System.Threading;
using Cysharp.Threading.Tasks;

namespace Book.Sell.Services
{
    public interface ISoldBookCommitter
    {
        void Reset();
        void CommitSoldBook(string bookId, string source);
        UniTask FlushAsync(CancellationToken ct);
    }
}
