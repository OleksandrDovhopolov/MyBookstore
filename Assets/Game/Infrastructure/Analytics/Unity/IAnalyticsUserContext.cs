namespace Analytics
{
    public interface IAnalyticsUserContext
    {
        string UserId { get; }

        void SetUserId(string userId);
    }
}
