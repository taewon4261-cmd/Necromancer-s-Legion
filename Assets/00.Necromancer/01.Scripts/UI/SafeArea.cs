// File: Assets/Necromancer/01.Scripts/UI/SafeArea.cs
using UnityEngine;

namespace Necromancer.UI
{
    /// <summary>
    /// 모바일 기기의 노치(Notch) 및 컷아웃 영역을 자동으로 감지하여 
    /// UI가 가려지지 않도록 RectTransform의 패딩을 조절하는 컴포넌트입니다.
    /// </summary>
    [ExecuteAlways]
    public class SafeArea : MonoBehaviour
    {
        [Header("Settings")]
        public bool applyToCamera = false;
        public float minTopPadding = 120f; // 최소 상단 여백 (블랙 바 높이)
        public RectTransform blackBar;     // 동적으로 높이를 조절할 블랙 바 (선택 사항)
        
        private RectTransform _rectTransform;
        private Camera _camera;
        
        private Rect _lastSafeArea = new Rect(0, 0, 0, 0);
        private Vector2 _lastScreenSize = new Vector2(0, 0);
        private ScreenOrientation _lastOrientation = ScreenOrientation.AutoRotation;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _camera = GetComponent<Camera>();

            // 블랙 바 수동 할당이 안 되어 있을 경우 이름으로 자동 검색 (현업 표준 방식)
            if (blackBar == null)
            {
                GameObject go = GameObject.Find("Notch_BlackBar");
                if (go != null) blackBar = go.GetComponent<RectTransform>();
            }

            Refresh();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea ||
                _lastScreenSize.x != Screen.width ||
                _lastScreenSize.y != Screen.height ||
                _lastOrientation != Screen.orientation)
            {
                Refresh();
            }
        }

        private void Refresh()
        {
            Rect safeArea = Screen.safeArea;
            ApplySafeArea(safeArea);
        }

        private void ApplySafeArea(Rect r)
        {
            _lastSafeArea = r;
            _lastScreenSize.x = Screen.width;
            _lastScreenSize.y = Screen.height;
            _lastOrientation = Screen.orientation;

            Canvas canvas = GetComponentInParent<Canvas>();
            float scaleFactor = (canvas != null) ? canvas.scaleFactor : 1.0f;
            
            // 1. 전 기기 공통 수치 계산 (UI Unit 기준)
            // 노치 픽셀을 UI 유닛으로 변환하여 최소 여백(120)과 비교합니다.
            float notchPixelHeight = Screen.height - (r.y + r.height);
            float notchUIHeight = notchPixelHeight / scaleFactor;
            float finalHeaderUIHeight = Mathf.Max(minTopPadding, notchUIHeight);

            // 2. 상단 블랙 바 (Notch_BlackBar) 정렬
            // 항상 최상단에서 시작하여 계산된 finalHeaderUIHeight 만큼 내려옵니다.
            if (blackBar != null)
            {
                blackBar.anchorMin = new Vector2(0, 1);
                blackBar.anchorMax = new Vector2(1, 1);
                blackBar.pivot = new Vector2(0.5f, 1);
                blackBar.anchoredPosition = Vector2.zero;
                blackBar.sizeDelta = new Vector2(0, finalHeaderUIHeight);
            }

            // 3. UI 콘텐츠 루트 (UI_SafeArea_Root) 정렬
            // 블랙 바가 끝나는 지점(finalHeaderUIHeight)부터 콘텐츠가 시작되도록 고정합니다.
            if (_rectTransform != null)
            {
                // 상단은 블랙 바 아래로 고정, 하단은 0(홈 바 고려 시 수정 가능)
                _rectTransform.anchorMin = new Vector2(0, 0);
                _rectTransform.anchorMax = new Vector2(1, 1);
                _rectTransform.pivot = new Vector2(0.5f, 1);
                
                // Top Offset을 블랙 바 높이만큼 내려서 겹침을 방지합니다.
                _rectTransform.offsetMax = new Vector2(0, -finalHeaderUIHeight);
                _rectTransform.offsetMin = new Vector2(0, r.y / scaleFactor); // 하단 세이프 아레나(홈 바) 반영
            }

            // 4. 카메라 뷰포트 (선택 사항)
            if (applyToCamera && _camera != null)
            {
                float safeTopPixel = Screen.height - (finalHeaderUIHeight * scaleFactor);
                _camera.rect = new Rect(0, r.y / Screen.height, 1, (safeTopPixel - r.y) / Screen.height);
            }

            Debug.Log($"[SafeArea] Elite Logic Applied:\n" +
                      $"- Notch(Units): {notchUIHeight:F1}, Header(Units): {finalHeaderUIHeight:F1}\n" +
                      $"- Root Offset Top: {-finalHeaderUIHeight} units");
        }
    }
}
