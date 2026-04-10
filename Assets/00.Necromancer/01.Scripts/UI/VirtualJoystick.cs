// File: Assets/Necromancer/01.Scripts/UI/VirtualJoystick.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Necromancer
{

/// <summary>
/// 모바일 및 PC 테스트 겸용 가상 조이스틱 (Virtual Joystick)
/// 마우스 클릭/드래그 및 스마트폰 화면 터치를 모두 완벽하게 동일하게 지원합니다.
/// </summary>
public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
{
    [Header("UI Components")]
    [Tooltip("조이스틱의 바깥쪽 둥근 배경 (빈 공간이어도 됨)")]
    public RectTransform background;

    [Tooltip("사용자가 직접 드래그해서 움직이는 안쪽 둥근 손잡이")]
    public RectTransform handle;

    private Canvas canvas;
    private RectTransform canvasRect;
    private CanvasGroup canvasGroup;
    private bool isPointerActive;
    private int activePointerId = -1;

    // 플레이어 컨트롤러가 읽어갈 조이스틱의 방향 벡터 (-1.0 ~ 1.0 사이의 값)
    public Vector2 InputVector { get; private set; }

    private void Start()
    {
        // 개발 편의성: 만약 인스펙터에 일일이 넣지 않았다면 자동으로 자식 객체에서 찾습니다.
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null) handle = transform.GetChild(0).GetComponent<RectTransform>();

        // 루트 Canvas 캐싱
        canvas = GetComponentInParent<Canvas>();
        if (canvas != null) canvas = canvas.rootCanvas;
        if (canvas != null) canvasRect = canvas.transform as RectTransform;

        // 프리팹에서 VirtualJoystick이 Canvas 직속 자식이 아닌 경우(예: UI_SafeArea_Root 하위)
        // SafeArea 오프셋 영향을 차단하기 위해 런타임에 루트 캔버스 직속으로 이동합니다.
        // ※ 프리팹에서 이미 Canvas 직속 자식이라면 이 코드는 무해하게 동작합니다.
        if (canvasRect != null && background.parent != canvasRect)
        {
            Vector2 originalSize = background.sizeDelta;
            Vector2 originalPivot = background.pivot;
            background.SetParent(canvasRect, false);
            background.pivot = originalPivot;
            background.sizeDelta = originalSize;
        }

        // [DYNAMIC] 가시성 제어를 위해 CanvasGroup 추가 또는 획득
        canvasGroup = background.GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = background.gameObject.AddComponent<CanvasGroup>();

        HideJoystick();
    }

    private void Update()
    {
        HandleGlobalPointerInput();
    }

    private void HandleGlobalPointerInput()
    {
        if (Input.touchSupported && Input.touchCount > 0)
        {
            HandleTouchInput();
            return;
        }

        HandleMouseInput();
    }

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (IsPointerOverOtherUI()) return;

            isPointerActive = true;
            activePointerId = -1;

            Vector2 pointerPosition = Input.mousePosition;
            ShowJoystick(pointerPosition, GetEventCamera());
            UpdateInputFromScreenPosition(pointerPosition, GetEventCamera());
        }

        if (isPointerActive && Input.GetMouseButton(0))
        {
            UpdateInputFromScreenPosition(Input.mousePosition, GetEventCamera());
        }

        if (isPointerActive && Input.GetMouseButtonUp(0))
        {
            ReleasePointer();
        }
    }

    private void HandleTouchInput()
    {
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (!isPointerActive && touch.phase == TouchPhase.Began)
            {
                if (IsPointerOverOtherUI(touch.fingerId)) return;

                isPointerActive = true;
                activePointerId = touch.fingerId;

                ShowJoystick(touch.position, GetEventCamera());
                UpdateInputFromScreenPosition(touch.position, GetEventCamera());
                return;
            }

            if (isPointerActive && touch.fingerId == activePointerId)
            {
                if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                {
                    UpdateInputFromScreenPosition(touch.position, GetEventCamera());
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    ReleasePointer();
                }

                return;
            }
        }
    }

    // Canvas 렌더 모드에 따라 올바른 카메라 반환
    private Camera GetEventCamera()
    {
        if (canvas == null) return null;
        return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
    }

    private bool IsPointerOverOtherUI(int pointerId = -1)
    {
        if (EventSystem.current == null) return false;

        return pointerId >= 0
            ? EventSystem.current.IsPointerOverGameObject(pointerId)
            : EventSystem.current.IsPointerOverGameObject();
    }

    private void ShowJoystick(Vector2 screenPos, Camera eventCamera)
    {
        // 캔버스 로컬 좌표로 변환 후 월드 좌표로 position 직접 지정
        // → anchor/rect 크기 계산 없이 터치 지점 = 조이스틱 중심 (pivot 0.5,0.5 전제)
        if (canvasRect != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, eventCamera, out Vector2 localPoint))
        {
            background.position = canvasRect.TransformPoint(localPoint);
        }
        else
        {
            background.position = screenPos;
        }

        if (canvasGroup != null) canvasGroup.alpha = 1f;
    }

    private void HideJoystick()
    {
        // [DYNAMIC] 조이스틱 숨김
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        InputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }

    private void UpdateInputFromScreenPosition(Vector2 screenPos, Camera eventCamera)
    {
        Vector2 position = Vector2.zero;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, screenPos, eventCamera, out position))
        {
            float radius = Mathf.Min(background.rect.width, background.rect.height) * 0.5f;
            InputVector = position / radius;
            InputVector = (InputVector.magnitude > 1.0f) ? InputVector.normalized : InputVector;
            handle.anchoredPosition = InputVector * radius;
        }
    }

    private void ReleasePointer()
    {
        isPointerActive = false;
        activePointerId = -1;
        HideJoystick();
    }

    /// <summary>
    /// 조이스틱을 터치(또는 마우스 클릭)하는 순간
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        isPointerActive = true;
        activePointerId = eventData.pointerId;
        ShowJoystick(eventData.position, eventData.pressEventCamera);
        UpdateInputFromScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    /// <summary>
    /// 조이스틱을 누른 채로 드래그하는 중 (실질적인 방향 계산 연산)
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isPointerActive) return;
        UpdateInputFromScreenPosition(eventData.position, eventData.pressEventCamera);
    }

    /// <summary>
    /// 조이스틱에서 마우스 스위치나 손가락을 떼는 순간
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId != activePointerId && activePointerId >= 0) return;
        ReleasePointer();
    }
}
}
