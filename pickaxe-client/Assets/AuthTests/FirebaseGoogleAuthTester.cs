using System;
using System.Threading.Tasks;
using Firebase;
using Firebase.Auth;
using Google;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class FirebaseGoogleAuthTester : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button loginButton;
    [SerializeField] private Button logoutButton;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text emailLabel;
    [SerializeField] private TMP_Text idLabel;
    [SerializeField] private Image profileImage;

    [Header("Config")]
    [SerializeField] private string webClientId = "";
    [SerializeField] private Sprite fallbackProfile;
    [SerializeField] private string fallbackName = "Name...";
    [SerializeField] private string fallbackEmail = "email...";
    [SerializeField] private string fallbackId = "id...";

    private FirebaseAuth auth;
    private bool initializing;

    private void Awake()
    {
        ApplyFallbackUi();
    }

    private async void Start()
    {
        await EnsureFirebaseReadyAsync();
    }

    public async void OnLoginClicked()
    {
        if (string.IsNullOrWhiteSpace(webClientId))
        {
            Debug.LogError("Web client ID is missing. Assign it in the inspector before running the login test.");
            return;
        }

        await EnsureFirebaseReadyAsync();
        if (auth == null)
        {
            return;
        }

        SetButtonsInteractable(false);

        try
        {
            ConfigureGoogleSignIn();

            // Sign out first to force the account picker to appear every time.
            GoogleSignIn.DefaultInstance.SignOut();

            var googleUser = await GoogleSignIn.DefaultInstance.SignIn();
            var credential = GoogleAuthProvider.GetCredential(googleUser.IdToken, null);
            var firebaseUser = await auth.SignInWithCredentialAsync(credential);

            await PopulateUiAsync(firebaseUser);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Login failed: {ex}");
            ApplyFallbackUi();
        }
        finally
        {
            SetButtonsInteractable(true);
        }
    }

    public void OnLogoutClicked()
    {
        if (auth != null)
        {
            auth.SignOut();
        }

        GoogleSignIn.DefaultInstance.SignOut();
        GoogleSignIn.DefaultInstance.Disconnect();

        ApplyFallbackUi();
    }

    private async Task EnsureFirebaseReadyAsync()
    {
        if (auth != null || initializing)
        {
            return;
        }

        initializing = true;

        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            ConfigureGoogleSignIn();
        }
        else
        {
            Debug.LogError($"Could not resolve Firebase dependencies: {dependencyStatus}");
        }

        initializing = false;
    }

    private void ConfigureGoogleSignIn()
    {
        GoogleSignIn.Configuration = new GoogleSignInConfiguration
        {
            WebClientId = webClientId,
            RequestEmail = true,
            RequestIdToken = true,
            RequestProfile = true,
            UseGameSignIn = false,
            ForceTokenRefresh = false
        };
    }

    private async Task PopulateUiAsync(FirebaseUser user)
    {
        nameLabel.text = string.IsNullOrWhiteSpace(user.DisplayName) ? fallbackName : user.DisplayName;
        emailLabel.text = string.IsNullOrWhiteSpace(user.Email) ? fallbackEmail : user.Email;
        idLabel.text = string.IsNullOrWhiteSpace(user.UserId) ? fallbackId : user.UserId;

        profileImage.sprite = fallbackProfile;
        profileImage.color = Color.white;

        var photoUrl = user.PhotoUrl?.ToString();
        if (string.IsNullOrEmpty(photoUrl))
        {
            return;
        }

        try
        {
            var sprite = await DownloadSpriteAsync(photoUrl);
            if (sprite != null)
            {
                profileImage.sprite = sprite;
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Profile image download failed: {ex}");
        }
    }

    private static async Task<Sprite> DownloadSpriteAsync(string url)
    {
        using (var request = UnityWebRequestTexture.GetTexture(url))
        {
            var operation = request.SendWebRequest();
            while (!operation.isDone)
            {
                await Task.Yield();
            }

#if UNITY_2020_1_OR_NEWER
            if (request.result != UnityWebRequest.Result.Success)
#else
            if (request.isNetworkError || request.isHttpError)
#endif
            {
                throw new Exception(request.error);
            }

            var texture = DownloadHandlerTexture.GetContent(request);
            var rect = new Rect(0, 0, texture.width, texture.height);
            return Sprite.Create(texture, rect, new Vector2(0.5f, 0.5f));
        }
    }

    private void ApplyFallbackUi()
    {
        nameLabel.text = fallbackName;
        emailLabel.text = fallbackEmail;
        idLabel.text = fallbackId;
        profileImage.sprite = fallbackProfile;
        profileImage.color = fallbackProfile != null ? Color.white : new Color(1f, 1f, 1f, 0.35f);
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (loginButton != null)
        {
            loginButton.interactable = interactable;
        }

        if (logoutButton != null)
        {
            logoutButton.interactable = interactable;
        }
    }
}
