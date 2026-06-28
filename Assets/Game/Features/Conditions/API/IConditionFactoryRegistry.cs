namespace Game.Conditions.API
{
    /// <summary>
    /// Lookup of leaf <see cref="IConditionFactory"/> instances by their <see cref="IConditionFactory.Type"/>
    /// discriminator. Populated from all factories registered in DI.
    /// </summary>
    public interface IConditionFactoryRegistry
    {
        bool TryGet(string type, out IConditionFactory factory);
    }
}
