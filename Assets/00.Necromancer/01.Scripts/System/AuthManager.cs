using Firebase.Auth;
using System.Threading.Tasks;
#if GPGS
using GooglePlayGames;
using GooglePlayGames.BasicApi;
#endif
using UnityEngine;
using System;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.Systems
{
    public enum AuthState
    {
        Initializing,
        LoggedIn,
        Guest,
        Failed
    }

    public class AuthManager : MonoBehaviour
    {
        public static event Action<AuthState> OnAuthStateChanged;
        public AuthState CurrentState { get; private set; } = AuthState.Initializing;

        private FirebaseAuth auth;
        public event Action<bool, string> OnLoginResult;

        public void Init()
        {
            auth = FirebaseAuth.DefaultInstance;

#if GPGS
            // 최신 버전 GPGS 활성화
            PlayGamesPlatform.Activate();
#endif
            // [AUTH] 저장된 로그인 수단을 확인하여 자동 로그인 결정 (Master's Strategy)
            string lastMethod = "None";
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null)
            {
                lastMethod = GameManager.Instance.SaveData.Data.lastLoginMethod;
            }

            Debug.Log($"<color=cyan>[AuthManager]</color> Last Login Method: {lastMethod}");

            if (lastMethod == "Google")
            {
                TryAutoLogin();
            }
            else if (lastMethod == "Guest")
            {
                LoginAsGuest();
            }
            else
            {
                SetState(AuthState.Initializing);
                Debug.Log("[AuthManager] No previous login record found. Waiting for user action.");
            }
        }

        private void TryAutoLogin()
        {
#if GPGS
            // [FIX] 최신 GPGS SDK: 콜백 타입이 Action<bool> → Action<SignInStatus>
            PlayGamesPlatform.Instance.Authenticate((SignInStatus status) => {
                if (status == SignInStatus.Success)
                {
                    LoginWithGoogleFirebase();
                }
                else
                {
                    Debug.Log("[AuthManager] Auto Login Failed or Cancelled.");
                }
            });
#endif
        }

        public void LoginAsGuest()
        {
            auth.SignInAnonymouslyAsync().ContinueWith(task => {
                bool success = !task.IsFaulted && !task.IsCanceled;
                string uid = success ? auth.CurrentUser.UserId : null;

                UnityMainThreadDispatcher.Enqueue(() => {
                    if (success)
                    {
                        SaveLoginMethod("Guest");
                        SetState(AuthState.Guest);
                    }
                    else
                    {
                        SetState(AuthState.Failed);
                    }
                    OnLoginResult?.Invoke(success, uid);
                });
            });
        }

        public void LoginWithGoogle()
        {
#if GPGS
            // [DEBUG] GPGS 인증 시도 로그
            Debug.Log("<color=cyan>[AuthManager]</color> Attempting GPGS Authenticate...");
            PlayGamesPlatform.Instance.Authenticate((SignInStatus status) => {
                Debug.Log($"<color=cyan>[AuthManager]</color> GPGS Authenticate Result: {status}");
                
                if (status == SignInStatus.Success)
                {
                    LoginWithGoogleFirebase();
                }
                else
                {
                    UnityMainThreadDispatcher.Enqueue(() => {
                        Debug.LogWarning($"<color=red>[AuthManager]</color> GPGS Login Failed! Status: {status}. If you are in Editor, this is normal.");
                        SetState(AuthState.Failed);
                        OnLoginResult?.Invoke(false, null);
                    });
                }
            });
#else
            Debug.LogWarning("GPGS 플러그인이 설치되어 있지 않습니다.");
            OnLoginResult?.Invoke(false, null);
#endif
        }

        private void LoginWithGoogleFirebase()
        {
#if GPGS
            Debug.Log("<color=cyan>[AuthManager]</color> Requesting Server Side Access...");
            PlayGamesPlatform.Instance.RequestServerSideAccess(false, serverAuthCode =>
            {
                Debug.Log($"<color=cyan>[AuthManager]</color> Server Auth Code received: {(string.IsNullOrEmpty(serverAuthCode) ? "NULL" : "SUCCESS")}");

                if (string.IsNullOrEmpty(serverAuthCode))
                {
                    UnityMainThreadDispatcher.Enqueue(() => {
                        Debug.LogError("[AuthManager] Failed to get Server Auth Code. Check Web Client ID in GPGS Setup.");
                        SetState(AuthState.Failed);
                        OnLoginResult?.Invoke(false, null);
                    });
                    return;
                }

                Credential credential = GoogleAuthProvider.GetCredential(null, serverAuthCode);

                if (auth.CurrentUser != null && auth.CurrentUser.IsAnonymous)
                {
                    // [FIX] LinkWithCredentialAsync → Task<AuthResult> (Firebase SDK 신버전)
                    auth.CurrentUser.LinkWithCredentialAsync(credential).ContinueWith(task => {
                        bool success = !task.IsFaulted && !task.IsCanceled;
                        string uid = (success && task.Result?.User != null) ? task.Result.User.UserId : null;
                        DispatchAuthResult(success, uid, isLinking: true, exception: task.Exception);
                    });
                }
                else
                {
                    // [FIX] SignInWithCredentialAsync → Task<FirebaseUser> (Firebase SDK 구버전)
                    auth.SignInWithCredentialAsync(credential).ContinueWith(task => {
                        bool success = !task.IsFaulted && !task.IsCanceled;
                        string uid = (success && task.Result != null) ? task.Result.UserId : null;
                        DispatchAuthResult(success, uid, isLinking: false, exception: task.Exception);
                    });
                }
            });
#endif
        }

        // [REFACTOR] 두 메서드의 반환 타입이 다르므로(AuthResult vs FirebaseUser)
        // 결과값을 미리 추출한 뒤 공통 처리 메서드로 위임합니다.
        private void DispatchAuthResult(bool success, string uid, bool isLinking, AggregateException exception)
        {
            UnityMainThreadDispatcher.Enqueue(() => {
                if (success)
                {
                    SaveLoginMethod("Google");
                    SetState(AuthState.LoggedIn);
                    Debug.Log(isLinking
                        ? "<color=green>[AuthManager]</color> Account Linked Successfully!"
                        : "<color=green>[AuthManager]</color> Logged In Successfully!");
                }
                else
                {
                    SetState(AuthState.Failed);
                    Debug.LogError($"[AuthManager] Login/Link Failed: {exception}");
                }
                OnLoginResult?.Invoke(success, uid);
            });
        }

        public void LinkAccount()
        {
            // [STABILITY] 게스트 상태이거나 이전에 로그인이 실패했던 경우에도 연동 시도를 허용합니다.
            if (CurrentState == AuthState.Guest || CurrentState == AuthState.Failed || CurrentState == AuthState.Initializing)
            {
                LoginWithGoogle();
            }
            else if (CurrentState == AuthState.LoggedIn)
            {
                Debug.Log("[AuthManager] Already linked with Google.");
            }
            else
            {
                Debug.LogWarning($"[AuthManager] Cannot link in current state: {CurrentState}");
            }
        }

        public void SwitchAccount()
        {
            Debug.Log("<color=orange>[AuthManager]</color> Attempting to switch account...");
            SignOut();
            LoginWithGoogle();
        }

        public void SignOut()
        {
            if (auth != null) auth.SignOut();
#if GPGS
            // [NOTE] GPGS v11+ (신버전) SDK에서는 PlayGamesPlatform.Instance.SignOut() 메서드가 제거되었습니다.
            // 신버전 GPGS는 시스템 레벨에서 로그인을 관리하므로, 앱 내에서 명시적인 GPGS 로그아웃은 지원되지 않습니다.
            // 대신 Firebase 로그아웃을 통해 앱 내 계정 상태를 초기화합니다.
#endif
            SaveLoginMethod("None");
            SetState(AuthState.Initializing);
            Debug.Log("<color=orange>[AuthManager]</color> Signed out successfully.");
        }

        private void SaveLoginMethod(string method)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null)
            {
                GameManager.Instance.SaveData.Data.lastLoginMethod = method;
                GameManager.Instance.SaveData.Save();
                Debug.Log($"<color=green>[AuthManager]</color> Saved last login method: {method}");
            }
        }

        private void SetState(AuthState newState)
        {
            CurrentState = newState;
            OnAuthStateChanged?.Invoke(newState);
            Debug.Log($"<color=cyan>[AuthManager]</color> State → {newState}");
        }
    }
}
