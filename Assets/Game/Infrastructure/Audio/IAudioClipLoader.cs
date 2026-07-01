using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Infrastructure.Audio
{
    public interface IAudioClipLoader
    {
        UniTask<AudioClip> LoadAsync(string address, CancellationToken ct);
        void Release(AudioClip clip);
        void ReleaseGroup(IEnumerable<string> addresses);
    }
}
