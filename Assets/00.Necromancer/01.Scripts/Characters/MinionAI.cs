
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    [Header("Minion Settings")]
    [Tooltip("적에게 닿았을 때 입히는 데미지")]
    public float attackDamage = 15f;
    
    [Tooltip("소멸하기 전까지 유지되는 생존 시간 (초 단위)")]
    public float lifeTime = 10f;
    
    [Tooltip("타겟을 새로 갱신하는 주기 (최적화용)")]
    public float targetScanRate = 0.5f;
    
    [Tooltip("공격 쿨타임")]
    public float hitCooldown = 0.5f;


    [Header("Inspector References (Zero-Search)")]
    [SerializeField] private Rigidbody2D rb;

    private Transform currentTarget;
    private float lastHitTime;
    private float spawnTime;
    private Transform playerTransform;
    private CancellationTokenSource lifetimeCts;
    
    // [OPTIMIZATION] 물리 쿼리용 버퍼 및 레이어 마스크
    private static readonly Collider2D[] scanBuffer = new Collider2D[10];
    private static readonly Collider2D[] explosionBuffer = new Collider2D[20];
    [SerializeField] private LayerMask enemyLayer;

        // [OPTIMIZATION] 타겟팅용 버퍼
    private float scanRange = 5.0f;
    private List<UnitBase> nearbyBuffer = new List<UnitBase>(16);
