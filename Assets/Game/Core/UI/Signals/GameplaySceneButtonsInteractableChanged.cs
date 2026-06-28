namespace Game.UI
{
    // TODO: Replace this point signal with a scene/UI availability policy service that derives
    // interactability from game phase, modal windows, tutorials, and scene ownership.
    public readonly struct GameplaySceneButtonsInteractableChanged
    {
        public bool Interactable { get; }

        public GameplaySceneButtonsInteractableChanged(bool interactable)
        {
            Interactable = interactable;
        }
    }
}
