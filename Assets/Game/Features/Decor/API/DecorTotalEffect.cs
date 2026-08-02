namespace Game.Decor
{
    public readonly struct DecorTotalEffect
    {
        public DecorTotalEffect(DecorEffectKind kind, string subject, float percent)
        {
            Kind = kind;
            Subject = subject;
            Percent = percent;
        }

        public DecorEffectKind Kind { get; }
        public string Subject { get; }
        public float Percent { get; }
    }
}
