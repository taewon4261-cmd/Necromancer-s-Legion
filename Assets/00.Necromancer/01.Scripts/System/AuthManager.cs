using Firebase.Auth;
using Firebase.Extensions;
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
        public static event Action OnFirebaseReady; 
        public AuthState CurrentState { get; private set; } = AuthState.Initializing;
        public bool IsFirebaseReady { get; private set; } = false; 

        private FirebaseAuth auth;
        public event Action<bool, string> OnLoginResult;


        public void Init()
        {
            Debug.Log("<color=cyan>[AuthManager]</color> Firebase dependency check started...");
            // [STABILITY] Firebase 의존성 체크 후 비동기 초기화 (Crash 방지)
            Firebase.FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
                var dependencyStatus = task.Result;
                Debug.Log($"<color=cyan>[AuthManager]</color> Firebase dependency result: {dependencyStatus}");
                if (dependencyStatus == Firebase.DependencyStatus.Available)
                {
                    IsFirebaseReady = true;
                    StartInitialization();
                }
                else
                {
                    Debug.LogError($"[AuthManager] Could not resolve all Firebase dependencies: {dependencyStatus}");
                    SetState(AuthState.Failed);
                }
            });
        }

        private void StartInitialization()
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

            Debug.Log($"<color=cyan>[AuthManager]</color> Firebase Initialized. Last Login Method: {lastMethod}");

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

            // [ADDED] Firebase가 이제 정말로 준비됨을 UI에 알림
            OnFirebaseReady?.Invoke();
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
            Debug.Log($"<color=orange>[AuthManager]</color> LoginAsGuest Requested. (FirebaseReady: {IsFirebaseReady})");
            
            if (!IsFirebaseReady)
            {
                Debug.LogWarning("[AuthManager] Firebase is not ready yet. Please wait a few seconds and try again.");
            }

            // [STABILITY] Firebase 초기화 전 클릭 방지
            if (auth == null)
            {
                Debug.LogError("<color=red>[AuthManager]</color> Firebase is not initialized yet!");
                OnLoginResult?.Invoke(false, null);
                return;
            }

            auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task => {
                bool success = !task.IsFaulted && !task.IsCanceled;
                string uid = success ? auth.CurrentUser.UserId : null;

                if (success)
                {
                    Debug.Log("<color=green>[AuthManager]</color> Guest Login Success!");
                    SaveLoginMethod("Guest");
                    SetState(AuthState.Guest);
                }
                else
                {
                    Debug.LogError($"[AuthManager] Guest Login Failed: {task.Exception}");
                    SetState(AuthState.Failed);
                }
                OnLoginResult?.Invoke(success, uid);
            });
        }

        public void LoginWithGoogle()
        {
            Debug.Log($"<color=orange>[AuthManager]</color> LoginWithGoogle Requested. (FirebaseReady: {IsFirebaseReady})");

            if (!IsFirebaseReady)
            {
                Debug.LogWarning("[AuthManager] Firebase is not ready yet. Google login might fail.");
            }

            if (auth == null)
            {
                Debug.LogError("<color=red>[AuthManager]</color> Firebase is not initialized yet!");
                OnLoginResult?.Invoke(false, null);
                return;
            }

#if GPGS
            Debug.Log("<color=cyan>[AuthManager]</color> Attempting GPGS Authenticate...");
            PlayGamesPlatform.Instance.Authenticate((SignInStatus status) => {
                Debug.Log($"<color=cyan>[AuthManager]</color> GPGS Authenticate Result: {status}");
                
                if (status == SignInStatus.Success)
                {
                    LoginWithGoogleFirebase();
                }
                else
                {
                    // [FIX] Task를 사용하여 메인 스레드로 안전하게 복귀
                    Task.Yield().GetAwaiter().OnCompleted(() => {
                        Debug.LogWarning($"<color=red>[AuthManager]</color> GPGS Login Failed! Status: {status}");
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
                    Task.Yield().GetAwaiter().OnCompleted(() => {
                        Debug.LogError("[AuthManager] Failed to get Server Auth Code. Check Web Client ID in GPGS Setup.");
                        SetState(AuthState.Failed);
                        OnLoginResult?.Invoke(false, null);
                    });
                    return;
                }

                Credential credential = PlayGamesAuthProvider.GetCredential(serverAuthCode);

                if (auth.CurrentUser != null && auth.CurrentUser.IsAnonymous)
                {
                    // [FIX] LinkWithCredentialAsync
                    auth.CurrentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
                        bool success = !task.IsFaulted && !task.IsCanceled;
                        string uid = (success && task.Result?.User != null) ? task.Result.User.UserId : null;
                        DispatchAuthResult(success, uid, isLinking: true, exception: task.Exception);
                    });
                }
                else
                {
                    // [FIX] SignInWithCredentialAsync
                    auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
                        bool success = !task.IsFaulted && !task.IsCanceled;
                        string uid = (success && task.Result != null) ? task.Result.UserId : null;
                        DispatchAuthResult(success, uid, isLinking: false, exception: task.Exception);
                    });
                }
            });
#endif
        }

        private void DispatchAuthResult(bool success, string uid, bool isLinking, AggregateException exception)
        {
            if (success)
            {
                SaveLoginMethod("Google");
                SetState(AuthState.LoggedIn);
                Debug.Log(isLinking
                    ? "<color=green>[AuthManager]</color> Account Linked Successfully!"
                    : "<b><color=green>[AuthManager] GOOGLE LOGIN SUCCESS!!</color></b> UID: " + uid);
            }
            else
            {
                SetState(AuthState.Failed);
                Debug.LogError($"[AuthManager] Login/Link Failed: {exception}");
            }
            OnLoginResult?.Invoke(success, uid);
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
