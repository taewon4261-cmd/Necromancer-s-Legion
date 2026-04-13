using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Necromancer.Core;
using Necromancer.Systems;
using Necromancer;

namespace Necromancer.UI
{
    /// <summary>
    /// 게임 설정을 제어하는 드라이브 패널입니다.
    /// </summary>
    public class SettingUI : MonoBehaviour
    {
        [Header("Volume Sliders")]
        public Slider masterSlider;
        public Slider bgmSlider;
        public Slider sfxSlider;

        [Header("Buttons")]
        public Button loginBtn;
        public Button privacyBtn;
        public Button backBtn;
        public Button quitBtn;
        public Button mainMenuBtn;

        [Header("Texts")]
        public TMPro.TextMeshProUGUI loginTxt;

        [Header("Toast Notification")]
        [SerializeField] private CanvasGroup toastCanvasGroup;
        [SerializeField] private TMPro.TextMeshProUGUI toastText;

        private void Awake()
        {
            if (masterSlider == null) Debug.LogWarning("[SettingUI] masterSlider is NOT assigned!");
            if (bgmSlider == null) Debug.LogWarning("[SettingUI] bgmSlider is NOT assigned!");
            if (sfxSlider == null) Debug.LogWarning("[SettingUI] sfxSlider is NOT assigned!");

            if (loginBtn != null) loginBtn.onClick.AddListener(OnClick_Login);
            if (privacyBtn != null) privacyBtn.onClick.AddListener(OpenPrivacyPolicy);
            if (backBtn != null) backBtn.onClick.AddListener(CloseAndSave);
            if (quitBtn != null) quitBtn.onClick.AddListener(QuitGame);
            if (mainMenuBtn != null) mainMenuBtn.onClick.AddListener(OnClick_MainMenu);
        }

        private void OnEnable()
        {
            // [UI-CONTEXT] 인게임 씬에서만 '메인으로' 버튼 노출
            if (mainMenuBtn != null)
            {
                bool isGameScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == "GameScene";
                mainMenuBtn.gameObject.SetActive(isGameScene);
            }

            // [AUTH] 패널이 열릴 때마다 현재 로그인 상태를 반영
            RefreshLoginButton();
            AuthManager.OnAuthStateChanged += OnAuthStateChanged;
            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.OnLoginResult += OnLoginResult;
        }

        private void OnDisable()
        {
            AuthManager.OnAuthStateChanged -= OnAuthStateChanged;
            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.OnLoginResult -= OnLoginResult;
        }

        private void OnLoginResult(bool success, string uid)
        {
            if (!success) return;
            var state = GameManager.Instance?.Auth?.CurrentState;
            if (state == AuthState.LoggedIn)
                ShowToast("구글 로그인 성공!");
            else if (state == AuthState.Guest)
                ShowToast("게스트 로그인 성공!");
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

            float t = 0f;
            while (t < 0.3f) { t += Time.unscaledDeltaTime; toastCanvasGroup.alpha = Mathf.Clamp01(t / 0.3f); yield return null; }
            toastCanvasGroup.alpha = 1f;

            yield return new WaitForSecondsRealtime(2f);

            t = 0f;
            while (t < 0.5f) { t += Time.unscaledDeltaTime; toastCanvasGroup.alpha = 1f - Mathf.Clamp01(t / 0.5f); yield return null; }
            toastCanvasGroup.gameObject.SetActive(false);
        }

        [ContextMenu("Test Toast - 구글 로그인 성공")]
        private void TestToastGoogle() => ShowToast("구글 로그인 성공!");

        [ContextMenu("Test Toast - 게스트 로그인 성공")]
        private void TestToastGuest() => ShowToast("게스트 로그인 성공!");

        /// <summary>
        /// [AUTH] 인증 상태 변경 시 로그인 버튼 텍스트/상호작용 갱신
        /// </summary>
        private void OnAuthStateChanged(AuthState state) => RefreshLoginButton();

