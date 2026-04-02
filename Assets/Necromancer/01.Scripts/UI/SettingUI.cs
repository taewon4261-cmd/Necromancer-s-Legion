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
        }

        private void Start()
        {
            LoadSettings();
        }

        private void LoadSettings()
        {
            if (SaveDataManager.Instance == null || SaveDataManager.Instance.Data == null) return;
            if (SoundManager.Instance == null) return;

            var data = SaveDataManager.Instance.Data;

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
            if (SaveDataManager.Instance != null && SoundManager.Instance != null)
            {
                SaveDataManager.Instance.Data.masterVolume = value;
                SoundManager.Instance.SetMasterVolume(value);
            }
        }

        private void OnBGMChanged(float value)
        {
            if (SaveDataManager.Instance != null && SoundManager.Instance != null)
            {
                SaveDataManager.Instance.Data.bgmVolume = value;
                SoundManager.Instance.SetBGMVolume(value);
            }
        }

        private void OnSFXChanged(float value)
        {
            if (SaveDataManager.Instance != null && SoundManager.Instance != null)
            {
                SaveDataManager.Instance.Data.sfxVolume = value;
                SoundManager.Instance.SetSFXVolume(value);
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
            // 사용자가 링크를 넣을 수 있도록 가이드 제공
            string url = "https://your-privacy-policy-link.com"; 
            Application.OpenURL(url);
            Debug.Log($"[SettingUI] Open URL: {url}");
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
            if (SaveDataManager.Instance != null)
            {
                SaveDataManager.Instance.Save();
            }
            gameObject.SetActive(false); // 패널 닫기
        }
    }
}
