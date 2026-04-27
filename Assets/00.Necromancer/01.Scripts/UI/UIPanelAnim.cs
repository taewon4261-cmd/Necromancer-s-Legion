using UnityEngine;
using DG.Tweening;
using System;

namespace Necromancer.UI
{
    /// <summary>
    /// 패널의 Show/Hide에 DOTween 스케일+페이드 애니메이션을 추가합니다.
    /// 패널 루트 오브젝트에 부착하면 자동으로 동작합니다.
    /// </summary>
    public class UIPanelAnim : MonoBehaviour
    {
        [SerializeField] private float _showDuration = 0.2f;
        [SerializeField] private float _hideDuration = 0.15f;
        [SerializeField] private Ease  _showEase = Ease.OutBack;
        [SerializeField] private Ease  _hideEase = Ease.InSine;

        private CanvasGroup _cg;

        private void Awake()
        {
            _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
        }

        public void Show()
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

            gameObject.SetActive(true);
            transform.DOKill();
            _cg.DOKill();

            transform.localScale = Vector3.one * 0.8f;
            _cg.alpha = 0f;
            
            // [UX FIX] 애니메이션 도중에도 인터랙션을 차단하지 않도록 수정 (기존 false 설정 제거)
            _cg.interactable = true; 
            _cg.blocksRaycasts = true;

            transform.DOScale(1f, _showDuration).SetEase(_showEase).SetUpdate(true).SetLink(gameObject);
            _cg.DOFade(1f, _showDuration).SetUpdate(true).SetLink(gameObject);
        }

        public void Hide(Action onComplete = null)
        {
            if (_cg == null) _cg = GetComponent<CanvasGroup>();
            if (_cg == null)
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
                return;
            }

            _cg.interactable = false;
            _cg.blocksRaycasts = false;
            transform.DOKill();
            _cg.DOKill();

            transform.DOScale(0.8f, _hideDuration).SetEase(_hideEase).SetUpdate(true).SetLink(gameObject);
            _cg.DOFade(0f, _hideDuration).SetUpdate(true).SetLink(gameObject).OnComplete(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }
    }
}
