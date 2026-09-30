using Game.Configs.Models;

namespace Book.Sell.Services
{
    /// <summary>
    /// Turns a request's boolean conditions into the customer-facing line shown in the recommendation
    /// minigame. Replaces the technical debug dump that used to be displayed.
    /// </summary>
    public interface IActiveRequestTextComposer
    {
        string Compose(RequestDefinitionConfig request);
    }
}
