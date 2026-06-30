using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Audio
{
    public sealed class AddressablesAudioClipLoader : IAudioClipLoader
    {
        public UniTask<AudioClip> LoadAsync(string address, CancellationToken ct)
            => global::Infrastructure.ProdAddressablesWrapper.LoadAsync<AudioClip>(address, ct);

        public void Release(AudioClip clip)
        {
            if (clip != null)
                global::Infrastructure.ProdAddressablesWrapper.Release(clip);
        }

        public void ReleaseGroup(IEnumerable<string> addresses)
        {
            global::Infrastructure.ProdAddressablesWrapper.ReleaseGroup(addresses);
        }
    }
}
