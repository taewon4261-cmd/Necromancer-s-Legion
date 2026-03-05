using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

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

    // 플레이어 컨트롤러가 읽어갈 조이스틱의 방향 벡터 (-1.0 ~ 1.0 사이의 값)
    public Vector2 InputVector { get; private set; }

    private void Start()
    {
        // 개발 편의성: 만약 인스펙터에 일일이 넣지 않았다면 자동으로 자식 객체에서 찾습니다.
        if (background == null) background = GetComponent<RectTransform>();
        if (handle == null) handle = transform.GetChild(0).GetComponent<RectTransform>();
    }

    /// <summary>
    /// 조이스틱을 터치(또는 마우스 클릭)하는 순간
    /// </summary>
    public void OnPointerDown(PointerEventData eventData)
    {
        // 클릭하자마자 바로 드래그 연산으로 넘겨서 반응성을 높입니다.
        OnDrag(eventData);
    }

    /// <summary>
    /// 조이스틱을 누른 채로 드래그하는 중 (실질적인 방향 계산 연산)
    /// </summary>
    public void OnDrag(PointerEventData eventData)
    {
        Vector2 position = Vector2.zero;

        // 터치된 화면 좌표를 조이스틱 UI 안의 로컬 좌표계로 변환합니다.
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, eventData.pressEventCamera, out position))
        {
            // 배경 크기 대비 어느 정도 비율로 터치했는지 계산하여 -1 ~ 1 사이의 X, Y 값으로 만듭니다.
            position.x = (position.x / background.sizeDelta.x) * 2 - 1;
            position.y = (position.y / background.sizeDelta.y) * 2 - 1;

            // 벡터 길이가 1을 넘지 않도록 정규화 (대각선으로 끝까지 당겨도 속도를 일정하게 유지: 피타고라스 최적화)
            InputVector = new Vector2(position.x, position.y);
            InputVector = (InputVector.magnitude > 1.0f) ? InputVector.normalized : InputVector;

            // 실제 화면에 보이는 손잡이(Handle) UI 이미지를 입력 벡터만큼 이동시킵니다.
            handle.anchoredPosition = new Vector2(InputVector.x * (background.sizeDelta.x / 2), InputVector.y * (background.sizeDelta.y / 2));
        }
    }

    /// <summary>
    /// 조이스틱에서 마우스 스위치나 손가락을 떼는 순간
    /// </summary>
    public void OnPointerUp(PointerEventData eventData)
    {
        // 입력 방향 초기화 및 손잡이를 원래 위치(한가운데 정중앙)로 탄성 복귀시킵니다.
        InputVector = Vector2.zero;
        handle.anchoredPosition = Vector2.zero;
    }
}
