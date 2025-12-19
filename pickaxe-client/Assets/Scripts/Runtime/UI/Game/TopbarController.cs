using TMPro;
using UnityEngine;
using InfinitePickaxe.Client.Net;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    /// <summary>
    /// 탭과 무관하게 항상 표시되는 상단바 재화 표시 담당
    /// 서버 이벤트(UserDataSnapshot, CurrencyUpdate, MiningComplete)만 반영하고
    /// 클라이언트에서 임의로 증감하지 않는다.
    /// </summary>
    public class TopbarController : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI goldText;
        [SerializeField] private TextMeshProUGUI crystalText;

        private ulong? currentGold;
        private uint? currentCrystal;
        private MessageHandler messageHandler;

        private void OnEnable()
        {
            messageHandler = MessageHandler.Instance;
            if (messageHandler != null)
            {
                messageHandler.OnHandshakeResult += HandleHandshake;
                messageHandler.OnUserDataSnapshot += HandleSnapshot;
                messageHandler.OnCurrencyUpdate += HandleCurrencyUpdate;
                messageHandler.OnMiningComplete += HandleMiningComplete;
            }
        }

        private void OnDisable()
        {
            if (messageHandler != null)
            {
                messageHandler.OnHandshakeResult -= HandleHandshake;
                messageHandler.OnUserDataSnapshot -= HandleSnapshot;
                messageHandler.OnCurrencyUpdate -= HandleCurrencyUpdate;
                messageHandler.OnMiningComplete -= HandleMiningComplete;
            }
        }

        private void HandleHandshake(HandshakeResponse res)
        {
            if (res != null && res.Snapshot != null)
            {
                HandleSnapshot(res.Snapshot);
            }
        }

        private void HandleSnapshot(UserDataSnapshot snapshot)
        {
            if (snapshot.Gold.HasValue)
            {
                currentGold = snapshot.Gold.Value;
            }
            if (snapshot.Crystal.HasValue)
            {
                currentCrystal = snapshot.Crystal.Value;
            }
            Apply();
        }

        private void HandleCurrencyUpdate(CurrencyUpdate update)
        {
            if (update.Gold.HasValue)
            {
                currentGold = update.Gold.Value;
            }
            if (update.Crystal.HasValue)
            {
                currentCrystal = update.Crystal.Value;
            }
            Apply();
        }

        private void HandleMiningComplete(MiningComplete complete)
        {
            // MiningComplete는 total_gold를 내려준다.
            currentGold = complete.TotalGold;
            Apply();
        }

        private void Apply()
        {
            if (goldText != null && currentGold.HasValue)
            {
                goldText.text = currentGold.Value.ToString("N0");
            }
            if (crystalText != null && currentCrystal.HasValue)
            {
                crystalText.text = currentCrystal.Value.ToString("N0");
            }
        }
    }
}
