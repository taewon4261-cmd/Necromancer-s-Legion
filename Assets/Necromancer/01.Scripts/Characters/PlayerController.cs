// File: Assets/Necromancer/01.Scripts/Characters/PlayerController.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
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

    [Header("Test Attack (임시)")]
    [Tooltip("몸통 박치기 데미지")]
    public float bodySlamDamage = 50f;
    [Tooltip("박치기 쿨타임")]
    public float slamCooldown = 0.5f;
    private float lastSlamTime;

    [Header("Persistent Stats (Data Binding)")]
    private float bonusHealth;
    private float bonusMagnetRange;

    protected override void Awake()
    {
        // GameManager에 스스로를 등록 (성능 최적화용)
        if (GameManager.Instance != null) GameManager.Instance.playerTransform = transform;

        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        ApplyUpgradedStats();
    }

    /// <summary>
    /// GameManager의 ResourceManager를 통해 업그레이드된 스탯을 가져와 적용합니다.
    /// </summary>
    private void ApplyUpgradedStats()
    {
        if (GameManager.Instance == null || GameManager.Instance.Resources == null) return;

        // 예시: LobbyUpgradeSO 데이터들이 리스트로 관리되고 있다고 가정하거나
        // 직접 ResourceManager에서 특정 스탯 타입을 가져오는 함수가 필요할 수 있습니다.
        // 현재 LobbyUpgradeSO는 개별로 존재하므로, 기획상 20종을 어떻게 관리할지 정해야 함.
        // 여기서는 간단하게 GameManager에서 전역적으로 관리하는 '업그레이드 값'을 사용한다고 가정.

        // TODO: 실제 LobbyUpgradeSO 에셋들을 로드하여 적용하는 관리자 클래스(UpgradeManager 등) 추가 검토
        // 임시로 기본 UnitBase 스탯에 가산
        maxHp += bonusHealth;
        currentHp = maxHp;
        
        Debug.Log($"[PlayerController] Upgraded Stats Applied. HP: {maxHp}, MoveSpeed: {moveSpeed}");
    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;
        
        HandleInput();
        UpdateAnimation();
    }

    private void FixedUpdate()
    {
        if (isDead) return;
        Move();
    }

    private void Move()
    {
        rb.velocity = movement * moveSpeed;
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;

        // 1. 이동 애니메이션 제어 (속도가 있으면 재생)
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        animator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        // 2. 좌우 반전 (속도의 X값을 기준으로 반전)
        if (rb.velocity.x > 0.01f) spriteRenderer.flipX = false;
        else if (rb.velocity.x < -0.01f) spriteRenderer.flipX = true;
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
    /// 무기가 없는 1주차 테스트용 플레이어 몸통 박치기 판정
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastSlamTime + slamCooldown) return;

        // 상대방이 적인지 태그로 확인
        if (collision.gameObject.CompareTag("Enemy"))
        {
            UnitBase enemyObj = collision.gameObject.GetComponent<UnitBase>();
            if (enemyObj != null)
            {
                enemyObj.TakeDamage(bodySlamDamage); // 적 피 깎기
                lastSlamTime = Time.time;
            }
        }
    }

    /// <summary>
    /// 플레이어 사망 시 게임 오버 연출 및 로직
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        // 이동 중지 처리 및 샌드백처럼 밀리는 현상 방지
        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static; // 죽으면 시체가 안 밀리게 고정
        
        // GameManager에 게임 오버 이벤트 발송
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageFailed();
        }
        
        Debug.Log("[PlayerController] Player is dead. Triggering GameOver sequence.");
    }
}
}
