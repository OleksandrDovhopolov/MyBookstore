using System.Threading;
using Cysharp.Threading.Tasks;

namespace Game.UI.ContentWidget
{
    public interface IContentWidgetView
    {
        bool Setup(ContentWidgetDataBase data);

        UniTask OnViewCreatedAsync(CancellationToken ct);
    }
}
