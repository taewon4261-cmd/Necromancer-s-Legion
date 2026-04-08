using UnityEngine;
using UnityEngine.UI;
using Necromancer.Core;

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
        public Button mainMenuBtn; // [NEW] 메인으로 가기 버튼

        [Header("Texts")]
        public TMPro.TextMeshProUGUI loginTxt;

        private bool isLoggedIn = false;

        private void Awake()
        {
            if (masterSlider == null) Debug.LogWarning("[SettingUI] masterSlider is NOT assigned!");
            if (bgmSlider == null) Debug.LogWarning("[SettingUI] bgmSlider is NOT assigned!");
            if (sfxSlider == null) Debug.LogWarning("[SettingUI] sfxSlider is NOT assigned!");

            // 버튼 리스너 연결 (인스펙터 할당 방식 권장하나, 코드에서도 안전하게 연결)
            if (loginBtn != null) loginBtn.onClick.AddListener(ToggleLogin);
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

        public void ToggleLogin()
        {
            isLoggedIn = !isLoggedIn;
            if (loginTxt != null)
            {
                loginTxt.text = isLoggedIn ? "로그아웃" : "로그인";
            }
            Debug.Log($"[SettingUI] Login Status: {isLoggedIn}");
        }

        public void OpenPrivacyPolicy()
        {
            // [ARCHITECT] 마스터가 제공한 최종 개인정보 처리방침 링크로 업데이트
            string url = "https://gist.githubusercontent.com/taewon4261-cmd/a4af2e183162369226c3a8cb83245b07/raw/c89eddb98753bc125f8d968a19d299edaa695568/gistfile1.txt"; 
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
