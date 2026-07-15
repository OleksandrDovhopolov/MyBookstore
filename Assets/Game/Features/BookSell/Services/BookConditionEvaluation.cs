namespace Book.Sell.Services
{
    public readonly struct BookConditionEvaluation
    {
        public BookConditionEvaluation(bool isMatch, string reason)
        {
            IsMatch = isMatch;
            Reason = reason;
        }

        public bool IsMatch { get; }
        public string Reason { get; }
    }
}
