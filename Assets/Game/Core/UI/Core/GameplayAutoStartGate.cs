using System;
using UnityEngine;

namespace Game.UI
{
    public sealed class GameplayAutoStartGate : IGameplayAutoStartGate
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
                Debug.LogWarning("[GameplayAutoStartGate] Release called without a matching Block.");
                return;
            }

            _blockCount--;
            if (_blockCount == 0)
                Released?.Invoke();
        }
    }
}
