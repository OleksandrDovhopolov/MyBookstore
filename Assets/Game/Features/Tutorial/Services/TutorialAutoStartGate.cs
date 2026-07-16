using System;
using Game.Tutorial.API;
using UnityEngine;

namespace Game.Tutorial.Services
{
    public sealed class TutorialAutoStartGate : ITutorialAutoStartGate
    {
        private int _blockCount;

        public bool IsBlocked => _blockCount > 0;

        public event Action Released;

        public void Block()
        {
            _blockCount++;
        }

        public void Release()
        {
            if (_blockCount <= 0)
            {
                Debug.LogWarning("[TutorialAutoStartGate] Release called without a matching Block.");
                return;
            }

            _blockCount--;
            if (_blockCount == 0)
                Released?.Invoke();
        }
    }
}
