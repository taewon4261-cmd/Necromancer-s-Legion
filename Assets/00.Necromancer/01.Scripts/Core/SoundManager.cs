using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

namespace Necromancer.Core
{
    public enum SfxPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }

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
        [SerializeField] private int maxActiveSfx = 32;

        [Header("Volume Settings")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.6f;
        [Range(0f, 1f)] public float sfxVolume = 0.8f;

        private readonly Queue<AudioSource> sfxPool = new Queue<AudioSource>();
        private readonly List<AudioSource> activeSfx = new List<AudioSource>();
        private readonly Dictionary<AudioSource, SfxPriority> activeSfxPriorities = new Dictionary<AudioSource, SfxPriority>(32);
        private readonly Dictionary<AudioSource, float> activeSfxStartTimes = new Dictionary<AudioSource, float>(32);
        private readonly Dictionary<AudioSource, Coroutine> activeSfxReturnRoutines = new Dictionary<AudioSource, Coroutine>(32);
        private readonly Dictionary<int, float> lastSfxPlayTimes = new Dictionary<int, float>(64);

        private bool isAudioSilenced;
        private int droppedSfxCount;
        private int throttledSfxCount;
        private int createdSfxSourceCount;
        private float nextDiagnosticTime;

        public void Init()
        {
            InitPool();
            LoadVolumesFromData();

            if (bgmSource == null) Debug.LogError("<color=red>[SoundManager]</color> BGM Source가 연결되지 않았습니다! 인스펙터에서 AudioSource를 드래그 앤 드롭 하세요.");
            if (titleBGM == null) Debug.LogWarning("<color=yellow>[SoundManager]</color> Title BGM 클립이 비어있습니다.");
            if (gameBGM == null) Debug.LogWarning("<color=yellow>[SoundManager]</color> Game BGM 클립이 비어있습니다.");

            Debug.Log("<color=cyan>[SoundManager]</color> Initialized by GameManager.");
        }

        private void LoadVolumesFromData()
        {
            if (GameManager.Instance?.SaveData?.Data == null) return;

            masterVolume = GameManager.Instance.SaveData.Data.masterVolume;
            bgmVolume = GameManager.Instance.SaveData.Data.bgmVolume;
            sfxVolume = GameManager.Instance.SaveData.Data.sfxVolume;
        }

        private void InitPool()
        {
            maxActiveSfx = Mathf.Max(1, maxActiveSfx);

            if (sfxSourcePrefab == null)
            {
                GameObject obj = new GameObject("SFX_Source_Template");
                obj.transform.SetParent(transform);
                AudioSource source = obj.AddComponent<AudioSource>();
                source.playOnAwake = false;
                sfxSourcePrefab = obj;
                obj.SetActive(false);
            }

            int poolSize = Mathf.Clamp(initialSfxPoolSize, 1, maxActiveSfx);
            for (int i = 0; i < poolSize; i++)
            {
                sfxPool.Enqueue(CreateNewSfxSource());
            }
        }

        private AudioSource CreateNewSfxSource()
        {
            GameObject obj = Instantiate(sfxSourcePrefab, transform);
            AudioSource source = obj.GetComponent<AudioSource>();
            source.playOnAwake = false;
            obj.SetActive(false);
            createdSfxSourceCount++;
            return source;
        }

        /// <summary>
        /// 기존 호출 호환용. 일반 효과음은 Medium 우선순위와 무제한 간격을 사용합니다.
        /// </summary>
        public void PlaySFX(AudioClip clip, float pitchVar = 0.1f)
        {
            PlaySFX(clip, SfxPriority.Medium, 0f, pitchVar);
        }

        public void PlaySFX(AudioClip clip, SfxPriority priority, float minInterval = 0f, float pitchVar = 0.1f)
        {
            if (isAudioSilenced || clip == null) return;
            if (IsThrottled(clip, priority, minInterval))
            {
                throttledSfxCount++;
                return;
            }

            AudioSource source = GetSfxSource(priority);
            if (source == null)
            {
                droppedSfxCount++;
                return;
            }

            source.clip = clip;
            source.volume = sfxVolume * masterVolume;
            source.pitch = 1.0f + Random.Range(-pitchVar, pitchVar);
            source.gameObject.SetActive(true);
            source.Play();

            if (!activeSfx.Contains(source))
                activeSfx.Add(source);

            activeSfxPriorities[source] = priority;
            activeSfxStartTimes[source] = Time.unscaledTime;
            activeSfxReturnRoutines[source] = StartCoroutine(ReturnToPoolAfterPlay(source));
        }

        private bool IsThrottled(AudioClip clip, SfxPriority priority, float minInterval)
        {
            if (priority == SfxPriority.Critical || minInterval <= 0f) return false;

            int clipId = clip.GetInstanceID();
            float now = Time.unscaledTime;
            if (lastSfxPlayTimes.TryGetValue(clipId, out float lastTime) && now - lastTime < minInterval)
                return true;

            lastSfxPlayTimes[clipId] = now;
            return false;
        }

        private AudioSource GetSfxSource(SfxPriority priority)
        {
            if (sfxPool.Count > 0)
                return sfxPool.Dequeue();

            if (activeSfx.Count < maxActiveSfx)
                return CreateNewSfxSource();

            return TryStealLowerPrioritySource(priority);
        }

        private AudioSource TryStealLowerPrioritySource(SfxPriority requestedPriority)
        {
            AudioSource candidate = null;
            SfxPriority candidatePriority = requestedPriority;
            float oldestStartTime = float.MaxValue;

            for (int i = 0; i < activeSfx.Count; i++)
            {
                AudioSource source = activeSfx[i];
                if (source == null) continue;

                SfxPriority priority = activeSfxPriorities.TryGetValue(source, out var storedPriority)
                    ? storedPriority
                    : SfxPriority.Low;
                if (priority >= requestedPriority) continue;

                float startTime = activeSfxStartTimes.TryGetValue(source, out float storedStartTime)
                    ? storedStartTime
                    : 0f;

                if (candidate == null || priority < candidatePriority || (priority == candidatePriority && startTime < oldestStartTime))
                {
                    candidate = source;
                    candidatePriority = priority;
                    oldestStartTime = startTime;
                }
            }

            if (candidate == null) return null;

            if (activeSfxReturnRoutines.TryGetValue(candidate, out Coroutine routine) && routine != null)
                StopCoroutine(routine);

            candidate.Stop();
            UntrackActiveSource(candidate);
            return candidate;
        }

        private System.Collections.IEnumerator ReturnToPoolAfterPlay(AudioSource source)
        {
            yield return new WaitUntil(() => source == null || !source.isPlaying);
            ReturnSourceToPool(source);
        }

        private void ReturnSourceToPool(AudioSource source)
        {
            if (source == null) return;

            source.gameObject.SetActive(false);
            UntrackActiveSource(source);
            if (!sfxPool.Contains(source))
                sfxPool.Enqueue(source);
        }

        private void UntrackActiveSource(AudioSource source)
        {
            activeSfx.Remove(source);
            activeSfxPriorities.Remove(source);
            activeSfxStartTimes.Remove(source);
            activeSfxReturnRoutines.Remove(source);
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (Time.unscaledTime < nextDiagnosticTime) return;
            nextDiagnosticTime = Time.unscaledTime + 1f;

            if (activeSfx.Count >= maxActiveSfx || droppedSfxCount > 0 || throttledSfxCount > 0 || isAudioSilenced)
            {
                Debug.Log($"[SoundManager] SFX diagnostics: active={activeSfx.Count}, pool={sfxPool.Count}, dropped={droppedSfxCount}, throttled={throttledSfxCount}, created={createdSfxSourceCount}, silenced={isAudioSilenced}");
            }
#endif
        }

        /// <summary>
        /// BGM 볼륨을 설정하고 즉시 반영합니다.
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            bgmVolume = Mathf.Clamp01(volume);
            if (bgmSource != null)
                bgmSource.volume = bgmVolume * masterVolume;
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
                bgmSource.volume = bgmVolume * masterVolume;
        }

        public void PlayBGM(AudioClip clip, bool fade = true)
        {
            if (bgmSource == null)
            {
                Debug.LogError("<color=red>[SoundManager]</color> BGM Source가 없어 재생할 수 없습니다.");
                return;
            }

            if (clip == null)
            {
                Debug.LogWarning("<color=yellow>[SoundManager]</color> 재생하려는 BGM 클립이 NULL입니다. 인스펙터 설정을 확인하세요.");
                return;
            }

            if (bgmSource.clip == clip && bgmSource.isPlaying) return;

            Debug.Log($"<color=lime>[SoundManager]</color> PlayBGM: <b>{clip.name}</b> (Fade: {fade})");
            bgmSource.DOKill();

            if (fade && bgmSource.clip != null && bgmSource.isPlaying)
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

        public void StopBGM(bool fade = true)
        {
            if (bgmSource == null) return;
            Debug.Log($"<color=orange>[SoundManager]</color> StopBGM (Current: {(bgmSource.clip != null ? bgmSource.clip.name : "None")}, Fade: {fade})");
            bgmSource.DOKill();

            if (fade)
            {
                bgmSource.DOFade(0f, 0.5f).OnComplete(() =>
                {
                    bgmSource.Stop();
                    bgmSource.clip = null;
                });
            }
            else
            {
                bgmSource.Stop();
                bgmSource.clip = null;
            }
        }

        /// <summary>
        /// [CLEANUP] 현재 재생 중인 모든 효과음을 즉시 중지하고 정리합니다.
        /// silenceNewSounds가 참이면 ResumeSFX() 전까지 새로운 사운드 재생이 차단됩니다.
        /// </summary>
        public void StopAllSFX(bool silenceNewSounds = true)
        {
            StopAllCoroutines();
            isAudioSilenced = silenceNewSounds;

            for (int i = activeSfx.Count - 1; i >= 0; i--)
            {
                AudioSource source = activeSfx[i];
                if (source == null) continue;

                source.Stop();
                source.gameObject.SetActive(false);
                if (!sfxPool.Contains(source))
                    sfxPool.Enqueue(source);
            }

            activeSfx.Clear();
            activeSfxPriorities.Clear();
            activeSfxStartTimes.Clear();
            activeSfxReturnRoutines.Clear();

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
    }
}
