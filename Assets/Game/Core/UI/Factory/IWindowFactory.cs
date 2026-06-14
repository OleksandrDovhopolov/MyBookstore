using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Game.UI
{
    public interface IWindowFactory
    {
        UniTask<T> CreateAsync<T>(Transform parent, CancellationToken ct)
            where T : class, IWindowController, new();

        void Destroy(IWindowController controller);
    }
}
