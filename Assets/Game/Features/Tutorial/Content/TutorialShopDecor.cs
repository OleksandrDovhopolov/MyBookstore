using System.Collections.Generic;
using Game.Inventory.API;
using Game.Tutorial.API;

namespace Game.Tutorial.Content
{
    public sealed class TutorialShopDecor : ITutorialSequence
    {
        private readonly IInventoryService _inventory;

        public TutorialShopDecor(IInventoryService inventory)
        {
            _inventory = inventory;
        }

        public string Id => "tutorial_shop_decor";
        public int Priority => 40;
        public TutorialContext Context => TutorialContext.Hub;
        public TutorialTrigger Trigger => TutorialTrigger.HubReady;
        public string TriggerParam => null;
        public TutorialResumePolicy ResumePolicy => TutorialResumePolicy.Restart;

        public bool IsEligible()
            => (_inventory?.GetByCategory(InventoryCategories.Decor)?.Count ?? 0) > 0;

        public IReadOnlyList<ITutorialStep> GetSteps()
            => new ITutorialStep[]
            {
                new TutorialLogStep("shop_decor_log", "[Tutorial] Decor purchase detected (stub).")
            };

        public void OnRunStarted() { }
        public void OnRunEnded() { }
    }
}
