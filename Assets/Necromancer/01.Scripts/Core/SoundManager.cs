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
        public static SoundManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private GameObject sfxSourcePrefab;
        [SerializeField] private int initialSfxPoolSize = 10;

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.6f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        private Queue<AudioSource> sfxPool = new Queue<AudioSource>();
        private List<AudioSource> activeSfx = new List<AudioSource>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                InitPool();
            }
            else
            {
                Destroy(gameObject);
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
            if (clip == null) return;

            AudioSource source = GetSfxSource();
            source.clip = clip;
            source.volume = sfxVolume * masterVolume;
            source.pitch = 1.0f + Random.Range(-pitchVar, pitchVar);
            source.gameObject.SetActive(true);
            source.Play();

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
            yield return new WaitUntil(() => !source.isPlaying);
            source.gameObject.SetActive(false);
            sfxPool.Enqueue(source);
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
    }
}
