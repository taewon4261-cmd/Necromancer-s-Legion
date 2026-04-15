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
            Debug.Log("<color=cyan>[AuthManager]</color> Attempting GPGS ManuallyAuthenticate (Interactive)...");
            // [FIX] Authenticate 대신 ManuallyAuthenticate를 사용하여 계정 선택창 유도
            PlayGamesPlatform.Instance.ManuallyAuthenticate((SignInStatus status) => {
                Debug.Log($"<color=cyan>[AuthManager]</color> GPGS ManuallyAuthenticate Result: {status}");
                
                if (status == SignInStatus.Success)
                {
                    LoginWithGoogleFirebase();
                }
                else
                {
                    // [FIX] Task를 사용하여 메인 스레드로 안전하게 복귀
                    Task.Yield().GetAwaiter().OnCompleted(() => {
                        Debug.LogWarning($"<color=red>[AuthManager]</color> GPGS Manually Login Failed! Status: {status}");
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
                    auth.CurrentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task => {
                        // 연동 실패 시 (예: 이미 가입된 구글 계정인 경우)
                        if (task.IsFaulted || task.IsCanceled)
                        {
                            Debug.LogWarning($"<color=orange>[AuthManager]</color> Link failed (Credential likely in use). Trying SignIn instead...\nException: {task.Exception}");
                            
                            // 해당 구글 계정으로 일반 로그인 시도 (이전 데이터 복구)
                            auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(signInTask => {
                                bool success = !signInTask.IsFaulted && !signInTask.IsCanceled;
                                string uid = (success && signInTask.Result != null) ? signInTask.Result.UserId : null;
                                DispatchAuthResult(success, uid, isLinking: false, exception: signInTask.Exception);
                            });
                        }
                        else // 연동 성공 시
                        {
                            string uid = task.Result?.User != null ? task.Result.User.UserId : null;
                            DispatchAuthResult(true, uid, isLinking: true, exception: null);
                        }
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
            if (!success)
            {
                SetState(AuthState.Failed);
                Debug.LogError($"[AuthManager] Login/Link Failed: {exception}");
                OnLoginResult?.Invoke(false, uid);
                return;
            }

            Debug.Log(isLinking
                ? "<color=green>[AuthManager]</color> Account Linked Successfully!"
                : "<b><color=green>[AuthManager] GOOGLE LOGIN SUCCESS!!</color></b> UID: " + uid);

            // [CLOUD] UID 등록 후 Firestore에서 데이터 복구
            var saveManager = GameManager.Instance?.SaveData;
            if (saveManager != null && !string.IsNullOrEmpty(uid))
            {
                saveManager.SetCloudUser(uid);
                saveManager.LoadFromCloud(uid).ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted)
                    {
                        Debug.LogError($"[AuthManager] LoadFromCloud error: {task.Exception}");
                        // [FIX] 데이터 로드 실패 시 로그인 상태로 넘어가지 않고 실패 상태로 유지하여 덮어쓰기 방지
                        SetState(AuthState.Failed);
                        OnLoginResult?.Invoke(false, uid);
                    }
                    else
                    {
                        Debug.Log($"[AuthManager] Cloud sync complete. HasData={task.Result}");

                        // [FIX] 데이터 로드 성공 시 게임 시스템 전체 새로고침 (화면 반영)
                        if (task.Result)
                        {
                            GameManager.Instance.RefreshSystemsAfterLoad();
                        }
                        // [FIX] 첫 로그인이라 클라우드에 데이터가 없으면 즉시 문서 생성
                        else
                        {
                            Debug.Log("[AuthManager] 첫 로그인 감지. Firestore에 문서를 즉시 생성합니다.");
                            _ = saveManager.SaveToCloud(uid);
                        }

                        // [FIX] 모든 데이터 처리가 끝난 후(Load 완료 후)에만 로그인 상태로 전환 및 수단 저장
                        SaveLoginMethod("Google");
                        SetState(AuthState.LoggedIn);
                        OnLoginResult?.Invoke(true, uid);
                    }
                });
            }
            else
            {
                SaveLoginMethod("Google");
                SetState(AuthState.LoggedIn);
                OnLoginResult?.Invoke(true, uid);
            }
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
            // GPGS SDK 버전에 따라 SignOut() 지원 여부가 다름
            // ManuallyAuthenticate()가 이미 계정 선택을 강제하므로 GPGS 세션 리셋 불필요
            Debug.Log("<color=orange>[AuthManager]</color> GPGS session will be reset on next ManuallyAuthenticate call.");
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
