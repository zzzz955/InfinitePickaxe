using System.Collections;
using Pickaxe.Client.Services;
using UnityEngine;
using TMPro;

namespace Pickaxe.Client.UI
{
    /// <summary>
    /// 에디터/디바이스에서 Google Sign-In 테스트용 간단한 UI 후크.
    /// 버튼의 OnClick에 연결해 사용.
    /// </summary>
    public class AuthTestUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text statusText;

        private void Start()
        {
            StartCoroutine(InitRoutine());
        }

        private IEnumerator InitRoutine()
        {
            statusText?.SetText("Initializing...");
            var task = GoogleAuthService.Instance.InitializeAsync();
            while (!task.IsCompleted) yield return null;
            statusText?.SetText(task.Result ? "Init OK" : "Init Failed");
        }

        public void OnSignInClicked()
        {
            StartCoroutine(SignInRoutine());
        }

        public void OnSignOutClicked()
        {
            GoogleAuthService.Instance.SignOut();
            statusText?.SetText("Signed out");
        }

        private IEnumerator SignInRoutine()
        {
            statusText?.SetText("Signing in...");
            var task = GoogleAuthService.Instance.SignInAsync();
            while (!task.IsCompleted) yield return null;
            var user = task.Result;
            if (user == null)
            {
                statusText?.SetText("Sign-In failed");
            }
            else
            {
                statusText?.SetText($"Signed in: {user.Email}");
            }
        }
    }

    internal static class TextExtensions
    {
        public static void SetText(this TMP_Text text, string value)
        {
            if (text == null) return;
            text.text = value;
        }
    }
}
