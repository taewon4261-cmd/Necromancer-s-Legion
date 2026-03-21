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

    [Header("Skill Effects")]
    public float dodgeChance = 0f;
    private float regenAmount = 0f;
    private bool isAuraEnabled = false;

    private Coroutine regenCoroutine;

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

        var side = GameManager.Instance.Resources;

        // 1. 체력 업그레이드 (LobbyUpgradeSO의 수치 적용)
        float hpUpgrade = side.GetUpgradeValue(UpgradeStatType.Health); 
        maxHp += hpUpgrade;
        currentHp = maxHp;

        // 2. 이동 속도 업그레이드
        float speedUpgrade = side.GetUpgradeValue(UpgradeStatType.MoveSpeed);
        moveSpeed += speedUpgrade;

        // 3. 자석 범위 업그레이드 (플레이어가 아닌 GameManager가 관리할 경우 거기서 따로 처리 가능)
        float magnetUpgrade = side.GetUpgradeValue(UpgradeStatType.MagnetRange);
        GameManager.Instance.magnetRadius += magnetUpgrade;
        
        Debug.Log($"[PlayerController] Lobby Upgrades Applied. Final HP: {maxHp}, Speed: {moveSpeed}, Magnet: {GameManager.Instance.magnetRadius}");
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

    public override void TakeDamage(float damage)
    {
        // [스킬 연동] 7. 환영 회피: 확률적 데미지 무효화
        if (dodgeChance > 0f && Random.value <= dodgeChance)
        {
            Debug.Log("[Player] 회피 성공! (Phantom Evasion)");
            return; 
        }

        base.TakeDamage(damage);

        // [연출 연동] 피격 시 화면 흔들림 및 피격 효과
        if (FeedbackManager.Instance != null && gameObject.activeInHierarchy)
        {
            FeedbackManager.Instance.ShakeCamera(0.15f, 0.2f);
            FeedbackManager.Instance.PlayHitEffect(transform.position, "HitEffect");
        }
    }

    public void AddRegen(float amount)
    {
        regenAmount += amount;
        if (regenCoroutine == null)
        {
            regenCoroutine = StartCoroutine(RegenRoutine());
        }
    }

    private IEnumerator RegenRoutine()
    {
        while (!isDead)
        {
            if (currentHp < maxHp)
            {
                currentHp = Mathf.Min(currentHp + regenAmount, maxHp);
            }
            yield return new WaitForSeconds(1f);
        }
    }

    public void EnableDeathAura(bool enable)
    {
        isAuraEnabled = enable;
        // TODO: 실제 오라 VFX 오브젝트를 자식으로 두어 활성화/비활성화
        Debug.Log($"[Player] 죽음의 오라 상태: {enable}");
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
