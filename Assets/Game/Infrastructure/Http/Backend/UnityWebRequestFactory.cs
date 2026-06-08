namespace Game.Http {
    public class UnityWebRequestFactory : IRequestFactory {
        public IRequest CreateRequest(IRequestParams requestParams) {
            return new UnityWebRequestAdapter(requestParams);
        }
    }
}
