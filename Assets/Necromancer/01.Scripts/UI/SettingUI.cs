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
        public Slider bgmSlider;
        public Slider sfxSlider;

        private void Awake()
        {
            // 자동 바인딩
            if (bgmSlider == null) bgmSlider = transform.Find("Slider_BGM")?.GetComponent<Slider>();
            if (sfxSlider == null) sfxSlider = transform.Find("Slider_SFX")?.GetComponent<Slider>();
            
            // 만약 못 찾았다면 이름으로 다시 한 번 시도 (계층 구조 유연성)
            if (bgmSlider == null) bgmSlider = GetComponentInChildren<Slider>(true);
            if (sfxSlider == null) sfxSlider = GetComponentsInChildren<Slider>(true).Length > 1 ? GetComponentsInChildren<Slider>(true)[1] : sfxSlider;
        }

        private void Start()
        {
            LoadSettings();
        }

        private void OnEnable()
        {
            // LoadSettings(); // Moved to Start()
        }

        private void LoadSettings()
        {
            if (SoundManager.Instance == null) return;

            // 저장된 값 로드 (0~1 사이값)
            float bgmVal = PlayerPrefs.GetFloat("Vol_BGM", SoundManager.Instance.bgmVolume);
            float sfxVal = PlayerPrefs.GetFloat("Vol_SFX", SoundManager.Instance.sfxVolume);

            bgmSlider.value = bgmVal;
            sfxSlider.value = sfxVal;

            bgmSlider.onValueChanged.RemoveAllListeners();
            sfxSlider.onValueChanged.RemoveAllListeners();

            bgmSlider.onValueChanged.AddListener(OnBGMChanged);
            sfxSlider.onValueChanged.AddListener(OnSFXChanged);
        }

        private void OnBGMChanged(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetBGMVolume(value);
                PlayerPrefs.SetFloat("Vol_BGM", value);
            }
        }

        private void OnSFXChanged(float value)
        {
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.SetSFXVolume(value);
                PlayerPrefs.SetFloat("Vol_SFX", value);
            }
        }

        /// <summary>
        /// 설정 패널을 닫을 때 저장합니다.
        /// </summary>
        public void CloseAndSave()
        {
            PlayerPrefs.Save();
        }
    }
}
