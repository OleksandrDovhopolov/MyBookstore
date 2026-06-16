using System;
using System.Threading;
using cheatModule;
using Game.Cheat;
using Game.UI;
using UnityEngine;

namespace Game.Cheat
{
    public class DecorationCheatModule : ICheatsModule
    {
        private const string CardsGroup = "Decoration";
        
        private readonly UIManager _uiManager;
        private readonly CancellationToken _ct;
        
        public DecorationCheatModule(UIManager uiManager, CancellationToken ct)
        {
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _ct = ct;
        }
        
        public void Initialize(ICheatsContainer cheatsContainer)
        {
            cheatsContainer.AddItem<CheatButtonItem>(item => item.OnClick("Add decoration from inventory", () =>
            {
                //TODO add decoration
            }).WithGroup(CardsGroup));
            
            cheatsContainer.AddItem<CheatButtonItem>(item => item.OnClick("Remove decoration from inventory", () =>
            {
                //remove is needed ? 
            }).WithGroup(CardsGroup));
        }
    }
}