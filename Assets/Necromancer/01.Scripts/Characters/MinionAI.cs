
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
            this.maxHp = 50f * sManager.globalMinionHpBonusRatio;
            this.moveSpeed = 3f * sManager.globalMinionSpeedBonusRatio;
            this.attackDamage = 15f * sManager.globalMinionDamageBonusRatio;
        }
    }

    private void OnDisable()
    {
        SkillManager.OnMinionStatsChanged -= ApplyGlobalBuffs;
        currentTarget = null; // [STABILITY] 참조 초기화

        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        lifetimeCts = null;
    }

    protected override void Update()
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) 
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }

        base.Update();
        UpdateAnimation();

        if (playerTransform != null)
        {
            float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
            if (sqrDistToPlayer > 144.0f) // 12.0f * 12.0f (18m -> 12m 단축)
            {
                transform.position = playerTransform.position + (Vector3)Random.insideUnitCircle * 2f;
            }
        }

        if (Time.time > spawnTime + lifeTime)
        {
            Die();
        }
    }

    private void UpdateAnimation()
    {
        if (unitAnimator == null) return;
        
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        if (rb.velocity.x > 0.01f) unitSprite.flipX = false;
        else if (rb.velocity.x < -0.01f) unitSprite.flipX = true;
    }

    private void FixedUpdate()
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) 
        {
             if (rb != null) rb.velocity = Vector2.zero;
             return;
        }
        
        ChaseTarget();
    }

    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        // [JITTERING] 모든 미니언이 동시에 스캔하지 않도록 초기 대기 시간에 무작위 오프셋 부여
        await UniTask.Delay(System.TimeSpan.FromSeconds(Random.Range(0f, targetScanRate)), cancellationToken: token);

        while (!isDead && !token.IsCancellationRequested)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) break;

            // 1. 플레이어와 너무 멀어지면 타겟팅 중단 (복귀 우선)
            if (playerTransform != null)
            {
                float sqrDistToPlayer = (playerTransform.position - transform.position).sqrMagnitude;
                if (sqrDistToPlayer > 64.0f) // 8m (10m -> 8m 응집력 강화)
                {
                    currentTarget = null;
                    await UniTask.Delay(System.TimeSpan.FromSeconds(targetScanRate), cancellationToken: token);
                    continue;
                }
            }

            // 2. [OPTIMIZATION] Physics2D.OverlapCircleNonAlloc 도입 (O(N*M) -> O(Nearby))
            // 스캔 범위를 12f -> 8f로 조정하여 마스터의 시야 범위(Cohesion)를 유지합니다.
            int count = Physics2D.OverlapCircleNonAlloc(transform.position, 8.0f, scanBuffer, enemyLayer);
            
            float minSqrDist = Mathf.Infinity;
            Transform bestTarget = null;

            for (int i = 0; i < count; i++)
            {
                Collider2D col = scanBuffer[i];
                if (col == null) continue;

                if (col.TryGetComponent(out IDamageable enemy))
                {
                    if (enemy.IsDead) continue;

                    // [Cohesion] 플레이어로부터 너무 멀리 떨어진(8m 이상) 적은 타겟팅에서 제외
                    if (playerTransform != null)
                    {
                        float sqrDistFromPlayer = (playerTransform.position - col.transform.position).sqrMagnitude;
                        if (sqrDistFromPlayer > 64.0f) continue;
                    }

                    float sqrDist = (transform.position - col.transform.position).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestTarget = col.transform;
                    }
                }
            }
            
            currentTarget = bestTarget;

            // [JITTERING] 스캔 주기에 미세한 변동을 주어 CPU 부하를 더욱 분산시킵니다.
            float jitteredRate = targetScanRate * Random.Range(0.9f, 1.1f);
            await UniTask.Delay(System.TimeSpan.FromSeconds(jitteredRate), cancellationToken: token);
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
                targetUnit.ApplyDamage(finalDamage);
                lastHitTime = Time.time;
                
                if (sManager != null)
                {
                    // [Refactoring] UnitBase에서 EnemyAI로 캐스팅 대신 데이터 기반 접근 권장
                    EnemyAI enemyScript = targetUnit.Unit as EnemyAI;
                    if (enemyScript != null)
                    {
                        if (sManager.hasToxicBlade) enemyScript.ApplyPoison(3f, 2f);
                        if (sManager.hasFrostWeapon) enemyScript.ApplyFrost(2f, 0.3f);
                        if (sManager.hasCursedStigma) enemyScript.AddStigmaStack();
                    }
                }
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
                        targetUnit.ApplyDamage(sManager.minionExplosionDamage);
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
