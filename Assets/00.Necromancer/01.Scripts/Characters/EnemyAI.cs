
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
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

    private AsyncOperationHandle<RuntimeAnimatorController> _animHandle;
    private CancellationTokenSource _loadCts;

    // --- [최적화 변수] ---
    private int separationOffset;
    private Vector2 cachedSeparationDir;
    [SerializeField] private LayerMask enemyLayer;

    [Header("Targeting Settings")]
    private UnitBase currentTarget; // 실질적인 추격/공격 대상
    private const float MINION_SCAN_RANGE = 4.0f; // 일반 몹이 한눈 팔 범위
        private static readonly List<UnitBase> sharedNearbyBuffer = new List<UnitBase>(32);
    public float attackRange = 1.2f;
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
        if (!hasCountedDeath && GameManager.Instance != null && GameManager.Instance.waveManager != null)
        {
            GameManager.Instance.waveManager.OnEnemyDied();
            hasCountedDeath = true;
        }

        currentTarget = null;

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

    public void Setup(EnemyData enemyData)
    {
        this.data = enemyData;
        if (data == null) return;

        // 능력치 설정 (즉시)
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
        this.hasCountedDeath = false;
        this.lastHitTime = 0f;

        if (unitSprite != null) unitSprite.color = Color.white;

        // 애니메이터: 비동기 로드 시작
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        LoadAnimatorAsync(_loadCts.Token).Forget();
    }

    private async UniTaskVoid LoadAnimatorAsync(CancellationToken ct)
    {
        if (data?.animatorController == null || unitAnimator == null) return;

        if (_animHandle.IsValid())
        {
            // 완료됐고 같은 컨트롤러면 재로드 불필요
            if (_animHandle.IsDone && _animHandle.Status == AsyncOperationStatus.Succeeded
                && unitAnimator.runtimeAnimatorController == _animHandle.Result)
                return;

            Addressables.Release(_animHandle);
            _animHandle = default;
        }

        // AssetReference.LoadAssetAsync 대신 RuntimeKey로 직접 로드:
        // 여러 적이 동일한 EnemyData SO를 공유할 때 AssetReference 내부 handle 충돌을 방지합니다.
        _animHandle = Addressables.LoadAssetAsync<RuntimeAnimatorController>(data.animatorController.RuntimeKey);

        while (!_animHandle.IsDone)
        {
            if (ct.IsCancellationRequested)
            {
                // 취소 시 인플라이트 핸들을 반드시 해제 (미해제 시 OnDisable과 이중 해제 충돌)
                if (_animHandle.IsValid())
                {
                    Addressables.Release(_animHandle);
                    _animHandle = default;
                }
                return;
            }
            await UniTask.Yield();
        }

        // while 종료 후 OnDisable이 먼저 실행돼 핸들이 해제됐을 수 있으므로 반드시 재확인
        if (!_animHandle.IsValid()) return;

        if (_animHandle.Status == AsyncOperationStatus.Succeeded && unitAnimator != null)
            unitAnimator.runtimeAnimatorController = _animHandle.Result;
        else
            Debug.LogWarning($"[EnemyAI] AnimatorController 로드 실패: {data.enemyID}");
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
            // [PERF] 이미 0이면 매 프레임 할당 스킵
            if (rb != null && rb.velocity != Vector2.zero) rb.velocity = Vector2.zero;
            return;
        }

        MoveWithSeparation();
        CheckAttack();
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
                                GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, separationRadius, sharedNearbyBuffer);
            }
            var neighbors = sharedNearbyBuffer;

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

    private void CheckAttack()
    {
        if (currentTarget == null) return;

        // [OPTIMIZED] 물리 트리거 대신 논리적 거리 연산으로 대체
        float sqrDist = (currentTarget.transform.position - transform.position).sqrMagnitude;
        float attackRange = 1.0f; // 적의 기본 공격 범위 (콜라이더 대체 반경)

        if (sqrDist <= attackRange * attackRange)
        {
            if (Time.time >= lastHitTime + hitCooldown)
            {
                if (currentTarget.gameObject.TryGetComponent(out IDamageable targetUnit))
                {
                    targetUnit.ApplyDamage(attackDamage, this);
                    lastHitTime = Time.time;

                    if (unitAnimator != null)
                    {
                        unitAnimator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
                    }
                }
            }
        }
    }

    public override void TakeDamage(float damage, UnitBase attacker = null)
    {
        if (isDead) return;
        base.TakeDamage(damage, attacker);

        if (GameManager.Instance != null && GameManager.Instance.feedbackManager != null && gameObject.activeInHierarchy)
        {
            float duration = (data != null && data.isElite) ? 0.12f : 0.05f;
            float magnitude = (data != null && data.isElite) ? 0.15f : 0.08f;
            GameManager.Instance.feedbackManager.ShakeCamera(duration, magnitude);
        }
    }

    
    /// <summary>
    /// [LOGIC] 사망 시 정수 드랍 로직 (Master's Directive)
    /// </summary>
    private void HandleEssenceDrop()
    {
        if (GameManager.Instance == null || GameManager.Instance.currentStage == null) return;

        int stageID = GameManager.Instance.currentStage.stageID;
        float baseDropRate = GameManager.Instance.currentStage.essenceDropRate;
        // 스테이지 1~10: 보너스 0%, 11~20: +1%, 21~30: +2% ...
        float stageBonus = Mathf.FloorToInt((stageID - 1) / 10f) * 0.01f;
        float finalDropRate = baseDropRate + stageBonus;

        if (Random.value >= finalDropRate) return;

        // 가중치 기반 전역 드롭 (Bronze 70 / Silver 25 / Gold 5)
        var minionData = GameManager.Instance.unitManager?.GetRandomMinionDataWeighted();
        if (minionData == null) return;

        if (GameManager.Instance.poolManager != null)
        {
            GameObject essenceObj = GameManager.Instance.poolManager.Get("Essence", transform.position, Quaternion.identity);
            if (essenceObj != null && essenceObj.TryGetComponent(out EssenceItem essenceItem))
                essenceItem.Setup(minionData);
        }
    }
