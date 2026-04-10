
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
    private float auraRadius = 3.5f; // [UPGRADE] ApplyUpgradedStats에서 AuraRange 업그레이드 적용 대상

    [Header("Skill Visuals (Optional - Assign in Inspector for better performance)")]
    [SerializeField] private GameObject auraVisualObject; // [NEW] 인스펙터에서 직접 연결 가능
    private Transform auraVisualInstance; 

    private bool isRegenActive = false;
    private readonly List<UnitBase> auraBuffer = new List<UnitBase>(16); // [PERF] GC 방지용 재사용 버퍼

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
        
        // [1] Player Base Stats
        maxHp += side.GetUpgradeValue(UpgradeStatType.Health); 
        currentHp = maxHp;
        moveSpeed += side.GetUpgradeValue(UpgradeStatType.MoveSpeed);
        
        // [2] Magnet & Utility
        float magnetUpgrade = side.GetUpgradeValue(UpgradeStatType.MagnetRange);
        GameManager.Instance.magnetRadius += magnetUpgrade;
        UpdateMagnetRadius();

        // [3] Combat Upgrades (New)
        // 공격력 가산 (기본 데미지에 합산)
        bodySlamDamage += side.GetUpgradeValue(UpgradeStatType.AttackDamage);

        // 죽음의 오라 범위 가산
        auraRadius += side.GetUpgradeValue(UpgradeStatType.AuraRange);

        Debug.Log($"[PlayerController] Lobby Upgrades Applied. HP: {maxHp}, Atk: {bodySlamDamage}, Speed: {moveSpeed}");
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

    // duration 미지정 시 invincibilityDuration(0.25s) 사용, 지정 시 해당 값 사용 (부활 2s 무적 등)
    private async UniTaskVoid InvincibilityAsync(float duration = -1f)
    {
        isInvincible = true;
        float elapsed = 0f;
        float activeDuration = duration > 0f ? duration : invincibilityDuration;

        while (elapsed < activeDuration)
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
        bool wasEnabled = isAuraEnabled;
        isAuraEnabled = enable;
        
        // [NEW] 비주얼 오브젝트 관리
        UpdateAuraVisual(enable);

        if (isAuraEnabled && !wasEnabled)
        {
            DeathAuraLoopAsync().Forget();
            DeathAuraVisualPulseAsync().Forget(); // 펄싱 애니메이션 시작
        }
        Debug.Log($"[Player] 죽음의 오라 상태: {enable}");
    }

    private void UpdateAuraVisual(bool enable)
    {
        if (enable)
        {
            // 1. 인스펙터에서 미리 할당된 오브젝트가 있다면 우선 사용
            if (auraVisualInstance == null && auraVisualObject != null)
            {
                auraVisualInstance = auraVisualObject.transform;
                // 범위에 맞춰 스케일 고정 (반경 3.5 -> 7.0)
                auraVisualInstance.localScale = Vector3.one * 7f;
            }

            // 2. 할당된 것도 없고 생성된 것도 없다면 그때만 자동 생성 (Safe-net)
            if (auraVisualInstance == null)
            {
                GameObject auraGo = new GameObject("DeathAura_Visual");
                auraGo.transform.SetParent(this.transform);
                auraGo.transform.localPosition = Vector3.zero;

                var sr = auraGo.AddComponent<SpriteRenderer>();
                sr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd");
                if (sr.sprite == null) sr.sprite = Resources.GetBuiltinResource<Sprite>("UI/Skin/UISprite.psd");
                
                sr.color = new Color(0.6f, 0.1f, 0.9f, 0.15f);
                sr.sortingOrder = -1;
                
                auraGo.transform.localScale = Vector3.one * 7f;
                auraVisualInstance = auraGo.transform;
            }
            auraVisualInstance.gameObject.SetActive(true);
        }
        else if (auraVisualInstance != null)
        {
            auraVisualInstance.gameObject.SetActive(false);
        }
    }

    private async UniTaskVoid DeathAuraVisualPulseAsync()
    {
        var token = gameObject.GetCancellationTokenOnDestroy();
        Vector3 baseScale = Vector3.one * 7f;

        while (!isDead && isAuraEnabled && !token.IsCancellationRequested)
        {
            if (auraVisualInstance != null)
            {
                // 소생하는 느낌의 부드러운 펄싱 효과
                float sinValue = (Mathf.Sin(Time.time * 2f) + 1f) / 2f; // 0 ~ 1 사이 반복
                auraVisualInstance.localScale = baseScale * (0.95f + sinValue * 0.1f);
            }
            await UniTask.Yield(PlayerLoopTiming.Update, token);
        }
    }

    private async UniTaskVoid DeathAuraLoopAsync()
    {
        var token = gameObject.GetCancellationTokenOnDestroy();
        // auraRadius는 클래스 필드 사용 (ApplyUpgradedStats에서 업그레이드 수치 적용됨)
        float auraDamage = 15f; // [BALANCE] 기초 데미지 15

        while (!isDead && isAuraEnabled && !token.IsCancellationRequested)
        {
            // 주변 적 스캔 (UnitManager 격자 시스템 활용)
            if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
            {
                auraBuffer.Clear();
                GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, auraRadius, auraBuffer);

                foreach (var unit in auraBuffer)
                {
                    if (unit != null && unit is EnemyAI && !unit.IsDead)
                    {
                        // 초당 데미지 적용
                        unit.ApplyDamage(auraDamage, this);

                        // [VISUAL] 오라 피격 이펙트 (필요 시 추가 가능)
                    }
                }
            }

            // 0.8초마다 한 번씩 타격 (너무 자주 연산하지 않도록 최적화)
            await UniTask.Delay(800, cancellationToken: token);
        }
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
        // [UPGRADE] 부활 로직 체크
        if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
        {
            if (GameManager.Instance.skillManager.totalResurrections > 0)
            {
                GameManager.Instance.skillManager.totalResurrections--;
                Resurrect();
                return;
            }
        }

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

    private void Resurrect()
    {
        isDead = false;
        currentHp = maxHp;
        
        // 2초간 무적 및 시각적 효과
        InvincibilityAsync(2.0f).Forget();
        
        // [SOUND] 부활 효과음
        if (GameManager.Instance != null && GameManager.Instance.Sound != null)
            GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxWin); 

        Debug.Log("<color=gold>[Player] RESURRECTED!</color> Remaining Lives: " + (GameManager.Instance?.skillManager?.totalResurrections ?? 0));
    }
}
}
