using UnityEngine;

namespace InfinitePickaxe.Client.Config
{
    [CreateAssetMenu(fileName = "ClientConfigAsset", menuName = "InfinitePickaxe/Client Config")]
    public sealed class ClientConfigAsset : ScriptableObject
    {
        [SerializeField] private ClientConfigData data = ClientConfigData.Default();

        public ClientConfigData Data => data;
    }
}
