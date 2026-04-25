using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Cysharp.Threading.Tasks;
using Necromancer.Systems;

namespace Necromancer.UI
{
    /// <summary>
    /// DownloadScene 전체 흐름을 관장합니다.
    /// 순서: Firebase 초기화 대기 → 로그인 → 패치 확인 → 다운로드 → TitleScene 이동
    /// </summary>
    public class DownloadSceneController : MonoBehaviour
    {
        [Header("로그인 패널")]
        [SerializeField] private GameObject      authPanel;
        [SerializeField] private Button          btnGoogle;
        [SerializeField] private Button          btnGuest;

        [Header("다운로드 상태 표시")]
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Slider          progressBar;
        [SerializeField] private TextMeshProUGUI progressDetailText;   // "50 MB / 100 MB"

        [Header("NoticePopup - 패치 용량 안내")]
        [SerializeField] private GameObject      noticePopup;
        [SerializeField] private TextMeshProUGUI noticeSizeText;
        [SerializeField] private Button          downloadButton;
        [SerializeField] private Button          skipButton;

        [Header("ErrorPopup - 네트워크 오류")]
        [SerializeField] private GameObject      errorPopup;
        [SerializeField] private TextMeshProUGUI errorMessageText;
        [SerializeField] private Button          retryButton;
        [SerializeField] private Button          quitButton;

        private const string NEXT_SCENE  = "TitleScene";
        private const int    MAX_RETRIES = 3;

        // 로그인 결과를 비동기 흐름에 전달하는 완료 소스
        // TrySetResult를 사용하므로 중복 호출되어도 예외 없음
        private UniTaskCompletionSource<bool> _loginTcs;

        // ─── 이벤트 구독/해제 ────────────────────────────────────────────────────

        private void OnEnable()
        {
            AuthManager.OnFirebaseReady += HandleFirebaseReady;

            var auth = GameManager.Instance?.Auth;
            if (auth != null)
                auth.OnLoginResult += HandleLoginResult;
        }

        private void OnDisable()
        {
            AuthManager.OnFirebaseReady -= HandleFirebaseReady;

            var auth = GameManager.Instance?.Auth;
            if (auth != null)
                auth.OnLoginResult -= HandleLoginResult;
        }

        // ─── 초기화 ──────────────────────────────────────────────────────────────

        private void Start()
        {
            // 모든 패널 숨김 → RunFlow가 상황에 맞게 켬
            authPanel.SetActive(false);
            noticePopup.SetActive(false);
            errorPopup.SetActive(false);
            progressBar.value = 0f;

            // Firebase 준비 완료 전까지 로그인 버튼 잠금
            // [크래쉬 방지] 버튼 잠금은 UI 레이어에서 직접 처리 — AuthManager의 플래그보다 먼저 막음
            SetLoginButtons(false);

            RunFlow(this.GetCancellationTokenOnDestroy()).Forget();
        }

        // ─── 메인 흐름 ──────────────────────────────────────────────────────────

        private async UniTaskVoid RunFlow(System.Threading.CancellationToken ct)
        {
            try
            {
                // ── 1단계: 다운로드 먼저 (로그인 패널 숨김) ─────────────────────────
                authPanel.SetActive(false);
                await RunDownloadFlow(ct);

                // ── 2단계: Firebase 초기화 대기 ──────────────────────────────────────
                SetStatus("Firebase 초기화 중...");

                var auth = GameManager.Instance?.Auth;
                if (auth != null && !auth.IsFirebaseReady && auth.CurrentState != AuthState.Failed)
                {
                    // [DEADLOCK FIX] Failed 상태도 탈출 조건에 포함 — Firebase 초기화 실패 시 무한 대기 방지
                    await UniTask.WaitUntil(() =>
                        GameManager.Instance?.Auth?.IsFirebaseReady == true ||
                        GameManager.Instance?.Auth?.CurrentState == AuthState.Failed,
                        cancellationToken: ct);
                }

                // ── 3단계: 로그인 (다운로드 완료 후) ────────────────────────────────
                bool loginSuccess = await HandleLoginPhase(ct);

                if (!loginSuccess)
                    Debug.LogWarning("[DownloadScene] 로그인 실패. 오프라인으로 진행합니다.");

                // ── 4단계: TitleScene 이동 ────────────────────────────────────────────
                GoToTitle();
            }
            catch (OperationCanceledException)
            {
                // 씬 파괴 등 정상 취소 — 무시
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DownloadScene] RunFlow 예외 발생: {ex}");
                bool wantsRetry = await ShowError($"오류가 발생했습니다.\n네트워크 상태를 확인해주세요.", ct);
                if (wantsRetry)
                    RunFlow(ct).Forget();
            }
        }

        // ─── 로그인 단계 ─────────────────────────────────────────────────────────

