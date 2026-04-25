
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{
/// <summary>
/// 적의 시체에서 부활한 아군 해골 미니언 AI
/// 살아남은 적(Enemy)들 중 가장 가까운 대상을 찾아내어 공격하며, 수명이 다하면 소멸합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class MinionAI : UnitBase
{
    [Header("Identity & Data")]
    public Necromancer.Data.MinionUnlockSO minionData; // [NEW] 개별 능력치 데이터 연결

    [Header("Minion Settings (Overridden by SO)")]
    public float attackDamage = 15f;
    public float lifeTime = 10f;
    public float targetScanRate = 0.5f;
    public float hitCooldown = 0.5f;
    public float attackRange = 1.5f; // [NEW] 공격 사거리
public int TargetPriority => (minionData != null) ? minionData.targetPriority : 5; // [NEW] 우선순위 노출

[Header("Inspector References (Zero-Search)")]
[SerializeField] private Rigidbody2D rb;

private AsyncOperationHandle<RuntimeAnimatorController> _animHandle;
private CancellationTokenSource _loadCts;

private Transform currentTarget;
private float lastHitTime;
private float spawnTime;
private Transform playerTransform;
private CancellationTokenSource lifetimeCts;

/// <summary>
/// [AUTOMATION] 소환 시 데이터를 주입받아 외형과 스탯을 동적으로 설정합니다.
/// 애니메이터는 비동기로 로드됩니다 (캐시된 번들이므로 보통 1~2프레임 이내).
/// </summary>
public void Initialize(Necromancer.Data.MinionUnlockSO data)
{
    this.minionData = data;

    // 스탯 재계산 및 체력 회복 (즉시)
    ApplyGlobalBuffs();
    this.currentHp = this.maxHp;

    // 애니메이터: 비동기 로드 시작 (이전 로드 취소 후 새로 시작)
    _loadCts?.Cancel();
    _loadCts?.Dispose();
    _loadCts = new CancellationTokenSource();
    LoadAnimatorAsync(_loadCts.Token).Forget();
}

private async UniTaskVoid LoadAnimatorAsync(CancellationToken ct)
{
    if (minionData?.animatorController == null || unitAnimator == null) return;

    // 이전 핸들 해제
    if (_animHandle.IsValid())
    {
        Addressables.Release(_animHandle);
        _animHandle = default;
    }

    _animHandle = minionData.animatorController.LoadAssetAsync<RuntimeAnimatorController>();
    while (!_animHandle.IsDone)
    {
        if (ct.IsCancellationRequested) return;
        await UniTask.Yield();
    }

    if (_animHandle.Status == AsyncOperationStatus.Succeeded && unitAnimator != null)
        unitAnimator.runtimeAnimatorController = _animHandle.Result;
    else if (_animHandle.Status != AsyncOperationStatus.Succeeded)
        Debug.LogWarning($"[MinionAI] AnimatorController 로드 실패: {minionData.minionID}");
}

    // [OPTIMIZATION] 물리 쿼리용 버퍼 및 레이어 마스크
    private static readonly Collider2D[] scanBuffer = new Collider2D[10];
    private static readonly Collider2D[] explosionBuffer = new Collider2D[20];
    [SerializeField] private LayerMask enemyLayer;

    // [OPTIMIZATION] 타겟팅용 버퍼
    private float scanRange = 10.0f;
        private static readonly List<UnitBase> sharedNearbyBuffer = new List<UnitBase>(32);

    protected override void Awake()
    {
        base.Awake();
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    protected override void OnEnable()
    {
        // 1. 데이터 기반 초기화 및 전역 버프 적용
        ApplyGlobalBuffs();
        SkillManager.OnMinionStatsChanged += ApplyGlobalBuffs;
        
        base.OnEnable();
        
        spawnTime = Time.time;
        currentTarget = null;
        lifetimeCts = new CancellationTokenSource();

        if (GameManager.Instance != null) playerTransform = GameManager.Instance.playerTransform;
        ScanForTargetAsync(lifetimeCts.Token).Forget();
    }

    private void ApplyGlobalBuffs()
    {
        if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
        {
            SkillManager sManager = GameManager.Instance.skillManager;
            
            // [DATA-DRIVEN] 데이터 기반 기본값 설정 (SO가 없을 경우 기본값 유지)
            float baseHpVal = (minionData != null) ? minionData.baseHp : 50f;
            float baseSpeedVal = (minionData != null) ? minionData.baseSpeed : 3f;
            float baseDmgVal = (minionData != null) ? minionData.baseDamage : 15f;
            float baseAtkSpeedVal = (minionData != null) ? minionData.baseAttackSpeed : 1.0f; // [NEW] 기본 공속
            this.attackRange = (minionData != null) ? minionData.attackRange : 1.5f;

            float oldMaxHp = maxHp;
            float hpRatio = (oldMaxHp > 0) ? currentHp / oldMaxHp : 1f;

            // [SKILL & UPGRADE] SkillManager에서 합산된 글로벌 배율만 적용 (중복 적용 방지)
            maxHp = baseHpVal * sManager.globalMinionHpBonusRatio;
            attackDamage = baseDmgVal * sManager.globalMinionDamageBonusRatio;
            moveSpeed = baseSpeedVal * sManager.globalMinionSpeedBonusRatio;
            
            // [NEW] 소환 유지 시간 보너스 적용 (기본 10초 + 업그레이드 수치)
            this.lifeTime = 10f + sManager.globalMinionDurationBonus;

            // [NEW] 공격 속도를 쿨타임으로 변환 (예: 공속 2.0 -> 0.5초 쿨타임)
            float finalAtkSpeed = baseAtkSpeedVal * sManager.globalMinionAttackSpeedBonusRatio;
            this.hitCooldown = (finalAtkSpeed > 0) ? (1f / finalAtkSpeed) : 1f;

            this.currentHp = maxHp * hpRatio;
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        SkillManager.OnMinionStatsChanged -= ApplyGlobalBuffs;

        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        lifetimeCts = null;

        // 로드 취소 및 핸들 해제
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = null;

        if (_animHandle.IsValid())
        {
            Addressables.Release(_animHandle);
            _animHandle = default;
        }
    }

    public override void ManualUpdate(float deltaTime)
    {
        base.ManualUpdate(deltaTime);
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver))
        {
            // [PERF] 이미 0이면 매 프레임 할당 스킵
            if (rb != null && rb.velocity != Vector2.zero) rb.velocity = Vector2.zero;
            return;
        }

        UpdateAnimation();

        if (Time.time > spawnTime + lifeTime)
        {
            Die();
        }

        // [OPTIMIZED] 공격 로직 (업데이트 루프에서 쿨타임 체크 및 거리 기반)
        if (currentTarget != null)
        {
            float sqrDist = (currentTarget.position - transform.position).sqrMagnitude;
            if (sqrDist <= attackRange * attackRange)
            {
                if (attackRange > 1.8f)
                    TryRangedAttack();
                else
                    TryMeleeAttack();
            }
        }
    }

    public override void ManualFixedUpdate(float fixedDeltaTime)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver))
        {
            if (rb != null && rb.velocity != Vector2.zero) rb.velocity = Vector2.zero;
            return;
        }

        ChaseTarget();
    }

    private void UpdateAnimation()
    {
        if (unitAnimator == null) return;
        
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        if (rb.velocity.x > 0.01f) unitSprite.flipX = false;
        else if (rb.velocity.x < -0.01f) unitSprite.flipX = true;
    }

    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            if (isDead) break;

            // [PERF] 텔레포트 체크: ManualUpdate(매 프레임)에서 이곳(300ms)으로 이전
            // 미니언 수십 마리 기준 초당 연산 횟수를 ~60회 → ~3.3회로 감소
            if (playerTransform != null)
            {
                float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
                if (sqrDistToPlayer > 144.0f) // 12.0f * 12.0f
                    transform.position = playerTransform.position + (Vector3)Random.insideUnitCircle * 2f;
            }

            if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
            {
                                GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, scanRange, sharedNearbyBuffer);
            }

            float closestSqrDist = scanRange * scanRange;
            UnitBase newTarget = null;

            for (int i = 0; i < sharedNearbyBuffer.Count; i++)
            {
                var unit = sharedNearbyBuffer[i];
                if (unit == null || unit.IsDead || unit is MinionAI || unit is PlayerController) continue;

                float sqrDist = (transform.position - unit.transform.position).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    newTarget = unit;
                }
            }

            currentTarget = (newTarget != null) ? newTarget.transform : null;
            await UniTask.Delay(300, cancellationToken: token).SuppressCancellationThrow();
        }
    }

    private void ChaseTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            if (playerTransform != null)
            {
                float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
                if (sqrDistToPlayer > 4.0f) // 2.0f * 2.0f
                {
                    Vector2 returnDir = (playerTransform.position - transform.position).normalized;
                    rb.velocity = returnDir * moveSpeed;
                }
                else rb.velocity = Vector2.zero;
            }
            else rb.velocity = Vector2.zero;
            
            return;
        }

        float distSqr = (currentTarget.position - transform.position).sqrMagnitude;
        float stopRange = attackRange * 0.8f; // 사거리 80% 지점에서 정지하여 공격 준비

        if (distSqr > stopRange * stopRange)
        {
            Vector2 direction = (currentTarget.position - transform.position).normalized;
            rb.velocity = direction * moveSpeed;
        }
        else
        {
            rb.velocity = Vector2.zero; // 공격 사거리 내에서는 정지
        }
    }

    public void AddLifeTime(float amount)
    {
        spawnTime += amount;
    }

    // [BloodFrenzy] HP 50% 미만 시 공속 버프를 반영한 실제 쿨타임 반환
    // 공격 시도 시점에만 호출 → 매 프레임 비용 없음
    private float GetEffectiveCooldown()
    {
        SkillManager sManager = GameManager.Instance?.skillManager;
        if (sManager != null && sManager.bloodFrenzyLevel > 0 && currentHp < maxHp * 0.5f)
        {
            float speedBonus = sManager.bloodFrenzyLevel * 0.1f; // lv1:10%, lv2:20%, lv3:30%
            return hitCooldown / (1f + speedBonus);
        }
        return hitCooldown;
    }







    private void TryRangedAttack()
    {
        if (Time.time < lastHitTime + GetEffectiveCooldown()) return;
        if (currentTarget == null) return;

        // [ACTION] 다중 발사 로직 처리 (1 + 추가 발수)
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            SkillManager sManager = GameManager.Instance.skillManager;
            int totalProjectiles = 1 + (sManager != null ? sManager.globalExtraProjectiles : 0);
            
            Vector2 baseDir = (currentTarget.position - transform.position).normalized;
            float spreadAngle = 10f; // 발사체 사이의 간격 (도)
            float startAngle = -((totalProjectiles - 1) * spreadAngle) / 2f;

            for (int i = 0; i < totalProjectiles; i++)
            {
                float currentAngle = startAngle + (i * spreadAngle);
                Vector2 finalDir = Quaternion.Euler(0, 0, currentAngle) * baseDir;

                GameObject projGo = GameManager.Instance.poolManager.Get("BoneProjectile", transform.position, Quaternion.identity);
                if (projGo != null && projGo.TryGetComponent<BoneProjectile>(out var proj))
                {
                    proj.Fire(finalDir, attackDamage, this); // 'this' (본체)를 주인으로 전달
                }
            }
        }
    }

    private void TryMeleeAttack()
    {
        if (Time.time < lastHitTime + GetEffectiveCooldown()) return;
        if (currentTarget == null) return;

        if (currentTarget.TryGetComponent(out IDamageable targetUnit))
        {
            SkillManager sManager = GameManager.Instance.skillManager;
            float finalDamage = attackDamage;

            if (unitAnimator != null) unitAnimator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
            targetUnit.ApplyDamage(finalDamage, this);

            if (GameManager.Instance != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxNormalAttackCraw);
            }
            
            // [NEW] 흡혈(Vampiric Teeth) 효과 적용
            if (sManager != null && sManager.vampiricChance > 0f)
            {
                if (Random.value < sManager.vampiricChance)
                {
                    float healAmount = sManager.vampiricHealAmount;
                    this.currentHp = Mathf.Min(currentHp + healAmount, maxHp);
                }
            }

            if (targetUnit.IsDead) AddLifeTime(0.2f);
            lastHitTime = Time.time;
            if (sManager != null) sManager.ApplyAttackEffects(targetUnit.Unit);
        }
    }

    protected override void Die()
    {
        base.Die();
        rb.velocity = Vector2.zero;
        
        SkillManager sManager = GameManager.Instance.skillManager;
        if (sManager != null && sManager.minionExplosionDamage > 0f)
        {
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, 2f, explosionBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D h = explosionBuffer[i];
                if (h != null && h.CompareTag("Enemy"))
                {
                    if (h.TryGetComponent(out IDamageable targetUnit))
                    {
                        targetUnit.ApplyDamage(sManager.minionExplosionDamage, this);
                    }
                }
            }
        }
        
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            // [BUG-FIX] 하드코딩된 "Minion" 태그 대신 SO의 minionTag를 사용하여 개별 풀에 정확히 반납
            // 기본 미니언(SO 없음)은 GameManager.minionPoolTag("Minion")로 fallback
            string poolTag = (minionData != null && !string.IsNullOrEmpty(minionData.minionTag))
                ? minionData.minionTag
                : GameManager.Instance.unitManager?.minionPoolTag ?? "Minion";
            GameManager.Instance.poolManager.Release(poolTag, gameObject);
        }
        else
            Destroy(gameObject);
    }
}
}
