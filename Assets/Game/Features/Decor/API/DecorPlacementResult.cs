namespace Game.Decor
{
    public enum DecorPlacementResult
    {
        Success = 0,
        DecorNotInInventory = 1,
        DecorConfigMissing = 2,
        SlotNotFound = 3,
        PositionTypeMismatch = 4,
        SizeMismatch = 5,
        SlotOccupied = 6,
        AlreadyPlaced = 7,
    }
}
