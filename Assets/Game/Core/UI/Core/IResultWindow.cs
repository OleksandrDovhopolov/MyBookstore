namespace Game.UI
{
    // Marker interface for windows that produce a typed result while open.
    // Combined with WaitForCloseAsync<TResult> extension to await the result.
    public interface IResultWindow<out TResult>
    {
        TResult Result { get; }
    }
}
