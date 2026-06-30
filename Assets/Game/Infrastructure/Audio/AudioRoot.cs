using UnityEngine;

namespace Infrastructure.Audio
{
    public sealed class AudioRoot : MonoBehaviour
    {
        public AudioSource MusicSource { get; private set; }
        public AudioSource SfxSource { get; private set; }
        public AudioSource UiSource { get; private set; }
        public AudioSource AmbientSource { get; private set; }

        public static AudioRoot Create()
        {
            var rootObject = new GameObject("[AudioRoot]");
            DontDestroyOnLoad(rootObject);

            var root = rootObject.AddComponent<AudioRoot>();
            root.MusicSource = CreateSource(rootObject.transform, "Music", loop: true);
            root.SfxSource = CreateSource(rootObject.transform, "Sfx", loop: false);
            root.UiSource = CreateSource(rootObject.transform, "Ui", loop: false);
            root.AmbientSource = CreateSource(rootObject.transform, "Ambient", loop: true);

            return root;
        }

        private static AudioSource CreateSource(Transform parent, string name, bool loop)
        {
            var sourceObject = new GameObject(name);
            sourceObject.transform.SetParent(parent, false);

            var source = sourceObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = 1f;

            return source;
        }
    }
}
