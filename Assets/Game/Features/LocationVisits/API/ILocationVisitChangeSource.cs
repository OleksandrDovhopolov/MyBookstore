using System;

namespace Game.LocationVisits.API
{
    /// <summary>Signals changes to visit counts or the runtime current-location value.</summary>
    public interface ILocationVisitChangeSource
    {
        event Action Changed;
    }
}