protected override void Awake()
    {
        // [Pure Inspector] 모든 참조를 미리 인스펙터에서 연결했으므로 런타임 검색 비용 0ms
        // [UnitBase] 부모의 Awake(currentHp 초기화 등)만 호출
        base.Awake();
        
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
    }

    protected override void OnEnable()
    {
        // 1. 전역 버프 적용 및 이벤트 구독
        ApplyGlobalBuffs();
        SkillManager.OnMinionStatsChanged += ApplyGlobalBuffs;
        
        // 2. 그 다음 기본 UnitBase.OnEnable()을 호출
        base.OnEnable();
        
        spawnTime = Time.time;
        currentTarget = null;
        lifetimeCts = new CancellationTokenSource();

        // 플레이어 위치 캐싱
        if (GameManager.Instance != null) playerTransform = GameManager.Instance.playerTransform;
        
        // [STABILITY] 오브젝트 비활성화 시 즉시 중단되도록 lifetimeCts.Token 연결
        ScanForTargetAsync(lifetimeCts.Token).Forget();
    }

    private void ApplyGlobalBuffs()
    {
        if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
        {
            SkillManager sManager = GameManager.Instance.skillManager;
            
            // [DATA INTEGRITY] HP Ratio Preservation (전역 버프 적용 시 비율 유지)
            float oldMaxHp = maxHp;
            float hpRatio = (oldMaxHp > 0) ? currentHp / oldMaxHp : 1f;

            this.maxHp = 50f * sManager.globalMinionHpBonusRatio;
            this.moveSpeed = 3f * sManager.globalMinionSpeedBonusRatio;
            this.attackDamage = 15f * sManager.globalMinionDamageBonusRatio;

            // 새로운 최대 체력에 맞춰 현재 체력 보정
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
    }

    public override void ManualUpdate(float deltaTime)
    {
        base.ManualUpdate(deltaTime);
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) 
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        UpdateAnimation();

        if (playerTransform != null)
        {
            float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
            if (sqrDistToPlayer > 144.0f) // 12.0f * 12.0f
            {
                transform.position = playerTransform.position + (Vector3)Random.insideUnitCircle * 2f;
            }
        }

        if (Time.time > spawnTime + lifeTime)
        {
            Die();
        }
    }

    public override void ManualFixedUpdate(float fixedDeltaTime)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) 
        {
             if (rb != null) rb.velocity = Vector2.zero;
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

            // [GC-FREE] GetNearbyUnitsNonAlloc을 활용하여 힙 할당 없이 주변 유닛 스캔
            if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
            {
                GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, scanRange, nearbyBuffer);
            }

            float closestSqrDist = scanRange * scanRange;
            UnitBase newTarget = null;

            for (int i = 0; i < nearbyBuffer.Count; i++)
            {
                var unit = nearbyBuffer[i];
                // 미니언 자신이나 플레이어/동료 미니언을 제외한 '적'만 타겟팅 (is PlayerController check)
                if (unit == null || unit.IsDead || unit is MinionAI || unit is PlayerController) continue;

                float sqrDist = (transform.position - unit.transform.position).sqrMagnitude;
                if (sqrDist < closestSqrDist)
                {
                    closestSqrDist = sqrDist;
                    newTarget = unit;
                }
            }

            // [STABILITY] Transform 조준을 위해 UnitBase에서 transform 추출
            currentTarget = (newTarget != null) ? newTarget.transform : null;

            // 성능 부하 분산을 위한 대기
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

        Vector2 direction = (currentTarget.position - transform.position).normalized;
        rb.velocity = direction * moveSpeed;
    }

    public void AddLifeTime(float amount)
    {
        // 생존 시간을 연장하기 위해 스폰 시점을 과거로 밉니다.
        spawnTime += amount;
    }

    /// <summary>
    /// 트리거 충돌 판정 (적 타격 로직) - 뱀서류 군집 AI 최적화
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // [STABILITY] 스치기 불사 해결: 처음 닿는 순간에는 프레임 필터링 없이 무조건 타격 시도
        TryAttack(collision, true);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 머물러 있을 때만 프레임 최적화 적용
        TryAttack(collision, false);
    }

    private void TryAttack(Collider2D collision, bool isInitialContact)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;

        // [OPTIMIZATION] 첫 타격이 아닐 때만 1/5 프레임 최적화
        if (!isInitialContact && Time.frameCount % 5 != 0) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 적인지 태그로 확인
        if (collision.CompareTag("Enemy"))
        {
            // [Zero-Search] GetComponent<UnitBase> 대신 인터페이스 레이어 사용 (성능 최적화)
            if (collision.TryGetComponent(out IDamageable targetUnit))
            {
                SkillManager sManager = GameManager.Instance.skillManager;
                float finalDamage = attackDamage;

                if (unitAnimator != null) unitAnimator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
                targetUnit.ApplyDamage(finalDamage, this);

                // [SOUND] 일반 공격(미니언 공격) 효과음 재생
                if (GameManager.Instance != null && GameManager.Instance.Sound != null)
                {
                    // [IMPORTANT] 현재는 근접 공격 미니언 로직이므로 NormalAttackCraw 재생
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxNormalAttackCraw);
                }
                
                // [Kill-to-Live] 적의 남은 체력을 체크하여 처치 시 수명 회복 (0.2s)
                if (targetUnit.IsDead)
                {
                    AddLifeTime(0.2f);
                }

                lastHitTime = Time.time;
                
                // [REGISTRY PATTERN] AI는 구체적인 스킬 효과를 몰라도 되며, 
                // SkillManager가 등록된 모든 공격 효과를 일괄적으로 적용합니다 (Agnostic).
                if (sManager != null) sManager.ApplyAttackEffects(targetUnit.Unit);
            }
        }
    }

    protected override void Die()
    {
        base.Die();
        rb.velocity = Vector2.zero;
        
        SkillManager sManager = GameManager.Instance.skillManager;
        if (sManager != null && sManager.minionExplosionDamage > 0f)
        {
            // [GC-FIX] OverlapCircleAll 대신 NonAlloc 사용
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, 2f, explosionBuffer);
            for (int i = 0; i < count; i++)
            {
                Collider2D h = explosionBuffer[i];
                if (h == null) continue;

                if (h.CompareTag("Enemy"))
                {
                    // [Zero-Search] 대규모 폭발 시 루프 내 검색 비용 제거
                    if (h.TryGetComponent(out IDamageable targetUnit))
                    {
                        targetUnit.ApplyDamage(sManager.minionExplosionDamage, this);
                    }
                }
            }
        }
        
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            GameManager.Instance.poolManager.Release("Minion", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
}
