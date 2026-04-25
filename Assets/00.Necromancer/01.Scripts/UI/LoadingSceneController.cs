using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using Necromancer.Systems;

namespace Necromancer.UI
{
    /// <summary>
    /// LoadingScene에서 Addressables 다운로드 흐름을 제어하는 UI 컨트롤러입니다.
    /// 확인 → 다운로드 → TitleScene 진입 순서로 동작합니다.
    /// </summary>
    public class LoadingSceneController : MonoBehaviour
    {
        [Header("항상 표시")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider           progressBar;
        [SerializeField] private TextMeshProUGUI  progressPercentText;

        [Header("다운로드 확인 팝업")]
        [SerializeField] private GameObject       confirmPanel;
        [SerializeField] private TextMeshProUGUI  downloadSizeText;
        [SerializeField] private Button           confirmButton;
        [SerializeField] private Button           skipButton;    // 나중에 / Wi-Fi만 허용 등

        [Header("다운로드 진행 패널")]
        [SerializeField] private GameObject       downloadPanel;

        private const string NEXT_SCENE   = "TitleScene";
        private const int    MAX_RETRIES  = 3;

        private void Start()
        {
            confirmPanel.SetActive(false);
            downloadPanel.SetActive(false);
            RunFlow().Forget();
        }

        // ─── 메인 흐름 ──────────────────────────────────────────────────────────

        private async UniTaskVoid RunFlow()
        {
            SetStatus("서버 연결 중...");
            SetProgress(0f);

            if (!TryGetDownloadManager(out var dm))
            {
                // GameManager가 없는 환경(직접 LoadingScene 실행 등)이면 바로 진행
                SetStatus("초기화 중...");
                await UniTask.Delay(500);
                GoToTitle();
                return;
            }

            // 1. 카탈로그 업데이트
            bool catalogOk = await dm.UpdateCatalogsAsync();
            if (!catalogOk)
            {
                SetStatus("서버 연결 실패 - 오프라인으로 시작합니다.");
                await UniTask.Delay(1500);
                GoToTitle();
                return;
            }

            // 2. 다운로드 필요 용량 확인
            SetStatus("업데이트 확인 중...");
            long sizeBytes = await dm.GetDownloadSizeAsync();

            if (sizeBytes <= 0)
            {
                // 추가 다운로드 불필요
                SetStatus("최신 버전입니다.");
                SetProgress(1f);
                await UniTask.Delay(500);
                GoToTitle();
                return;
            }

            // 3. 사용자에게 다운로드 확인 팝업
            bool userConfirmed = await ShowConfirmPanel(sizeBytes);
            if (!userConfirmed)
            {
                GoToTitle();
                return;
            }

            // 4. 다운로드 실행 (최대 MAX_RETRIES 회 재시도)
            await RunDownloadWithRetry(dm);
        }

        // ─── 확인 팝업 ──────────────────────────────────────────────────────────

        private async UniTask<bool> ShowConfirmPanel(long bytes)
        {
            float mb = bytes / (1024f * 1024f);
            downloadSizeText.text = $"추가 데이터 다운로드가 필요합니다.\n({mb:F1} MB)";

            confirmPanel.SetActive(true);

            bool decided   = false;
            bool confirmed = false;

            void OnConfirm() { confirmed = true;  decided = true; }
            void OnSkip()    { confirmed = false; decided = true; }

            confirmButton.onClick.AddListener(OnConfirm);
            skipButton.onClick.AddListener(OnSkip);

            await UniTask.WaitUntil(() => decided);

            confirmButton.onClick.RemoveListener(OnConfirm);
            skipButton.onClick.RemoveListener(OnSkip);
            confirmPanel.SetActive(false);

            return confirmed;
        }

        // ─── 다운로드 (재시도 포함) ────────────────────────────────────────────

        private async UniTask RunDownloadWithRetry(DownloadManager dm)
        {
            downloadPanel.SetActive(true);

            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                SetStatus(attempt == 1 ? "다운로드 중..." : $"다운로드 중... (재시도 {attempt}/{MAX_RETRIES})");
                SetProgress(0f);

                bool success = await dm.DownloadAsync(progress =>
                {
                    SetProgress(progress);
                    if (progressPercentText != null)
                        progressPercentText.text = $"{progress * 100f:F0}%";
                });

                if (success)
                {
                    SetStatus("완료!");
                    await UniTask.Delay(500);
                    GoToTitle();
                    return;
                }

                if (attempt < MAX_RETRIES)
                {
                    SetStatus($"다운로드 실패. {3}초 후 재시도합니다...");
                    await UniTask.Delay(3000);
                }
            }

            // 모든 재시도 실패 → 오프라인으로 진입
            SetStatus("다운로드 실패. 오프라인으로 시작합니다.");
            await UniTask.Delay(1500);
            GoToTitle();
        }

        // ─── 헬퍼 ──────────────────────────────────────────────────────────────

        private bool TryGetDownloadManager(out DownloadManager dm)
        {
            dm = null;
            if (GameManager.Instance == null) return false;
            dm = GameManager.Instance.Download;
            return dm != null;
        }

        private void SetProgress(float value)
        {
            if (progressBar != null)
                progressBar.value = Mathf.Clamp01(value);
        }

        private void SetStatus(string msg)
        {
            if (statusText != null)
                statusText.text = msg;
            Debug.Log($"[LoadingScene] {msg}");
        }

        private void GoToTitle()
        {
            if (SceneTransitionManager.Instance != null)
                SceneTransitionManager.Instance.ChangeScene(NEXT_SCENE);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(NEXT_SCENE);
        }
    }
}
