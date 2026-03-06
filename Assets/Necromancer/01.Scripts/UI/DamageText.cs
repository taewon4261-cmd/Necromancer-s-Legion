// File: Assets/Necromancer/01.Scripts/UI/DamageText.cs
using UnityEngine;
using TMPro;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{
    public class DamageText : MonoBehaviour
    {
        [Header("Animation Settings")]
        public float moveDistance = 0.8f;
        public float duration = 1.0f;
        public AnimationCurve moveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
        public float startScale = 0.02f; // 월드 스페이스용 아주 작은 값
        public float endScale = 0.04f;   // 최종 크기도 작게

        private TextMeshPro textMesh;
        private CancellationTokenSource cts;

        private void Awake()
        {
            textMesh = GetComponent<TextMeshPro>();
            if (textMesh != null)
            {
                textMesh.sortingOrder = 500;
                textMesh.alignment = TextAlignmentOptions.Center;
            }
        }

        private void OnEnable()
        {
            // 리셋: 텍스트가 거대해지는 것을 방지하기 위해 즉시 스케일 고정
            transform.localScale = Vector3.one * startScale;
            
            if (textMesh != null)
            {
                Color c = textMesh.color;
                c.a = 1f;
                textMesh.color = c;
            }

            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
            }
            cts = new CancellationTokenSource();

            AnimateText(cts.Token).Forget();
        }

        private void OnDisable()
        {
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                cts = null;
            }
        }

        private async UniTaskVoid AnimateText(CancellationToken token)
        {
            try
            {
                Vector3 startPos = transform.position;
                Vector3 targetPos = startPos + Vector3.up * moveDistance;
                Color startColor = textMesh.color;
                float elapsed = 0f;

                while (elapsed < duration)
                {
                    if (token.IsCancellationRequested) return;

                    elapsed += Time.deltaTime;
                    float progress = Mathf.Clamp01(elapsed / duration);
                    float curveValue = moveCurve.Evaluate(progress);

                    // 위치 이동
                    transform.position = Vector3.Lerp(startPos, targetPos, curveValue);
                    
                    // 크기 변화
                    transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, curveValue);

                    // 페이드 아웃
                    if (progress > 0.5f)
                    {
                        float alpha = Mathf.Lerp(1f, 0f, (progress - 0.5f) * 2f);
                        textMesh.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                    }

                    await UniTask.Yield(PlayerLoopTiming.Update, token);
                }
            }
            finally
            {
                // 어떤 이유로든 루프가 끝나면 비활성화
                gameObject.SetActive(false);
            }
        }
    }
}
