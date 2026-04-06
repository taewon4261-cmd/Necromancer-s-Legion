
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using System.Collections;

namespace Necromancer
{
/// <summary>
/// 적(인간 기사/농부) 유닛의 공통 AI
/// 플레이어를 무조건 쫓아가며, 사망 시 조상 클래스(UnitBase)의 Die()를 오버라이드하여 부활 로직 호출
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyAI : UnitBase
{
    [Header("Enemy Data (SO)")]
    [Tooltip("에디터에서 미리 설정해둔 적의 스탯 데이터를 할당합니다.")]
    public EnemyData data;

    [Header("Enemy Settings (Legacy)")]
    [Tooltip("플레이어에게 닿았을 때 입히는 데미지")]
    public float attackDamage = 10f;
    
    [Tooltip("공격 쿨타임 (연속 타격 방지)")]
    public float hitCooldown = 0.5f;

    [Header("Swarm AI Settings")]
    public float separationRadius = 0.8f;
    public float separationStrength = 5f;
    public float movementSmoothTime = 0.15f;
    private Vector2 currentVelocity;

    [Header("Inspector References (Zero-Search)")]
    [SerializeField] private Rigidbody2D rb;
    private float lastHitTime;
    private CancellationTokenSource lifetimeCts;

    // --- [최적화 변수] ---
    private int separationOffset;
    private Vector2 cachedSeparationDir;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Targeting Settings")]
    private UnitBase currentTarget; // 실질적인 추격/공격 대상
    private const float MINION_SCAN_RANGE = 4.0f; // 일반 몹이 한눈 팔 범위
    private List<UnitBase> nearbyBuffer = new List<UnitBase>(16);
    private int stigmaStacks;
    private bool hasCountedDeath;

    // --- [스킬 연동: Modifier 패턴으로 대체됨] ---

    protected override void Awake()
    {
        base.Awake();
        // [Pure Inspector] rb는 인스펙터에서 사전에 할당되었습니다.
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // [OPTIMIZATION] 모든 적이 동시에 연산하지 않도록 확정적 오프셋 부여 (1/10 확률 분산)
        separationOffset = Mathf.Abs(gameObject.GetInstanceID()) % 10;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        stigmaStacks = 0;
        hasCountedDeath = false; // [NEW] 가드 초기화
        lifetimeCts = new CancellationTokenSource();
        
        if (GameManager.Instance != null && GameManager.Instance.playerController != null)
        {
            currentTarget = (UnitBase)GameManager.Instance.playerController; // 초기화
        }

        // [ENRAGE] 스폰 20초 후 광폭화 루틴 시작 (오브젝트 비활성화 시 즉시 중단)
        EnrageAfterDelayAsync(lifetimeCts.Token).Forget();
        
        // [HYBRID TARGETING] 타겟 스캔 루프 시작
        ScanForTargetAsync(lifetimeCts.Token).Forget();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        // [OPTIMIZATION] O(N) 리스트 제거 대신 O(1) 카운트 감소
        if (!hasCountedDeath && GameManager.Instance != null && GameManager.Instance.waveManager != null)
        {
            GameManager.Instance.waveManager.OnEnemyDied();
            hasCountedDeath = true; // 가드 활성화
        }

        currentTarget = null; // [STABILITY] 참조 초기화
        
        lifetimeCts?.Cancel();
        lifetimeCts?.Dispose();
        lifetimeCts = null;
    }

    public void Setup(EnemyData enemyData)
    {
        this.data = enemyData;
        if (data == null) return;

        float hpMult = 1f;
        float dmgMult = 1f;

        if (GameManager.Instance != null && GameManager.Instance.Combat != null)
        {
            hpMult = GameManager.Instance.Combat.enemyHpMultiplier;
            dmgMult = GameManager.Instance.Combat.enemyDamageMultiplier;
        }

        this.maxHp = data.maxHp * hpMult;
        this.attackDamage = data.attackDamage * dmgMult;
        this.moveSpeed = data.moveSpeed;

        this.currentHp = maxHp;
        this.isDead = false;
        this.hasCountedDeath = false; // [NEW] 셋업 시 가드 리셋
        this.lastHitTime = 0f;

        if (unitSprite != null) unitSprite.color = Color.white;
    }

    private async UniTaskVoid EnrageAfterDelayAsync(CancellationToken token)
    {
        bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(20.0f), cancellationToken: token).SuppressCancellationThrow();
        
        if (!isCancelled && !isDead && gameObject.activeInHierarchy)
        {
            moveSpeed *= 1.5f;
            hitCooldown *= 0.7f;

            if (unitSprite != null)
            {
                unitSprite.color = Color.red;
            }

            Debug.Log($"<color=red>[EnemyAI]</color> {gameObject.name} ENRAGED! (20s reached)");
        }
    }

