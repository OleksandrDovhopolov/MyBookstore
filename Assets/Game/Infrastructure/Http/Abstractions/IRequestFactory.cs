namespace Game.Http {
    public interface IRequestFactory {
        IRequest CreateRequest(IRequestParams requestParams);
    }
}
