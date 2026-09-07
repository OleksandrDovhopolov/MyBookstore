namespace Game.Decor.UI
{
    public static class DecorSizeVisualScale
    {
        public const float SmallFactor = 1.25f;
        public const float MediumFactor = 2f;
        public const float LargeFactor = 3f;

        public static float Factor(DecorSize size)
        {
            switch (size)
            {
                case DecorSize.Small:
                    return SmallFactor;
                case DecorSize.Medium:
                    return MediumFactor;
                case DecorSize.Large:
                    return LargeFactor;
                default:
                    return LargeFactor;
            }
        }
    }
}
