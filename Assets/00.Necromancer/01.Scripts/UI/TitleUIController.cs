using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Necromancer.Systems;
using Cysharp.Threading.Tasks;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// 타이틀 씬의 UI 전환을 SetActive(True/False)로만 관리합니다.
    /// 버튼 바인딩은 전량 Inspector 직렬화 방식을 사용하며,
    /// 런타임 Find / GetComponentsInChildren 스캔이 없습니다.
    /// 서브 패널 내부 갱신은 각 패널의 OnEnable()이 자동으로 담당합니다.
    /// </summary>
    public class TitleUIController : MonoBehaviour
    {
        [Header("Main Panels")]
        public GameObject authPanel;
        public GameObject mainButtonPanel;
        public RectTransform logoTransform;

        [Header("Sub Panels")]
        public GameObject stageSelectPanel;
        public GameObject upgradePanel;
        public GameObject minionStorePanel;
        public GameObject settingPanel;

        [Header("Main Menu Buttons")]
        [SerializeField] private Button btnStart;
        [SerializeField] private Button btnUpgrade;
        [SerializeField] private Button btnMinionStore;
        [SerializeField] private Button btnSetting;

        [Header("Back Buttons (Sub Panels)")]
        [SerializeField] private Button btnStageSelectBack;
        [SerializeField] private Button btnUpgradeBack;
        [SerializeField] private Button btnMinionStoreBack;
        [SerializeField] private Button btnSettingBack;

        [Header("Auth Panel Buttons")]
        [SerializeField] private Button btnGuest;
        [SerializeField] private Button btnGoogle;

        [Header("Toast Notification")]
        [SerializeField] private CanvasGroup toastCanvasGroup;
        [SerializeField] private TextMeshProUGUI toastText;

        private readonly List<GameObject> allSubPanels = new List<GameObject>();
        private bool isTitleInitialized = false;

        private void Awake()
        {
            ValidateReferences();

            if (stageSelectPanel != null) allSubPanels.Add(stageSelectPanel);
            if (upgradePanel != null)     allSubPanels.Add(upgradePanel);
            if (minionStorePanel != null) allSubPanels.Add(minionStorePanel);
            if (settingPanel != null)     allSubPanels.Add(settingPanel);

            // Firebase 초기화 완료(OnFirebaseReady) 전까지 버튼 비활성화
            SetButtonsInteractable(false);

            if (GameManager.Instance != null) GameManager.Instance.RegisterTitleUI(this);

            InitAllPanels();
        }

        private void OnEnable()
        {
            AuthManager.OnAuthStateChanged += HandleAuthState;
            AuthManager.OnFirebaseReady += EnableLoginButtons;
            if (GameManager.Instance != null && GameManager.Instance.Auth != null)
            {
                GameManager.Instance.Auth.OnLoginResult += OnLoginSuccess;
            }
        }

        private void OnDisable()
        {
            AuthManager.OnAuthStateChanged -= HandleAuthState;
            AuthManager.OnFirebaseReady -= EnableLoginButtons;
            if (GameManager.Instance != null && GameManager.Instance.Auth != null)
            {
                GameManager.Instance.Auth.OnLoginResult -= OnLoginSuccess;
            }
        }

        private void Start()
        {
            SetupButtonEvents();

            if (GameManager.Instance != null && GameManager.Instance.Auth != null)
            {
                // [FIX] OnEnable 시점에 Auth가 null이어서 구독이 누락됐을 경우 재구독
                GameManager.Instance.Auth.OnLoginResult -= OnLoginSuccess;
                GameManager.Instance.Auth.OnLoginResult += OnLoginSuccess;

                // OnFirebaseReady 이벤트를 OnEnable 구독 전에 이미 놓쳤을 경우 대비
                if (GameManager.Instance.Auth.IsFirebaseReady)
                    SetButtonsInteractable(true);

                var currentState = GameManager.Instance.Auth.CurrentState;
                HandleAuthState(currentState);

                // [FIX] 자동 로그인 등으로 이미 로그인이 완료된 상태라면 토스트 메시지 출력
                if (currentState == AuthState.LoggedIn)
                {
                    ShowToast("구글 로그인 성공!");
                }
                else if (currentState == AuthState.Guest)
                {
                    ShowToast("게스트 로그인 성공!");
                }
            }
        }

        private void EnableLoginButtons()
        {
            SetButtonsInteractable(true);
            Debug.Log("<color=cyan>[TitleUI]</color> Firebase ready. Login buttons enabled.");
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (btnGuest != null) btnGuest.interactable = interactable;
            if (btnGoogle != null) btnGoogle.interactable = interactable;
        }

        private void HandleAuthState(AuthState state)
        {
            // [FIX] 로그인 성공(Guest/LoggedIn) 시에는 초기화 여부와 상관없이 화면을 전환하도록 보강
            if (isTitleInitialized && (state != AuthState.LoggedIn && state != AuthState.Guest))
                return;

            switch (state)
            {
                case AuthState.LoggedIn:
                case AuthState.Guest:
                    isTitleInitialized = true;
                    if (authPanel != null) authPanel.SetActive(false);
                    if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
                    Debug.Log($"<color=green>[TitleUI]</color> Login Success! UI Transition to Main Menu. (State: {state})");
                    break;
                case AuthState.Initializing:
                    Debug.Log("<color=cyan>[TitleUI]</color> Firebase initializing...");
                    break;
                case AuthState.Failed:
                    // 자동로그인 실패 시 authPanel 복원 후 버튼 활성화 (부모가 꺼져있으면 버튼도 안 보임)
                    if (authPanel != null) authPanel.SetActive(true);
                    if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
                    SetButtonsInteractable(true);
                    Debug.LogWarning($"<color=red>[TitleUI]</color> Login Failed. Showing Auth Panel.");
                    break;
            }
        }

        private void InitAllPanels()
        {
            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            foreach (var panel in allSubPanels)
                if (panel != null) panel.SetActive(false);

            var saveData = GameManager.Instance?.SaveData?.Data;
            bool hasLoginRecord = saveData != null && saveData.lastLoginMethod != "None";

            if (authPanel != null) authPanel.SetActive(!hasLoginRecord);

            if (hasLoginRecord)
                Debug.Log("<color=cyan>[TitleUI]</color> Login record found. Hiding login panel for auto-login.");
        }

        public void SetupInitialUI() => InitAllPanels();

        public void OnLoginSuccess(bool success, string uid)
        {
            if (success)
            {
                Debug.Log($"<color=green>[TitleUI]</color> Login/Link Success (UID: {uid}): Navigating to Main UI.");

                if (authPanel != null) authPanel.SetActive(false);
                if (mainButtonPanel != null) mainButtonPanel.SetActive(true);

                // 로그인 방식에 따라 토스트 메시지 구분
                var authState = GameManager.Instance?.Auth?.CurrentState;
                if (authState == AuthState.LoggedIn)
                    ShowToast("구글 로그인 성공!");
                else if (authState == AuthState.Guest)
                    ShowToast("게스트 로그인 성공!");
            }
        }

        private void ShowToast(string message)
        {
            if (toastCanvasGroup == null) return;
            StopCoroutine("ToastCoroutine");
            StartCoroutine(ToastCoroutine(message));
        }

        private IEnumerator ToastCoroutine(string message)
        {
            if (toastText != null) toastText.text = message;
            toastCanvasGroup.alpha = 0f;
            toastCanvasGroup.gameObject.SetActive(true);

            // Fade in (0.3s)
            float t = 0f;
            while (t < 0.3f)
            {
                t += Time.deltaTime;
                toastCanvasGroup.alpha = Mathf.Clamp01(t / 0.3f);
                yield return null;
            }
            toastCanvasGroup.alpha = 1f;

            yield return new WaitForSeconds(2f);

            // Fade out (0.5s)
            t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                toastCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.5f);
                yield return null;
            }
            toastCanvasGroup.gameObject.SetActive(false);
        }

        public void ShowPanel(GameObject targetPanel)
        {
            if (targetPanel == null) return;
            if (mainButtonPanel != null) mainButtonPanel.SetActive(false);
            targetPanel.SetActive(true);
            PlaySelectSound();
            Debug.Log($"<color=green>[TitleUI]</color> Open SubPanel: {targetPanel.name}");
        }

        public void BackToMainMenu()
        {
            foreach (var panel in allSubPanels)
                if (panel != null) panel.SetActive(false);
            if (mainButtonPanel != null) mainButtonPanel.SetActive(true);
            PlaySelectSound();
        }

        private void SetupButtonEvents()
        {
            // [FORCE] 로그인 버튼 이벤트 강제 할당
            if (btnGuest != null)
            {
                btnGuest.onClick.RemoveAllListeners();
                btnGuest.onClick.AddListener(OnGuestLoginClick);
                Debug.Log("[TitleUI] Guest Button Listener Linked");
            }

            if (btnGoogle != null)
            {
                btnGoogle.onClick.RemoveAllListeners();
                btnGoogle.onClick.AddListener(OnGoogleLoginClick);
                Debug.Log("[TitleUI] Google Button Listener Linked");
            }

            btnStart?.onClick.AddListener(() => ShowPanel(stageSelectPanel));
            btnUpgrade?.onClick.AddListener(() => ShowPanel(upgradePanel));
            btnMinionStore?.onClick.AddListener(() => ShowPanel(minionStorePanel));
            btnSetting?.onClick.AddListener(() => ShowPanel(settingPanel));

            btnStageSelectBack?.onClick.AddListener(BackToMainMenu);
            btnUpgradeBack?.onClick.AddListener(BackToMainMenu);
            btnMinionStoreBack?.onClick.AddListener(BackToMainMenu);
            btnSettingBack?.onClick.AddListener(BackToMainMenu);
        }

        public void OnGuestLoginClick()
        {
            Debug.Log("<color=yellow>[TitleUI]</color> Guest Login Button Clicked!");
            if (GameManager.Instance.Auth != null)
                GameManager.Instance.Auth.LoginAsGuest();
            else
                Debug.LogError("[TitleUI] GameManager.Instance.Auth is NULL!");
        }

        public void OnGoogleLoginClick()
        {
            Debug.Log("<color=yellow>[TitleUI]</color> Google Login Button Clicked!");
            if (GameManager.Instance.Auth != null)
                GameManager.Instance.Auth.LoginWithGoogle();
            else
                Debug.LogError("[TitleUI] GameManager.Instance.Auth is NULL!");
        }

        [ContextMenu("Test Toast - 구글 로그인 성공")]
        private void TestToastGoogle() => ShowToast("구글 로그인 성공!");

        [ContextMenu("Test Toast - 게스트 로그인 성공")]
        private void TestToastGuest() => ShowToast("게스트 로그인 성공!");

        private void PlaySelectSound()
        {
            if (GameManager.Instance?.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn);
        }

        private void ValidateReferences()
        {
            if (authPanel == null)        Debug.LogError("[TitleUI] authPanel is NOT assigned!");
            if (mainButtonPanel == null)  Debug.LogError("[TitleUI] mainButtonPanel is NOT assigned!");
            if (stageSelectPanel == null) Debug.LogError("[TitleUI] stageSelectPanel is NOT assigned!");
            if (upgradePanel == null)     Debug.LogError("[TitleUI] upgradePanel is NOT assigned!");
            if (minionStorePanel == null) Debug.LogError("[TitleUI] minionStorePanel is NOT assigned!");
            if (settingPanel == null)     Debug.LogError("[TitleUI] settingPanel is NOT assigned!");
            if (btnStart == null)         Debug.LogWarning("[TitleUI] btnStart is NOT assigned!");
            if (btnUpgrade == null)       Debug.LogWarning("[TitleUI] btnUpgrade is NOT assigned!");
            if (btnMinionStore == null)   Debug.LogWarning("[TitleUI] btnMinionStore is NOT assigned!");
            if (btnSetting == null)       Debug.LogWarning("[TitleUI] btnSetting is NOT assigned!");
            if (btnGuest == null)         Debug.LogWarning("[TitleUI] btnGuest is NOT assigned! (Auth panel guest button)");
            if (btnGoogle == null)        Debug.LogWarning("[TitleUI] btnGoogle is NOT assigned! (Auth panel google button)");
        }
    }
}