        private async UniTask<bool> HandleLoginPhase(System.Threading.CancellationToken ct)
        {
            var auth = GameManager.Instance?.Auth;

            // 이미 로그인된 상태면 즉시 통과 (자동 로그인 성공)
            if (auth != null &&
                (auth.CurrentState == AuthState.LoggedIn || auth.CurrentState == AuthState.Guest))
            {
                Debug.Log("[DownloadScene] 이미 로그인 완료 상태. 로그인 단계 생략.");
                return true;
            }

            // 이전 로그인 기록이 있으면 자동 로그인 진행 중 → 패널 숨기고 대기
            bool hasRecord = HasPreviousLoginRecord();
            if (hasRecord)
            {
                SetStatus("자동 로그인 중...");
                authPanel.SetActive(false);

                bool autoResult = await WaitForLoginResultAsync(ct);

                // 자동 로그인 실패 → 수동 로그인 패널로 전환
                if (!autoResult)
                {
                    SetStatus("자동 로그인 실패. 직접 로그인해주세요.");
                    return await ShowLoginPanelAndWait(ct);
                }
                return true;
            }

            // 로그인 기록 없음 → 수동 로그인 패널 표시
            return await ShowLoginPanelAndWait(ct);
        }

        private async UniTask<bool> ShowLoginPanelAndWait(System.Threading.CancellationToken ct)
        {
            authPanel.SetActive(true);
            SetLoginButtons(true);
            SetStatus("로그인해주세요.");
            return await WaitForLoginResultAsync(ct);
        }

        // UniTaskCompletionSource로 OnLoginResult 이벤트를 비동기 대기
        private UniTask<bool> WaitForLoginResultAsync(System.Threading.CancellationToken ct)
        {
            _loginTcs = new UniTaskCompletionSource<bool>();
            return _loginTcs.Task.AttachExternalCancellation(ct);
        }

        // AuthManager.OnLoginResult 콜백 → TCS에 결과 전달
        private void HandleLoginResult(bool success, string uid)
        {
            if (!success)
            {
                // [크래쉬 방지] 실패 시 버튼 즉시 재활성화
                SetLoginButtons(true);
                SetStatus("로그인에 실패했습니다. 다시 시도해주세요.");
            }

            // TrySetResult: 이미 결과가 설정됐거나 null이면 무시 (중복 호출 안전)
            _loginTcs?.TrySetResult(success);
            _loginTcs = null;
        }

        private void HandleFirebaseReady()
        {
            // 수동 로그인 대기 중일 때만 버튼 활성화
            // (자동 로그인 대기 중엔 패널이 숨겨져 있으므로 무해)
            SetLoginButtons(true);
            SetStatus("로그인해주세요.");
            Debug.Log("<color=cyan>[DownloadScene]</color> Firebase ready. Login buttons enabled.");
        }

        // ─── 로그인 버튼 (Inspector의 OnClick()에 연결) ──────────────────────────

        /// <summary>구글 로그인 버튼 클릭. Inspector OnClick()에 연결하세요.</summary>
        public void OnClickGoogle()
        {
            // [크래쉬 방지] 클릭 즉시 양쪽 버튼 모두 잠금 — AuthManager 플래그보다 먼저 막음
            SetLoginButtons(false);
            SetStatus("구글 로그인 중...");
            Debug.Log("<color=yellow>[DownloadScene]</color> Google Login Clicked.");

            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.LoginWithGoogle();
            else
                Debug.LogError("[DownloadScene] Auth is NULL!");
        }

        /// <summary>게스트 로그인 버튼 클릭. Inspector OnClick()에 연결하세요.</summary>
        public void OnClickGuest()
        {
            // [크래쉬 방지] 클릭 즉시 양쪽 버튼 모두 잠금
            SetLoginButtons(false);
            SetStatus("게스트 로그인 중...");
            Debug.Log("<color=yellow>[DownloadScene]</color> Guest Login Clicked.");

            if (GameManager.Instance?.Auth != null)
                GameManager.Instance.Auth.LoginAsGuest();
            else
                Debug.LogError("[DownloadScene] Auth is NULL!");
        }

        // ─── 다운로드 단계 ───────────────────────────────────────────────────────

        private async UniTask RunDownloadFlow(System.Threading.CancellationToken ct)
        {
            if (!TryGetDownloadManager(out var dm))
            {
                SetStatus("리소스 서버 연결 생략.");
                await UniTask.Delay(800, cancellationToken: ct);
                return; // 로그인 단계로 진행
            }

            // 카탈로그 업데이트
            SetStatus("서버 연결 중...");
            bool catalogOk = await dm.UpdateCatalogsAsync();
            if (!catalogOk)
            {
                SetStatus("서버 연결 실패. 캐시 데이터로 진행합니다.");
                await UniTask.Delay(1500, cancellationToken: ct);
                return; // 오프라인이어도 로그인은 진행
            }

            // 다운로드 용량 확인
            SetStatus("업데이트 확인 중...");
            long totalBytes = await dm.GetDownloadSizeAsync();

            if (totalBytes <= 0)
            {
                // 0: 최신 / -1: 오류 → 둘 다 로그인 단계로 진행
                SetStatus(totalBytes == 0 ? "최신 버전입니다." : "업데이트 확인 실패.");
                SetProgress(1f);
                await UniTask.Delay(500, cancellationToken: ct);
                return;
            }

            // 패치 안내 팝업 (다운로드 또는 나중에)
            bool confirmed = await ShowNoticePopup(totalBytes, ct);
            if (!confirmed)
                return; // "나중에" 선택 → 로그인 단계로 진행

            // 다운로드 실행
            await RunDownloadWithRetry(dm, totalBytes, ct);
        }

