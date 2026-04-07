
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{
/// <summary>
/// 플레이어 입력 처리 및 이동 제어 클래스
/// UnitBase 상속을 통해 공통 스탯 및 피격 로직 공유
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : UnitBase
{
    [Header("Player Survival")]
    [SerializeField] private float invincibilityDuration = 0.25f;
    private bool isInvincible = false;
    private bool isKnockbackActive = false;

    [Header("Inspector References (Zero-Search)")]
    [SerializeField] private Rigidbody2D rb;
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

    [Header("Magnet Setup")]
    [SerializeField] private CircleCollider2D magnetCollider;

    [Header("Skill Effects")]
    public float dodgeChance = 0f;
    private float regenAmount = 0f;
    private bool isAuraEnabled = false;

    private bool isRegenActive = false;

    protected override void Awake()
    {
        if (GameManager.Instance != null) 
        {
            GameManager.Instance.playerTransform = transform;
            GameManager.Instance.playerController = this;
        }
        base.Awake();
        // [Pure Inspector] rb는 인스펙터에서 사전에 할당되었습니다.

        // [STABILITY] 고속 이동 시 적/아이템 판정 누락 방지를 위해 연속 충돌 감지 활성화
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.velocity = Vector2.zero;
        }

        // [PERFORMANCE] 인스펙터 직렬화 할당 (런타임 검색 비용 0)
        if (magnetCollider == null)
        {
            Debug.LogWarning("[PlayerController] Magnet Collider is NOT assigned in Inspector!");
        }
    }

    protected override void OnEnable()
    {
        base.OnEnable(); // UnitBase의 HP 초기화 로직 보존
        SkillManager.OnPlayerStatsChanged += UpdateMagnetRadius;
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SkillManager.OnPlayerStatsChanged -= UpdateMagnetRadius;
    }

    private void Start()
    {
        ApplyUpgradedStats();
        if (virtualJoystick == null)
        {
            virtualJoystick = FindObjectOfType<VirtualJoystick>();
        }
    }

    private void ApplyUpgradedStats()
    {
        if (GameManager.Instance == null || GameManager.Instance.Resources == null) return;

        var side = GameManager.Instance.Resources;
        float hpUpgrade = side.GetUpgradeValue(UpgradeStatType.Health); 
        maxHp += hpUpgrade;
        currentHp = maxHp;

        float speedUpgrade = side.GetUpgradeValue(UpgradeStatType.MoveSpeed);
        moveSpeed += speedUpgrade;

        float magnetUpgrade = side.GetUpgradeValue(UpgradeStatType.MagnetRange);
        GameManager.Instance.magnetRadius += magnetUpgrade;
        
        UpdateMagnetRadius();

        Debug.Log($"[PlayerController] Lobby Upgrades Applied. Final HP: {maxHp}, Speed: {moveSpeed}, Magnet: {GameManager.Instance.magnetRadius}");
    }

    public void UpdateMagnetRadius()
    {
        if (magnetCollider != null && GameManager.Instance != null)
        {
            magnetCollider.radius = GameManager.Instance.magnetRadius;
            Debug.Log($"[PlayerController] Magnet Area radius synchronized: {magnetCollider.radius}");
        }
    }

    public override void ManualUpdate(float deltaTime)
    {
        base.ManualUpdate(deltaTime);
        if (isDead) return;
        
        HandleInput();
        UpdateAnimation();
    }

    public override void ManualFixedUpdate(float fixedDeltaTime)
    {
        if (isDead) return;
        Move();
    }

    private void Move()
    {
        if (isKnockbackActive) return; // 넉백 중에는 입력 이동 무시
        rb.velocity = movement * moveSpeed;
    }

    public override void TakeDamage(float damage, UnitBase attacker = null)
    {
        if (isInvincible || isDead) return;

        if (dodgeChance > 0f && Random.value <= dodgeChance)
        {
            Debug.Log("[Player] 회피 성공! (Phantom Evasion)");
            return; 
        }

        base.TakeDamage(damage, attacker);

        // [I-Frame & Knockback]
        if (gameObject.activeInHierarchy)
        {
            InvincibilityAsync().Forget();
            if (attacker != null) ApplyKnockback(attacker.transform.position);
        }

        if (GameManager.Instance != null && GameManager.Instance.feedbackManager != null && gameObject.activeInHierarchy)
        {
            GameManager.Instance.feedbackManager.ShakeCamera(0.15f, 0.2f);
        }
    }

    private void ApplyKnockback(Vector3 attackerPos)
    {
        KnockbackAsync(attackerPos).Forget();
    }

    private async UniTaskVoid KnockbackAsync(Vector3 attackerPos)
    {
        isKnockbackActive = true;
        Vector2 knockbackDir = (transform.position - attackerPos).normalized;
        
        // 0.1초 동안 강한 힘으로 밀쳐냄
        rb.velocity = knockbackDir * 8f; 
        await UniTask.Delay(100, cancellationToken: gameObject.GetCancellationTokenOnDestroy());
        
        isKnockbackActive = false;
    }

    private async UniTaskVoid InvincibilityAsync()
    {
        isInvincible = true;
        float elapsed = 0f;
        
        // 0.25초 동안 깜빡임 연출
        while (elapsed < invincibilityDuration)
        {
            if (unitSprite != null) unitSprite.color = new Color(1, 1, 1, 0.2f);
            await UniTask.Delay(50);
            if (unitSprite != null) unitSprite.color = Color.white;
            await UniTask.Delay(50);
            elapsed += 0.1f;
        }

        if (unitSprite != null) unitSprite.color = Color.white;
        isInvincible = false;
    }

    public void AddRegen(float amount)
    {
        regenAmount += amount;
        if (!isRegenActive)
        {
            RegenAsync().Forget();
        }
    }

    private async UniTaskVoid RegenAsync()
    {
        isRegenActive = true;
        var token = gameObject.GetCancellationTokenOnDestroy();

        while (!isDead && !token.IsCancellationRequested)
        {
            if (currentHp < maxHp)
            {
                currentHp = Mathf.Min(currentHp + regenAmount, maxHp);
            }
            await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token);
        }
        isRegenActive = false;
    }

    public void EnableDeathAura(bool enable)
    {
        isAuraEnabled = enable;
        Debug.Log($"[Player] 죽음의 오라 상태: {enable}");
    }

    private void UpdateAnimation()
    {
        if (unitAnimator == null || rb == null) return;
        
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        if (rb.velocity.x > 0.01f) unitSprite.flipX = false;
        else if (rb.velocity.x < -0.01f) unitSprite.flipX = true;
    }

    private void HandleInput()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        if (virtualJoystick != null && virtualJoystick.InputVector != Vector2.zero)
        {
            movement = virtualJoystick.InputVector;
        }
        movement.Normalize(); 
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // 처음 닿았을 때 즉각 타격
        HandleSlam(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // [OPTIMIZATION] 플레이어의 트리거 연산도 5프레임마다 한 번만 수행하여 부하 절감
        if (Time.frameCount % 5 != 0) return;
        
        // 머물러 있을 때의 타격 (쿨타임 체크 포함)
        HandleSlam(collision);
    }

    private void HandleSlam(Collider2D collision)
    {
        if (isDead || Time.time < lastSlamTime + slamCooldown) return;

        if (collision.CompareTag("Enemy"))
        {
            if (collision.TryGetComponent(out IDamageable target))
            {
                target.ApplyDamage(bodySlamDamage, this);
                
                // [REGISTRY PATTERN] 본체 몸통 박치기 시에도 스킬 효과 적용
                if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
                {
                    GameManager.Instance.skillManager.ApplyAttackEffects(target.Unit);
                }
                
                lastSlamTime = Time.time;
            }
        }
    }

    protected override void Die()
    {
        base.Die();
        if (unitAnimator != null)
        {
            unitAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
        }

        rb.velocity = Vector2.zero;
        rb.bodyType = RigidbodyType2D.Static; 
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnStageFailed();
        }
        
        Debug.Log("[PlayerController] Player is dead. Triggering GameOver sequence.");
    }
}
}
