using UnityEngine;
using UnityEngine.UI;

namespace InfinitePickaxe.Client.UI.Game
{
    public sealed class InfiniteMineSimulationViewController : MonoBehaviour
    {
        [SerializeField] private GameObject uiRootPanel;
        [SerializeField] private Button closeButton;

        private bool uiPanelWasActive;

        private void Awake()
        {
            EnsureReferences();
            BindButtons();
        }

        private void OnEnable()
        {
            EnsureReferences();
            if (uiRootPanel != null)
            {
                uiPanelWasActive = uiRootPanel.activeSelf;
                if (uiPanelWasActive)
                {
                    uiRootPanel.SetActive(false);
                }
            }
        }

        private void OnDisable()
        {
            if (uiRootPanel != null && uiPanelWasActive)
            {
                uiRootPanel.SetActive(true);
            }
        }

        public void Show()
        {
            EnsureReferences();
            BindButtons();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        private void EnsureReferences()
        {
            if (uiRootPanel == null)
            {
                var uiCanvas = GameObject.Find("UI Canvas");
                if (uiCanvas != null)
                {
                    var panel = uiCanvas.transform.Find("Panel");
                    if (panel != null) uiRootPanel = panel.gameObject;
                }
            }

            if (closeButton == null)
            {
                var closeTf = transform.Find("CloseButton");
                if (closeTf == null)
                {
                    closeTf = transform.Find("TopBar/CloseButton");
                }
                if (closeTf != null)
                {
                    closeButton = closeTf.GetComponent<Button>();
                }
                else
                {
                    var buttons = GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i] != null && buttons[i].name == "CloseButton")
                        {
                            closeButton = buttons[i];
                            break;
                        }
                    }
                }
            }
        }

        private void BindButtons()
        {
            if (closeButton == null) return;
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
    }
}
