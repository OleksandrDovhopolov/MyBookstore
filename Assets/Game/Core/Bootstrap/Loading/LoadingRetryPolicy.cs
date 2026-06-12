using System;

namespace Game.Bootstrap.Loading
{
    public readonly struct LoadingRetryPolicy
    {
        public static readonly LoadingRetryPolicy None = new(1, TimeSpan.Zero);

        public int MaxAttempts { get; }
        public TimeSpan DelayBetweenAttempts { get; }

        public LoadingRetryPolicy(int maxAttempts, TimeSpan delayBetweenAttempts)
        {
            MaxAttempts = Math.Max(1, maxAttempts);
            DelayBetweenAttempts = delayBetweenAttempts < TimeSpan.Zero ? TimeSpan.Zero : delayBetweenAttempts;
        }
    }
}
