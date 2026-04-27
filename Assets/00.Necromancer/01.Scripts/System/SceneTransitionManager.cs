using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

namespace Necromancer.Systems
{
    /// <summary>
    /// 씬 전환 시 페이드 인/아웃 효과를 담당하고 데이터를 관리합니다.
    /// DontDestroyOnLoad를 사용하여 씬이 바뀌어도 끊김없는 연출을 제공합니다.
    /// </summary>
    public class SceneTransitionManager : MonoBehaviour
    {
        public static SceneTransitionManager Instance { get; private set; }

        [Header("Transition UI")]
        [SerializeField] private CanvasGroup _fadeCanvasGroup;
        
        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Start()
        {
            // 시작 시 페이드 인 (밝아짐)
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.alpha = 1f;
                _fadeCanvasGroup.DOFade(0f, UIConstants.DefaultFadeDuration).SetUpdate(true);
            }
        }

        /// <summary>
        /// 다른 씬으로 페이드 아웃 후 이동합니다.
        /// </summary>
        /// <param name="sceneName">이동할 씬 이름</param>
        public void ChangeScene(string sceneName)
        {
            StartCoroutine(TransitionRoutine(sceneName));
        }

        private IEnumerator TransitionRoutine(string sceneName)
        {
            // [AUDIO] 씬 전환 시작 시 사운드 정리 (BGM 페이드 아웃 및 SFX 중단)
            if (GameManager.Instance != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.Sound.StopAllSFX(true);
                GameManager.Instance.Sound.StopBGM(true);
            }

            // 1. 페이드 아웃 (어두워짐)
            if (_fadeCanvasGroup != null)
            {
                _fadeCanvasGroup.blocksRaycasts = true; // 전환 중 클릭 방지
                yield return _fadeCanvasGroup.DOFade(1f, UIConstants.DefaultFadeDuration).SetUpdate(true).WaitForCompletion();
            }

            // 2. 실제 씬 로드
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            while (!op.isDone)
            {
                yield return null;
            }

            // 3. 페이드 인 (밝아짐)
            if (_fadeCanvasGroup != null)
            {
                yield return _fadeCanvasGroup.DOFade(0f, UIConstants.DefaultFadeDuration).SetUpdate(true).WaitForCompletion();
                _fadeCanvasGroup.blocksRaycasts = false;

                // [FIX] 페이드 인 완료 후 사운드 잠금 해제 (새로운 씬의 효과음 재생 허용)
                if (GameManager.Instance != null && GameManager.Instance.Sound != null)
                {
                    GameManager.Instance.Sound.ResumeSFX();
                }
            }

            // [AUDIO] 씬 완전 진입 후 SFX 재개 — StopAllSFX(true)로 걸린 무음 잠금 해제
            if (GameManager.Instance?.Sound != null)
                GameManager.Instance.Sound.ResumeSFX();
        }
    }
}
