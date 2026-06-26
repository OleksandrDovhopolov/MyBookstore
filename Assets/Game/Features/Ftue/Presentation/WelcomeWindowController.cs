using System;
using System.Collections.Generic;
using Game.UI;
using UnityEngine;
using VContainer;

namespace Game.Ftue
{
    [Window("WelcomeWindow", WindowType.Page)]
    public class WelcomeWindowController : WindowController<WelcomeWindowView>
    {
        [Inject]
        public void Install()
        {
        }
        
        protected override void OnShowStart()
        {
            View.CloseClick += Close;
            View.ClaimClick += Close;
        }

        protected override void OnShowComplete()
        {
            base.OnShowComplete();
            
            ClaimRewards();
        }

        protected override void OnHideStart(bool isClosed)
        {
            View.CloseClick -= Close;
            View.ClaimClick -= Close;
        }

        private void ClaimRewards()
        {
            
        }

        protected override void OnHideComplete(bool isClosed)
        {
        }
        
        private void Close()
        {
        }
    }
}
