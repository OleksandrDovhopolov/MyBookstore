using UnityEngine;
using UnityEngine.UI;

namespace Infrastructure
{
    public class RemoteSpriteProbe : MonoBehaviour
    {
        [SerializeField] private Image _target;

        private async void Start()
        {
            var sprite = await ProdAddressablesWrapper.LoadAsync<Sprite>("test_sprite", destroyCancellationToken);
            _target.sprite = sprite;
        }

        private void OnDestroy() => ProdAddressablesWrapper.Release("test_sprite");
    }
}