        private void RefreshLoginButton()
        {
            if (loginBtn == null || loginTxt == null) return;
            if (GameManager.Instance == null || GameManager.Instance.Auth == null) return;

            var auth = GameManager.Instance.Auth;
            bool isGoogle = (auth.CurrentState == AuthState.LoggedIn);
            bool isGuest = (auth.CurrentState == AuthState.Guest);

            // [UI-FEEDBACK] 연동 상태에 따른 텍스트 및 색상 피드백 강화
            if (isGoogle)
            {
                loginTxt.text = "다른 구글 계정으로 변경";
                loginTxt.color = Color.white;
            }
            else if (isGuest)
            {
                loginTxt.text = "구글 계정으로 연동 (데이터 보존)";
                loginTxt.color = new Color(1f, 0.6f, 0f); // 주황색
            }
            else
            {
                loginTxt.text = "구글 로그인";
                loginTxt.color = new Color(1f, 0.6f, 0f);
            }

            loginBtn.interactable = true; 
        }

        private void Start()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null || GameManager.Instance.SaveData.Data == null) return;
            if (GameManager.Instance.Sound == null) return;

            var data = GameManager.Instance.SaveData.Data;

            if (masterSlider != null) masterSlider.value = data.masterVolume;
            if (bgmSlider != null) bgmSlider.value = data.bgmVolume;
            if (sfxSlider != null) sfxSlider.value = data.sfxVolume;

            masterSlider?.onValueChanged.RemoveAllListeners();
            bgmSlider?.onValueChanged.RemoveAllListeners();
            sfxSlider?.onValueChanged.RemoveAllListeners();

            masterSlider?.onValueChanged.AddListener(OnMasterChanged);
            bgmSlider?.onValueChanged.AddListener(OnBGMChanged);
            sfxSlider?.onValueChanged.AddListener(OnSFXChanged);
        }

        private void OnMasterChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.masterVolume = value;
                GameManager.Instance.Sound.SetMasterVolume(value);
            }
        }

        private void OnBGMChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.bgmVolume = value;
                GameManager.Instance.Sound.SetBGMVolume(value);
            }
        }

        private void OnSFXChanged(float value)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.SaveData.Data.sfxVolume = value;
                GameManager.Instance.Sound.SetSFXVolume(value);
            }
        }

        private void OnClick_Login()
        {
            if (GameManager.Instance == null || GameManager.Instance.Auth == null) return;
            
            var auth = GameManager.Instance.Auth;
            
            // [AUTH] 이미 구글 로그인 중이면 계정 변경, 아니면 연동 시도
            if (auth.CurrentState == AuthState.LoggedIn)
            {
                auth.SwitchAccount();
            }
            else
            {
                auth.LinkAccount();
            }
        }

        public void OpenPrivacyPolicy()
        {
            // [ARCHITECT] 마스터가 제공한 최종 개인정보 처리방침 링크로 업데이트
            string url = "https://gist.github.com/taewon4261-cmd/a4af2e183162369226c3a8cb83245b07"; 
            Application.OpenURL(url);
            Debug.Log($"<color=cyan>[SettingUI]</color> Redirecting to Privacy Policy: {url}");
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void CloseAndSave()
        {
            // Settings 사유 해소 (다른 정지 사유가 있으면 timeScale은 0으로 유지됨)
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetPause(Necromancer.PauseSource.Settings, false);
            }

            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                GameManager.Instance.SaveData.Save();
            }

            gameObject.SetActive(false); 
        }

        public void OnClick_MainMenu()
        {
            // [DATA-SAFETY] 나가기 전 현재까지 얻은 소울 강제 커밋 & 저장
            if (GameManager.Instance != null && GameManager.Instance.Resources != null)
            {
                GameManager.Instance.Resources.CommitSessionSoul();
            }

            // DOTween 등 모든 트윈 정지 및 씬 이동
            DG.Tweening.DOTween.KillAll();
            Time.timeScale = 1f;
            UnityEngine.SceneManagement.SceneManager.LoadScene("TitleScene");
            Debug.Log("<color=green>[SettingUI]</color> Intermediate Save and Redirecting to TitleScene.");
        }
    }
}
