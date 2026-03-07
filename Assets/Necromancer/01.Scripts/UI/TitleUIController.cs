using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

namespace Necromancer.UI
{
    /// <summary>
    /// 타이틀 화면의 전체적인 비주얼 연출과 버튼 상호작용을 담당합니다.
    /// DOTween을 활용하여 로고 부유, 버튼 슬라이딩 등의 효과를 구현합니다.
    /// </summary>
    public class TitleUIController : MonoBehaviour
    {
        [Header("Main Visuals")]
        public RectTransform logoTransform;
        public CanvasGroup mainButtonPanel;
        
        [Header("Animation Settings")]
        public float fadeDuration = 1f;
        public float floatingAmount = 20f;
        public float floatingDuration = 2f;

        private void Start()
        {
            InitTitleAnimations();
        }

        private void InitTitleAnimations()
        {
            // 1. 로고 부유 효과: 위아래로 계속 반복 (Yoyo)
            if (logoTransform != null)
            {
                logoTransform.DOAnchorPosY(logoTransform.anchoredPosition.y + floatingAmount, floatingDuration)
                    .SetEase(Ease.InOutSine)
                    .SetLoops(-1, LoopType.Yoyo);
            }

            // 2. 메인 버튼 패널 페이드 인 및 슬라이드 업
            if (mainButtonPanel != null)
            {
                mainButtonPanel.alpha = 0;
                mainButtonPanel.DOFade(1, fadeDuration).SetDelay(0.5f);
                
                RectTransform panelRT = mainButtonPanel.GetComponent<RectTransform>();
                float originalY = panelRT.anchoredPosition.y;
                panelRT.anchoredPosition = new Vector2(panelRT.anchoredPosition.x, originalY - 100f);
                panelRT.DOAnchorPosY(originalY, fadeDuration).SetEase(Ease.OutBack).SetDelay(0.5f);
            }
        }

        /// <summary>
        /// 버튼에 Mouse Enter 시 점진적으로 커지는 효과 (버튼 컴포넌트의 EventTrigger 등에서 호출 가능)
        /// </summary>
        public void OnHoverButton(RectTransform buttonRT)
        {
            buttonRT.DOScale(1.1f, 0.2f).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 버튼에서 Mouse Exit 시 원래 크기로 복구
        /// </summary>
        public void OnExitButton(RectTransform buttonRT)
        {
            buttonRT.DOScale(1f, 0.2f).SetEase(Ease.OutQuad);
        }

        /// <summary>
        /// 버튼 클릭 시 살짝 눌리는 느낌의 애니메이션 후 로직 실행
        /// </summary>
        public void OnClickButton(RectTransform buttonRT, global::System.Action callback)
        {
            buttonRT.DOPunchScale(new Vector3(-0.1f, -0.1f, 0), 0.2f).OnComplete(() => callback?.Invoke());
        }
    }
}