    // [MODIFIER PATTERN] 기존 비동기 스킬 로직은 PoisonModifier 등으로 이관될 예정이므로 제거 가능하나
    // 현재 호환성을 위해 유지하거나 Modifier 주입 방식으로 리팩토링합니다.
    // (지시서에 따라 UnitBase.AddModifier를 사용하는 방향으로 선회)

    public override void ManualUpdate(float deltaTime)
    {
        base.ManualUpdate(deltaTime);
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;
        
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (unitAnimator == null) return;
        
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        unitAnimator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        if (rb.velocity.x > 0.01f) unitSprite.flipX = false;
        else if (rb.velocity.x < -0.01f) unitSprite.flipX = true;
    }

    public override void ManualFixedUpdate(float fixedDeltaTime)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) 
        {
            if (rb != null) rb.velocity = Vector2.zero;
            return;
        }
        
        MoveWithSeparation();
    }

    private void MoveWithSeparation()
    {
        if (currentTarget == null || currentTarget.transform == null) return;

        Vector2 stalkDir = ((Vector2)currentTarget.transform.position - (Vector2)transform.position).normalized;
        Vector2 separationDir = cachedSeparationDir;

        // [OPTIMIZATION] Grid Spatial Partitioning 사용 (OverlapCircleNonAlloc 대체)
        if ((Time.frameCount + separationOffset) % 10 == 0)
        {
            separationDir = Vector2.zero;
            int neighborCount = 0;
            float sqrRadius = separationRadius * separationRadius;

            // UnitManager의 격자 시스템으로 주변 유닛만 O(1)에 가깝게 추출 (GC Free)
            if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
            {
                GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, separationRadius, nearbyBuffer);
            }
            var neighbors = nearbyBuffer;

            for (int i = 0; i < neighbors.Count; i++)
            {
                var neighbor = neighbors[i];
                if (neighbor == null || neighbor == this || neighbor is PlayerController) continue;

                Vector2 diff = (Vector2)transform.position - (Vector2)neighbor.transform.position;
                float sqrDist = diff.sqrMagnitude;

                if (sqrDist < sqrRadius && sqrDist > 0.0001f)
                {
                    separationDir += diff.normalized / Mathf.Sqrt(sqrDist);
                    neighborCount++;
                }
            }
            
            if (neighborCount > 0) separationDir /= neighborCount;
            cachedSeparationDir = separationDir;
        }

        Vector2 targetDir = (stalkDir + separationDir * separationStrength).normalized;
        Vector2 targetVelocity = targetDir * moveSpeed;
        rb.velocity = Vector2.SmoothDamp(rb.velocity, targetVelocity, ref currentVelocity, movementSmoothTime);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        // [STABILITY] 스치기 불사 해결: 처음 닿는 순간에는 프레임 필터링 없이 무조건 타격 시도
        TryAttack(collision, true);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 머물러 있는 동안에는 성능을 위해 5프레임마다 한 번씩만 데미지 시도
        TryAttack(collision, false);
    }

    private void TryAttack(Collider2D collision, bool isInitialContact)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;
        
        // [OPTIMIZATION] 첫 타격이 아닐 때만 5프레임 최적화 적용
        // 디버깅 로그는 유지하되, 프레임 필터링은 다시 복구하여 성능 확보
        if (!isInitialContact && Time.frameCount % 5 != 0) return;
        
        if (Time.time < lastHitTime + hitCooldown) return;

        if (collision.CompareTag("Player") || collision.CompareTag("Minion"))
        {
            // [Zero-Search] GetComponent<UnitBase> 대신 인터페이스 레이어 사용 (O(1) 접근)
            if (collision.TryGetComponent(out IDamageable targetUnit))
            {
                if (unitAnimator != null) unitAnimator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
                targetUnit.ApplyDamage(attackDamage);
                lastHitTime = Time.time;
                
                // 디버그 로그 (성공 시만 출력하여 가독성 확보)
                Debug.Log($"[HitSuccess] {gameObject.name} -> {collision.name} (isInitial: {isInitialContact}, Damage: {attackDamage})");
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;
        base.TakeDamage(damage);

        if (GameManager.Instance != null && GameManager.Instance.feedbackManager != null && gameObject.activeInHierarchy)
        {
            float duration = (data != null && data.isElite) ? 0.12f : 0.05f;
            float magnitude = (data != null && data.isElite) ? 0.15f : 0.08f;
            GameManager.Instance.feedbackManager.ShakeCamera(duration, magnitude);
        }
    }

    protected override void Die()
    {
        base.Die();
        rb.velocity = Vector2.zero;
        
        if (GameManager.Instance != null)
        {
            if (GameManager.Instance.poolManager != null)
            {
                GameManager.Instance.poolManager.Get("ExpGem", transform.position, Quaternion.identity);
            }
            GameManager.Instance.TryReviveAsMinion(transform.position);
        }

        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            GameManager.Instance.poolManager.Release("Enemy", gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// [HYBRID TARGETING] 주변 미니언 유무에 따라 타겟을 결정하는 비동기 루프
    /// </summary>
    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        // 초기 지터링 부여 (CPU 부하 분산)
        await UniTask.Delay(System.TimeSpan.FromSeconds(Random.Range(0f, 0.5f)), cancellationToken: token).SuppressCancellationThrow();

        while (!isDead && !token.IsCancellationRequested)
        {
            // 1. 스나이퍼형 또는 보스는 무조건 플레이어 고정
            if (data != null && (data.isSniper || data.isElite))
            {
                currentTarget = (UnitBase)GameManager.Instance.playerController;
            }
            else
            {
                // 2. 일반형: UnitManager 격자 시스템 활용 (O(1)에 가까운 탐색)
                if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
                {
                    GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, MINION_SCAN_RANGE, nearbyBuffer);
                }

                UnitBase closestMinion = null;
                float minSqrDist = Mathf.Infinity;

                for (int i = 0; i < nearbyBuffer.Count; i++)
                {
                    // [IMPORTANT] MinionAI 타입만 타겟팅 (동료 Enemy는 제외)
                    if (nearbyBuffer[i] is MinionAI minion && !minion.IsDead)
                    {
                        float sqrDist = ((Vector2)transform.position - (Vector2)minion.transform.position).sqrMagnitude;
                        if (sqrDist < minSqrDist)
                        {
                            minSqrDist = sqrDist;
                            closestMinion = minion;
                        }
                    }
                }
                
                // 미니언 있으면 미니언, 없으면 플레이어
                currentTarget = closestMinion ?? (UnitBase)GameManager.Instance.playerController;
            }

            // 0.4초 간격으로 스캔 (성능 확보)
            bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f), cancellationToken: token).SuppressCancellationThrow();
            if (isCancelled) break;
        }
    }
}
}
