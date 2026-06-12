namespace Analytics
{
    //TODO replace interface. attached to service  - Authentication — свой сервер (решение)
    public interface IPlayerIdentityProvider
    {
        string GetPlayerId();
    }
}
