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
            }
        }
    }
}
