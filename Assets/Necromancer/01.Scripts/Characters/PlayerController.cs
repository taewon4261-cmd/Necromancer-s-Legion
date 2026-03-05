using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플레이어 입력 처리 및 이동 제어 클래스
/// UnitBase 상속을 통해 공통 스탯 및 피격 로직 공유
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : UnitBase
{
    private Rigidbody2D rb;
    private Vector2 movement;

    [Header("Input Setup")]
    [Tooltip("모바일 UI 가상 조이스틱 (연결 시 조이스틱 우선, 없을 시 WASD 키보드)")]
    public VirtualJoystick virtualJoystick;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (isDead) return;
        
        HandleInput();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        
        Move();
    }

    /// <summary>
    /// 사용자 입력 수집 (가상 조이스틱 우선, 차선책으로 키보드 대기)
    /// </summary>
    private void HandleInput()
    {
        // 1. 기본 키보드 입력 받기 (PC 빌드 및 에디터 테스트용 WASD)
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // 2. 만약 모바일용 조이스틱 UI가 화면에 있고 손가락(마우스)으로 당기고 있다면?
        if (virtualJoystick != null && virtualJoystick.InputVector != Vector2.zero)
        {
            // 키보드 입력을 무시하고 스마트폰 조이스틱 방향으로 덮어씁니다.
            movement = virtualJoystick.InputVector;
        }
        
        // 대각선 이동 시 속도가 빨라지는(피타고라스 루트2배) 현상을 막는 정규화 처리
        movement.Normalize(); 
    }

    /// <summary>
    /// 물리 기반(Kinematic) 위치 업데이트
    /// </summary>
    private void Move()
    {
        rb.velocity = movement * moveSpeed;
    }

    /// <summary>
    /// 플레이어 사망 시 게임 오버 연출 및 로직
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        // 이동 중지 처리
        rb.velocity = Vector2.zero;
        
        // TODO: GameManager에 게임 오버 이벤트 발송 (Result UI 팝업 연동)
        Debug.Log("[PlayerController] Player is dead. Triggering GameOver sequence.");
    }
}
