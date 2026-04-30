using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Necromancer.UI
{
    public class LogMessageSlot : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI _text;

        private CanvasGroup _canvasGroup;
        private CancellationTokenSource _cts;
        private Action<LogMessageSlot> _onReturn;

        private void Awake()
        {
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        public void Setup(string message, Action<LogMessageSlot> onReturn)
        {
            _onReturn = onReturn;
            _text.SetText(message);

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            DOTween.Kill(_canvasGroup);
            _canvasGroup.alpha = 0f;
            _canvasGroup.DOFade(1f, 0.3f).SetUpdate(true);

            RunAsync(_cts.Token).Forget();
        }

        private async UniTaskVoid RunAsync(CancellationToken token)
        {
            try
            {
                await UniTask.Delay(2000, ignoreTimeScale: true, cancellationToken: token);

                DOTween.Kill(_canvasGroup);
                _canvasGroup.DOFade(0f, 0.5f).SetUpdate(true).OnComplete(ReturnToPool);
            }
            catch (OperationCanceledException) { }
        }

        private void ReturnToPool()
        {
            gameObject.SetActive(false);
            _onReturn?.Invoke(this);
        }

        private void OnDisable()
        {
            _cts?.Cancel();
            DOTween.Kill(_canvasGroup);
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
        }
    }
}
