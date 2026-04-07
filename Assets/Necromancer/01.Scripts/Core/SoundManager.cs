using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Necromancer.Core
{
    /// <summary>
    /// 게임 전체의 사운드(BGM, SFX)를 총괄하며, 오디오 소스 풀링을 지원합니다.
    /// </summary>
    public class SoundManager : MonoBehaviour
    {
        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [Header("Audio Clips")]
        [SerializeField] public AudioClip titleBGM;
        [SerializeField] public AudioClip gameBGM;

        [Header("SFX Clips")]
        public AudioClip sfxBow;
        public AudioClip sfxCreateMinion;
        public AudioClip sfxFailBtn;
        public AudioClip sfxLose;
        public AudioClip sfxNormalAttackCraw;
        public AudioClip sfxPlayerAttack;
        public AudioClip sfxSelectBtn;
        public AudioClip sfxSoulGain;
        public AudioClip sfxUpgrade;
        public AudioClip sfxWin;

        [SerializeField] private GameObject sfxSourcePrefab;
        [SerializeField] private int initialSfxPoolSize = 10;

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.6f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
        private List<AudioSource> activeSfx = new List<AudioSource>();

        public void Init()
        {
            InitPool();
            LoadVolumesFromData();
            Debug.Log("<color=cyan>[SoundManager]</color> Initialized by GameManager.");
        }

        private void LoadVolumesFromData()
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null)
            {
                masterVolume = GameManager.Instance.SaveData.Data.masterVolume;
                bgmVolume = GameManager.Instance.SaveData.Data.bgmVolume;
                sfxVolume = GameManager.Instance.SaveData.Data.sfxVolume;
            }
        }

        private void InitPool()
        {
            if (sfxSourcePrefab == null)
            {
                GameObject obj = new GameObject("SFX_Source_Template");
                obj.transform.SetParent(transform);
                AudioSource source = obj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxSourcePrefab = obj;
                obj.SetActive(false);
            }

            for (int i = 0; i < initialSfxPoolSize; i++)
            {
                CreateNewSfxSource();
            }
        }

        private AudioSource CreateNewSfxSource()
        {
            GameObject obj = Instantiate(sfxSourcePrefab, transform);
            AudioSource source = obj.GetComponent<AudioSource>();
            obj.SetActive(false);
            sfxPool.Enqueue(source);
            return source;
        }

        /// <summary>
        /// 특정 사운드 시냅스를 재생합니다.
        /// </summary>
        public void PlaySFX(AudioClip clip, float pitchVar = 0.1f)
        {
            if (isAudioSilenced || clip == null) return; // [STABILITY] 사운드 셧다운 상태면 재생 거부

            AudioSource source = GetSfxSource();
            if (source == null) return;

            source.clip = clip;
            source.volume = sfxVolume * masterVolume;
            source.pitch = 1.0f + Random.Range(-pitchVar, pitchVar);
            source.gameObject.SetActive(true);
            source.Play();

            if (activeSfx != null && !activeSfx.Contains(source)) 
            {
                activeSfx.Add(source);
            }

            StartCoroutine(ReturnToPoolAfterPlay(source));
        }

        private AudioSource GetSfxSource()
        {
            if (sfxPool.Count > 0)
            {
                return sfxPool.Dequeue();
            }
            else
            {
                return CreateNewSfxSource();
            }
        }

        private System.Collections.IEnumerator ReturnToPoolAfterPlay(AudioSource source)
        {
            // [STABILITY] 소스 파괴 여부도 함께 체크
            yield return new WaitUntil(() => source == null || !source.isPlaying);
            
            if (source != null && !sfxPool.Contains(source))
            {
                source.gameObject.SetActive(false);
                if (activeSfx.Contains(source)) activeSfx.Remove(source);
                sfxPool.Enqueue(source);
            }
        }

        /// <summary>
        /// BGM 볼륨을 설정하고 즉시 반영합니다.
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume * masterVolume;
            }
        }

        /// <summary>
        /// SFX 볼륨을 설정합니다.
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            sfxVolume = Mathf.Clamp01(volume);
        }

        /// <summary>
        /// 마스터 볼륨을 설정하고 모든 오디오 소스에 반영합니다.
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            masterVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
            {
                bgmSource.volume = bgmVolume * masterVolume;
            }
            // SFX는 재생 시점에 masterVolume을 곱하므로 별도의 루프가 필요하지 않으나, 
            // 현재 재생 중인 SFX가 있다면 업데이트 로직을 추가할 수 있습니다.
        }

        public void PlayBGM(AudioClip clip, bool fade = true)
        {
            if (bgmSource.clip == clip) return;

            if (fade && bgmSource.clip != null)
            {
                bgmSource.DOFade(0f, 0.5f).OnComplete(() =>
                {
                    bgmSource.clip = clip;
                    bgmSource.volume = 0f;
                    bgmSource.Play();
                    bgmSource.DOFade(bgmVolume * masterVolume, 0.5f);
                });
            }
            else
            {
                bgmSource.clip = clip;
                bgmSource.volume = bgmVolume * masterVolume;
                bgmSource.Play();
            }
        }

        public void StopBGM()
        {
            bgmSource.DOFade(0f, 0.5f).OnComplete(() => bgmSource.Stop());
        }

        /// <summary>
        /// [CLEANUP] 현재 재생 중인 모든 효과음을 즉시 중지하고 정리합니다.
        /// 스테이지 탈출이나 씬 전환 시 호출됩니다.
        /// </summary>
        /// <summary>
        /// [CLEANUP] 현재 재생 중인 모든 효과음을 즉시 중지하고 정리합니다.
        /// silenceNewSounds가 참이면 ResumeSFX() 전까지 새로운 사운드 재생이 차단됩니다.
        /// </summary>
        /// <summary>
        /// [CLEANUP] 현재 재생 중인 모든 효과음을 즉시 중지하고 정리합니다.
        /// silenceNewSounds가 참이면 ResumeSFX() 전까지 새로운 사운드 재생이 차단됩니다.
        /// </summary>
        public void StopAllSFX(bool silenceNewSounds = true)
        {
            StopAllCoroutines(); 
            isAudioSilenced = silenceNewSounds;

            if (activeSfx != null)
            {
                foreach (var source in activeSfx)
                {
                    if (source != null)
                    {
                        source.Stop();
                        source.gameObject.SetActive(false);
                        if (!sfxPool.Contains(source))
                        {
                            sfxPool.Enqueue(source);
                        }
                    }
                }
                activeSfx.Clear();
            }
            
            Debug.Log($"<color=orange>[SoundManager]</color> All SFX Stopped. Silenced: {isAudioSilenced}");
        }

        /// <summary>
        /// [LIFECYCLE] 씬 로드 완료 시 사운드 재생 잠금을 해제합니다.
        /// </summary>
        public void ResumeSFX()
        {
            isAudioSilenced = false;
            Debug.Log("<color=green>[SoundManager]</color> SFX Playback Resumed.");
        }


    

        private bool isAudioSilenced = false; // [STABILITY] 씬 전환 중 새로운 사운드 재생 방지 플래그
}
}