        private async UniTask<bool> ShowNoticePopup(long bytes, System.Threading.CancellationToken ct)
        {
            float mb = bytes / (1024f * 1024f);
            noticeSizeText.text = $"추가 다운로드가 필요합니다.\n({mb:F1} MB)\n와이파이 연결을 권장합니다.";
            noticePopup.SetActive(true);

            bool decided = false, confirmed = false;

            void OnDownload() { confirmed = true;  decided = true; }
            void OnSkip()     { confirmed = false; decided = true; }

            downloadButton.onClick.AddListener(OnDownload);
            skipButton.onClick.AddListener(OnSkip);

            await UniTask.WaitUntil(() => decided, cancellationToken: ct);

            downloadButton.onClick.RemoveListener(OnDownload);
            skipButton.onClick.RemoveListener(OnSkip);
            noticePopup.SetActive(false);
            return confirmed;
        }

        private async UniTask RunDownloadWithRetry(DownloadManager dm, long totalBytes,
                                                   System.Threading.CancellationToken ct)
        {
            float totalMB = totalBytes / (1024f * 1024f);

            for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
            {
                SetStatus(attempt == 1 ? "다운로드 중..." : $"재시도 중... ({attempt}/{MAX_RETRIES})");
                SetProgress(0f);

                bool success = await dm.DownloadAsync(progress =>
                {
                    SetProgress(progress);
                    if (progressDetailText != null)
                        progressDetailText.text = $"{totalMB * progress:F0} MB / {totalMB:F0} MB";
                });

                if (success)
                {
                    SetStatus("완료!");
                    if (progressDetailText != null) progressDetailText.text = "";
                    await UniTask.Delay(600, cancellationToken: ct);
                    // [DOUBLE-GOTO FIX] GoToTitle 제거 — RunFlow가 로그인 후 호출
                    return;
                }

                if (attempt < MAX_RETRIES)
                {
                    SetStatus("다운로드 실패. 잠시 후 재시도합니다...");
                    await UniTask.Delay(3000, cancellationToken: ct);
                }
            }

            // 모든 재시도 실패 → 재시도 or 종료 선택
            bool wantsRetry = await ShowError("다운로드에 실패했습니다.\n다시 시도하시겠습니까?", ct);
            if (wantsRetry)
            {
                await RunDownloadWithRetry(dm, totalBytes, ct); // 처음부터 재시도
            }
            else
            {
                // 종료 선택 시 흐름을 완전히 중단 — Application.Quit()만으론 에디터/딜레이 이슈로
                // 코드가 계속 실행되므로 예외로 RunFlow의 catch까지 즉시 탈출
                throw new OperationCanceledException("유저가 다운로드 실패 후 종료를 선택함.");
            }
        }

        // true = 재시도, false = 종료
        private async UniTask<bool> ShowError(string message, System.Threading.CancellationToken ct)
        {
            errorMessageText.text = message;
            errorPopup.SetActive(true);

            bool decided = false, retry = false;

            void OnRetry() { retry = true;  decided = true; }
            void OnQuit()  { retry = false; decided = true; }

            retryButton.onClick.AddListener(OnRetry);
            if (quitButton != null) quitButton.onClick.AddListener(OnQuit);

            await UniTask.WaitUntil(() => decided, cancellationToken: ct);

            retryButton.onClick.RemoveListener(OnRetry);
            if (quitButton != null) quitButton.onClick.RemoveListener(OnQuit);
            errorPopup.SetActive(false);

            if (!retry) Application.Quit();
            return retry;
        }

        // ─── 헬퍼 ──────────────────────────────────────────────────────────────

        // [크래쉬 방지] 항상 두 버튼을 동시에 제어 — 한 쪽만 잠가도 다른 버튼으로 중복 호출 가능
        private void SetLoginButtons(bool interactable)
        {
            if (btnGoogle != null) btnGoogle.interactable = interactable;
            if (btnGuest  != null) btnGuest.interactable  = interactable;
        }

        private bool HasPreviousLoginRecord()
        {
            var data = GameManager.Instance?.SaveData?.Data;
            return data != null && data.lastLoginMethod != "None";
        }

        private bool TryGetDownloadManager(out DownloadManager dm)
        {
            dm = GameManager.Instance?.Download;
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
            Debug.Log($"[DownloadScene] {msg}");
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
