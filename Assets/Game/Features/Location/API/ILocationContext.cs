using System.Collections.Generic;
using UnityEngine;

namespace Game.Location.API
{
    public interface ILocationContext
    {
        bool IsBound { get; }
        string LocationId { get; }

        Transform CustomerSpawnRoot { get; }
        Transform EntryLeft { get; }
        Transform EntryRight { get; }
        Transform ExitLeft { get; }
        Transform ExitRight { get; }
        Transform ShopApproach { get; }
        IReadOnlyList<Transform> LaneAnchors { get; }
    }
}
