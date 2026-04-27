using System;
using System.Threading;
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
            // OnLoginResult는 Start()에서 구독 — OnEnable 시점에 GameManager.Auth가 미초기화일 수 있음
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
            // [FIX] 모든 Awake() 완료 후 Start()가 실행되므로 여기서 구독해야 Auth가 보장됨
            var auth = GameManager.Instance?.Auth;
            if (auth != null)
                auth.OnLoginResult += HandleLoginResult;
            else
                Debug.LogError("[DownloadScene] GameManager.Auth is NULL in Start! 로그인 콜백을 받을 수 없습니다.");

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
                // [TIMING FIX] 기기 및 네트워크 모듈이 완전히 준비될 시간 확보
                await UniTask.Delay(300, cancellationToken: ct);

                // ── 1단계: Firebase 초기화 완료 대기 ─────────────────────────────────
                // [RACE CONDITION FIX] Firebase와 Addressables가 동시에 네트워크를 점유하면
                // 모바일 저사양 기기에서 초기화 실패가 잦으므로 Firebase 완료 후 Addressables 시작
                SetStatus("초기화 중...");
                var auth = GameManager.Instance?.Auth;
                if (auth != null && !auth.IsFirebaseReady && auth.CurrentState != AuthState.Failed)
                {
                    // [DEADLOCK FIX] Failed 상태도 탈출 조건에 포함 — Firebase 초기화 실패 시 무한 대기 방지
                    await UniTask.WaitUntil(() =>
                        GameManager.Instance?.Auth?.IsFirebaseReady == true ||
                        GameManager.Instance?.Auth?.CurrentState == AuthState.Failed,
                        cancellationToken: ct);
                }

                // ── 2단계: 다운로드 (Firebase 준비 완료 후 Addressables 시작) ──────────
                authPanel.SetActive(false);
                await RunDownloadFlow(ct);

                // ── 3단계: 로그인 ────────────────────────────────────────────────────
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
            if (auth == null) return false;

            // 이미 로그인 성공 상태면 즉시 통과
            if (auth.CurrentState == AuthState.LoggedIn || auth.CurrentState == AuthState.Guest)
            {
                Debug.Log("[DownloadScene] 이미 로그인 완료 상태. 로그인 단계 생략.");
                return true;
            }

            // [RACE CONDITION FIX] 다운로드 중 자동 로그인이 이미 실패했으면 바로 수동 로그인 표시
            // — WaitForLoginResultAsync를 호출하면 TCS가 영원히 채워지지 않아 멈춤
            if (auth.CurrentState == AuthState.Failed)
                return await ShowLoginPanelAndWait(ct);

            // 이전 로그인 기록이 있으면 자동 로그인 진행 중 → 패널 숨기고 대기
            bool hasRecord = HasPreviousLoginRecord();
            if (hasRecord)
            {
                SetStatus("로그인 중...");
                authPanel.SetActive(false);

                // 대기 직전 한 번 더 체크 (Failed로 바뀐 경우 방어)
                if (auth.CurrentState == AuthState.Failed)
                {
                    SetStatus("자동 로그인 실패.");
                    return await ShowLoginPanelAndWait(ct);
                }

                bool autoResult = await WaitForLoginResultAsync(ct, withTimeout: true);

                // 자동 로그인 실패 → 수동 로그인 패널로 전환
                if (!autoResult)
                {
                    SetStatus("자동 로그인 실패.");
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
        // withTimeout=true 일 때만 10초 타임아웃 적용 (자동 로그인 전용)
        // 수동 로그인 패널 대기에는 타임아웃 없음 — 사용자 행동을 무한 대기해야 함
        private async UniTask<bool> WaitForLoginResultAsync(
            System.Threading.CancellationToken ct, bool withTimeout = false)
        {
            var auth = GameManager.Instance?.Auth;

            // 이미 완료 상태면 TCS 없이 즉시 반환
            if (auth != null)
            {
                if (auth.CurrentState == AuthState.LoggedIn || auth.CurrentState == AuthState.Guest) return true;
                if (auth.CurrentState == AuthState.Failed) return false;
            }

            _loginTcs = new UniTaskCompletionSource<bool>();

            // TCS 생성 직후 한 번 더 체크하여 결과 유실 방지
            if (auth != null)
            {
                if (auth.CurrentState == AuthState.LoggedIn || auth.CurrentState == AuthState.Guest)
                    _loginTcs.TrySetResult(true);
                else if (auth.CurrentState == AuthState.Failed)
                    _loginTcs.TrySetResult(false);
            }

            if (withTimeout)
            {
                // [THREAD-SAFE TIMEOUT] CancelAfter 타이머는 ThreadPool에서 발동되어 UniTask 상태머신을
                // 백그라운드 스레드에서 재개시킬 수 있음 → Unity API 호출 시 크래시.
                // WhenAny 방식은 PlayerLoop(메인 스레드) 위에서만 동작하므로 안전함.
                try
                {
                    var loginTask   = _loginTcs.Task;
                    var timeoutTask = UniTask.Delay(10000, cancellationToken: ct)
                                             .ContinueWith(() => false);

                    var (winner, loginResult, _) = await UniTask.WhenAny(loginTask, timeoutTask);

                    if (winner == 0)
                        return loginResult;

                    // 타임아웃 발동 — 그러나 실제로 로그인이 완료됐을 수도 있으므로 재확인
                    var authState = GameManager.Instance?.Auth?.CurrentState;
                    if (authState == AuthState.LoggedIn || authState == AuthState.Guest)
                    {
                        _loginTcs = null;
                        return true;
                    }

                    Debug.LogWarning("[DownloadScene] 자동 로그인 10초 타임아웃. 수동 로그인으로 전환.");
                    _loginTcs = null;
                    return false;
                }
                catch (OperationCanceledException)
                {
                    // ct 취소(씬 파괴) — RunFlow로 전파
                    throw;
                }
            }

            // 타임아웃 없음 — 사용자가 버튼을 누를 때까지 무한 대기
            return await _loginTcs.Task.AttachExternalCancellation(ct);
        }

        // AuthManager.OnLoginResult 콜백 → TCS에 결과 전달
        private void HandleLoginResult(bool success, string uid)
        {
            Debug.Log($"[DownloadScene] HandleLoginResult: success={success}, uid={uid ?? "null"}, _loginTcs={(_loginTcs != null ? "SET" : "NULL")}");

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
                // [HARD UPDATE] 서버 연결 실패 시 로그인 진행 차단 — 재시도 또는 종료만 허용
                bool wantsRetry = await ShowError("서버 연결에 실패했습니다.\n네트워크 상태를 확인해주세요.", ct);
                if (wantsRetry)
                    await RunDownloadFlow(ct);
                return;
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

            // 패치 안내 팝업 — 필수 다운로드, 거부 시 종료
            await ShowNoticePopup(totalBytes, ct);

            // 다운로드 실행
            await RunDownloadWithRetry(dm, totalBytes, ct);
        }

        private async UniTask ShowNoticePopup(long bytes, System.Threading.CancellationToken ct)
        {
            float mb = bytes / (1024f * 1024f);
            noticeSizeText.text = string.Format(
                "원활한 게임 플레이를 위해 최신 데이터 다운로드가 필요합니다.\n" +
                "<b>(용량: {0:F1} MB)</b>\n\n" +
                "<size=80%>※ LTE/5G 이용 시 데이터 요금이 발생할 수 있으니\n" +
                "Wi-Fi 환경에서 다운로드하시길 권장합니다.</size>",
                mb);

            // [MANDATORY] 스킵 버튼을 "게임 종료"로 재활용
            if (skipButton != null)
            {
                skipButton.gameObject.SetActive(true);
                var label = skipButton.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null) label.text = "게임 종료";
            }

            noticePopup.SetActive(true);

            bool decided = false;

            void OnDownload() { decided = true; }
            void OnSkip()     { Application.Quit(); }  // 거부 시 게임 종료

            downloadButton.onClick.AddListener(OnDownload);
            if (skipButton != null) skipButton.onClick.AddListener(OnSkip);

            await UniTask.WaitUntil(() => decided, cancellationToken: ct);

            downloadButton.onClick.RemoveListener(OnDownload);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkip);
            noticePopup.SetActive(false);
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
            // [REINSTALL FIX] null이면 C#에서 null != "None" = true가 되어 재설치 시
            // lastLoginMethod가 초기화되지 않은 경우에도 hasRecord=true로 오판되는 버그 방지
            return data != null &&
                   !string.IsNullOrEmpty(data.lastLoginMethod) &&
                   data.lastLoginMethod != "None";
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
