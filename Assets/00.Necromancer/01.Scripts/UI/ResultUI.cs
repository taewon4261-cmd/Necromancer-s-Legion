using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer.Core;
using Necromancer.Systems;
using UnityEngine.SceneManagement;
using DG.Tweening;

namespace Necromancer.UI
{
    /// <summary>
    /// 스테이지 클리어 또는 패배 시 나타나는 결과창 전용 컨트롤러
    /// </summary>
    public class ResultUI : MonoBehaviour
    {
        [Header("UI Components")]
        [SerializeField] private TextMeshProUGUI tmpTitle;
        [SerializeField] private TextMeshProUGUI tmpSoulCount;
        [SerializeField] private Button btnConfirm;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("2배 보상 광고")]
        [SerializeField] private Button btnDoubleReward;
        [SerializeField] private TextMeshProUGUI tmpDoubleBtnLabel;

        // 이번 판에서 획득한 소울 (보너스 지급 기준값)
        private int _currentEarnedSoul;
        // 중복 지급 방지 플래그
        private bool _isDoubleRewarded = false;

        private void Awake()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 결과창을 활성화하고 데이터를 바인딩합니다.
        /// </summary>
        public void Open(bool isVictory, int soulCount)
        {
            gameObject.SetActive(true);
            _currentEarnedSoul = soulCount;
            _isDoubleRewarded = false;

            // [STABILITY] 시간 정지 상태에서도 애니메이션이 작동하도록 SetUpdate(true) 필수 적용
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.DOFade(1f, 0.5f).SetUpdate(true).SetLink(gameObject);
            }

            transform.localScale = Vector3.one * 0.8f;
            transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);

            if (tmpTitle != null)
            {
                tmpTitle.text = isVictory ? "승 리" : "패 배";
                tmpTitle.color = isVictory ? Color.green : Color.red;
            }

            if (tmpSoulCount != null)
                tmpSoulCount.text = $"획득한 소울: {soulCount:N0}";

            // 2배 버튼 초기 상태 설정
            if (btnDoubleReward != null)
                btnDoubleReward.interactable = true;

            if (tmpDoubleBtnLabel != null)
                tmpDoubleBtnLabel.text = "2배 받기 (광고)";
        }

        /// <summary>
        /// 2배 받기 버튼 클릭 시 호출 (Inspector에서 OnClick 연결)
        /// </summary>
        public void OnClick_DoubleReward()
        {
            if (_isDoubleRewarded) return;

            // 즉시 버튼 비활성화 (중복 클릭 방지)
            if (btnDoubleReward != null)
                btnDoubleReward.interactable = false;

            var adManager = GameManager.Instance?.AdManager;
            if (adManager == null)
            {
                Debug.LogWarning("[ResultUI] AdManager not found.");
                OnDoubleFailed();
                return;
            }

            adManager.ShowRewardedAd(AdManager.AdUnitType.DoubleReward, OnDoubleSuccess, OnDoubleFailed);
        }

        /// <summary>
        /// 광고 시청 완료 콜백
        /// </summary>
        private void OnDoubleSuccess()
        {
            if (_isDoubleRewarded) return;
            _isDoubleRewarded = true;

            // ResourceManager를 통해 보너스 소울 지급 및 Firestore 동기화
            if (ResourceManager.Instance != null)
                ResourceManager.Instance.DoubleSessionSoul(_currentEarnedSoul);

            int totalEarned = _currentEarnedSoul * 2;

            // 텍스트 업데이트
            if (tmpSoulCount != null)
                tmpSoulCount.text = $"획득한 소울: {totalEarned:N0} <color=yellow>(x2)</color>";

            // DOTween PunchScale 연출
            if (tmpSoulCount != null)
                tmpSoulCount.transform
                    .DOPunchScale(Vector3.one * 0.3f, 0.5f, 8, 0.5f)
                    .SetUpdate(true)
                    .SetLink(gameObject);

            // 버튼 텍스트 변경 (재클릭 차단 표시)
            if (tmpDoubleBtnLabel != null)
                tmpDoubleBtnLabel.text = "획득 완료";

            Debug.Log($"<color=green>[ResultUI]</color> 2배 보상 지급 완료. 보너스: +{_currentEarnedSoul}");
        }

        /// <summary>
        /// 광고 실패/취소 콜백 — 버튼을 다시 활성화하여 재시도 허용
        /// </summary>
        private void OnDoubleFailed()
        {
            if (_isDoubleRewarded) return;

            if (btnDoubleReward != null)
                btnDoubleReward.interactable = true;

            Debug.LogWarning("[ResultUI] 광고 시청 실패 또는 취소. 버튼 재활성화.");
            GameManager.Instance?.Popup?.ShowMessagePopup("광고를 불러올 수 없습니다.\n잠시 후 다시 시도해주세요.");
        }

        /// <summary>
        /// 확인 버튼 클릭 시 타이틀 씬으로 이동 (시간 복구 포함)
        /// </summary>
        public void OnClick_Confirm()
        {
            // [CLEANUP] 스테이지 종료 전 모든 사운드 및 물리 상호작용 강제 중지
            if (GameManager.Instance != null)
                GameManager.Instance.CleanupGameSession();

            SceneManager.LoadScene("TitleScene");
        }
    }
}
