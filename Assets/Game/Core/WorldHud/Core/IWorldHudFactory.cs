using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.WorldHud
{
    public interface IWorldHudFactory
    {
        UniTask<T> CreateAsync<T>(CancellationToken ct) where T : WorldHud;
        void Destroy(WorldHud hud);
    }
}
