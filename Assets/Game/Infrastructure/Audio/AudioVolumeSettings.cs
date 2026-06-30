using System;
using UnityEngine;

namespace Infrastructure.Audio
{
    [Serializable]
    public sealed class AudioVolumeSettings
    {
        [Range(0f, 1f)] public float Master = 1f;
        [Range(0f, 1f)] public float Music = 1f;
        [Range(0f, 1f)] public float Sfx = 1f;
        [Range(0f, 1f)] public float Ui = 1f;

        public AudioVolumeSettings Clone()
        {
            return new AudioVolumeSettings
            {
                Master = Master,
                Music = Music,
                Sfx = Sfx,
                Ui = Ui
            };
        }
    }
}
