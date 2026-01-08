using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public partial class MiningTabController
    {
        [Header("Challenge Tab")]
        [SerializeField] private Transform challengeContentContainer;
        [SerializeField] private GameObject infiniteMineCardPrefab;
        [SerializeField] private Button infiniteMineCardButton;
        [SerializeField] private InfiniteMineModalController infiniteMineModal;

        private GameObject infiniteMineCardInstance;
        private bool challengeTabInitialized;

        private void InitializeChallengeTab()
        {
            if (challengeTabInitialized) return;

            EnsureChallengeTabReferences();
            BindChallengeTabButtons();

            challengeTabInitialized = true;
        }

        private void EnsureChallengeTabReferences()
        {
            var root = challengeTabPanel != null ? challengeTabPanel.transform : transform;

            if (challengeContentContainer == null)
            {
                var container = FindChildRecursive(root, "ChallengeContentContainer");
                if (container == null)
                {
                    container = FindChildRecursive(root, "ChallengeContent");
                }
                if (container != null) challengeContentContainer = container;
            }

            if (infiniteMineCardPrefab == null)
            {
                var prefabTf = FindChildRecursive(root, "InfiniteMineCardPrefab");
                if (prefabTf != null) infiniteMineCardPrefab = prefabTf.gameObject;
            }

            if (infiniteMineCardButton == null)
            {
                var cardTf = FindChildRecursive(root, "InfiniteMineCard");
                if (cardTf != null)
                {
                    infiniteMineCardButton = cardTf.GetComponent<Button>() ?? cardTf.GetComponentInChildren<Button>(true);
                }
            }

            EnsureInfiniteMineCardInstance();
            AutoBindInfiniteMineModal();
        }

        private void EnsureInfiniteMineCardInstance()
        {
            if (infiniteMineCardButton != null) return;
            if (challengeContentContainer == null || infiniteMineCardPrefab == null) return;

            if (infiniteMineCardInstance == null)
            {
                infiniteMineCardInstance = Instantiate(infiniteMineCardPrefab, challengeContentContainer, false);
                infiniteMineCardInstance.name = "InfiniteMineCard";
                infiniteMineCardInstance.SetActive(true);
            }

            infiniteMineCardButton = infiniteMineCardInstance.GetComponent<Button>()
                ?? infiniteMineCardInstance.GetComponentInChildren<Button>(true);
        }

        private void BindChallengeTabButtons()
        {
            if (infiniteMineCardButton != null)
            {
                infiniteMineCardButton.onClick.RemoveAllListeners();
                infiniteMineCardButton.onClick.AddListener(OpenInfiniteMineModal);
            }
        }

        private void OpenInfiniteMineModal()
        {
            AutoBindInfiniteMineModal();
            if (infiniteMineModal == null) return;
            infiniteMineModal.Show();
        }

        private void AutoBindInfiniteMineModal()
        {
            if (infiniteMineModal != null) return;

            var modalObj = GameObject.Find("InfiniteMineModal");
            if (modalObj != null)
            {
                infiniteMineModal = modalObj.GetComponent<InfiniteMineModalController>();
                return;
            }

            var prefab = Resources.Load<GameObject>("UI/InfiniteMineModal");
            if (prefab == null) return;
            var instance = Instantiate(prefab, transform.root);
            instance.name = "InfiniteMineModal";
            instance.SetActive(false);
            infiniteMineModal = instance.GetComponent<InfiniteMineModalController>();
        }
    }
}
