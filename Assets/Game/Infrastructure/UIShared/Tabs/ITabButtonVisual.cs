namespace UIShared
{
    /// <summary>
    /// Selected-state visual of a tab button. Implementations sit on the same GameObject as the
    /// <c>TabButton</c> and are discovered by it via <c>GetComponents</c>, so a button can combine
    /// several visuals (lift + sprite swap) without a dedicated subclass per combination.
    /// </summary>
    public interface ITabButtonVisual
    {
        void ApplySelected(bool selected);
    }
}