protected override void Die()
    {
        // 웨이브 카운트 즉시 반영 (애니메이션 지연 전에 처리)
        if (!hasCountedDeath && GameManager.Instance?.waveManager != null)
        {
            GameManager.Instance.waveManager.OnEnemyDied();
            hasCountedDeath = true;
        }

        base.Die(); // isDead=true, 콜라이더 비활성화, 사망 애니메이션 + DieSequenceAsync 시작

        rb.velocity = Vector2.zero;
        HandleEssenceDrop();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.poolManager?.Get("ExpGem", transform.position, Quaternion.identity);
            GameManager.Instance.unitManager?.TryReviveAsMinion(transform.position);
        }
    }

    protected override void OnDeathComplete()
    {
        if (GameManager.Instance?.poolManager != null)
            GameManager.Instance.poolManager.Release("Enemy", gameObject);
        else
            Destroy(gameObject);
    }

    /// <summary>
    /// [HYBRID TARGETING] 미니언을 최우선으로, 없으면 플레이어를 타겟팅하는 비동기 루프
    /// </summary>
    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        // 초기 지터링 부여 (CPU 부하 분산)
        await UniTask.Delay(System.TimeSpan.FromSeconds(Random.Range(0f, 0.5f)), cancellationToken: token).SuppressCancellationThrow();

        while (!isDead && !token.IsCancellationRequested && gameObject.activeInHierarchy)
        {
            // 1. 보스 또는 특정 스나이퍼형은 무조건 플레이어 고정 (전략적 난이도 유지)
            if (data != null && (data.isElite || data.isSniper))
            {
                currentTarget = (UnitBase)GameManager.Instance.playerController;
            }
            else
            {
                // 2. 일반형: 우선순위가 높은 대상을 스캔 (O(1) 격자 활용)
                if (GameManager.Instance != null && GameManager.Instance.unitManager != null)
                {
                                        GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(transform.position, MINION_SCAN_RANGE, sharedNearbyBuffer);
                }

                UnitBase bestTarget = null;
                int highestPriority = -1;
                float minSqrDist = Mathf.Infinity;

                for (int i = 0; i < sharedNearbyBuffer.Count; i++)
                {
                    var unit = sharedNearbyBuffer[i];
                    if (unit == null || unit.IsDead) continue;

                    // [LOGIC] 우선순위 결정 (미니언은 데이터 기반, 플레이어는 고정값 1)
                    int priority = 0;
                    if (unit is MinionAI minion) priority = minion.TargetPriority;
                    else if (unit is PlayerController) priority = 1; // 플레이어는 최후의 타겟

                    if (priority <= 0) continue;

                    float sqrDist = ((Vector2)transform.position - (Vector2)unit.transform.position).sqrMagnitude;

                    // [DECISION] 1순위: 우선순위가 높은가? 2순위: 더 가까운가?
                    if (priority > highestPriority)
                    {
                        highestPriority = priority;
                        bestTarget = unit;
                        minSqrDist = sqrDist;
                    }
                    else if (priority == highestPriority && sqrDist < minSqrDist)
                    {
                        bestTarget = unit;
                        minSqrDist = sqrDist;
                    }
                }
                
                // [FINAL] 최적의 타겟이 발견되지 않으면 기본적으로 플레이어 추격 (fallback)
                currentTarget = bestTarget ?? (UnitBase)GameManager.Instance.playerController;
            }

            // 0.4초 간격으로 타겟 갱신 (성능 확보)
            bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(0.4f), cancellationToken: token).SuppressCancellationThrow();
            if (isCancelled) break;
        }
    }
}
}
