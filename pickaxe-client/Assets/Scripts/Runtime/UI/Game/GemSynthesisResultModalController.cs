using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Infinitepickaxe;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class GemSynthesisResultModalController : MonoBehaviour
    {
        [Header("Modal UI")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Transform gemItemContainer;
        [SerializeField] private ScrollRect gemScrollRect;

        [Header("Buttons")]
        [SerializeField] private Button closeButton;

        [Header("Prefabs")]
        [SerializeField] private GameObject gemResultItemPrefab;

        private readonly List<GameObject> spawnedItems = new List<GameObject>();

        private void Awake()
        {
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(Close);
            }
        }

        public void ShowSynthesisResult(GemSynthesisResult result)
        {
            if (result == null) return;

            var title = result.SynthesisSuccess ? "합성 성공" : "합성 실패";
            var gems = new List<GemInfo>();

            if (result.ResultGem != null)
            {
                gems.Add(result.ResultGem);
            }
            else if (result.RetainedGem != null)
            {
                gems.Add(result.RetainedGem);
            }

            SetResults(title, gems);
        }

        public void ShowAutoSynthesisResult(GemAutoSynthesisResult result)
        {
            if (result == null) return;

            var title = $"자동 합성 결과 ({result.SuccessCount}/{result.Attempted})";
            SetResults(title, result.ResultGems);
        }

        private void SetResults(string title, IEnumerable<GemInfo> gems)
        {
            if (titleText != null) titleText.text = title;

            ClearItems();

            if (gemItemContainer == null || gemResultItemPrefab == null || gems == null)
            {
                gameObject.SetActive(true);
                return;
            }

            foreach (var gem in gems)
            {
                if (gem == null) continue;

                var itemObj = Instantiate(gemResultItemPrefab);
                if (gemItemContainer != null)
                {
                    itemObj.transform.SetParent(gemItemContainer, false);
                }
                spawnedItems.Add(itemObj);

                var itemView = itemObj.GetComponent<GemResultItemView>();
                if (itemView != null)
                {
                    itemView.SetGem(gem);
                }
            }

            if (gemScrollRect != null)
            {
                gemScrollRect.normalizedPosition = new Vector2(0, 1);
            }

            gameObject.SetActive(true);
        }

        private void ClearItems()
        {
            for (int i = 0; i < spawnedItems.Count; i++)
            {
                if (spawnedItems[i] != null)
                {
                    Destroy(spawnedItems[i]);
                }
            }
            spawnedItems.Clear();
        }

        private void Close()
        {
            gameObject.SetActive(false);
        }
    }
}
