
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

    private Transform targetPlayer;
    private Rigidbody2D rb;
    private float lastHitTime;
    private CancellationTokenSource enrageCts;

    // --- [최적화 변수] ---
    private int separationOffset;
    private Vector2 cachedSeparationDir;

    // --- [스킬 연동: 상태이상 디버프 변수들] ---
    private float originalMoveSpeed;
    private int stigmaStacks = 0;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
        
        // [STABILITY] 스치기 불사 및 터널링 방지: 고속 이동 시 정확한 판정을 위해 연속 충돌 감지 활성화
        if (rb != null) rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // [OPTIMIZATION] 모든 적이 동시에 연산하지 않도록 확정적 오프셋 부여 (1/10 확률 분산)
        separationOffset = Mathf.Abs(gameObject.GetInstanceID()) % 10;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        
        stigmaStacks = 0;

        // WaveManager에 실시간 적 목록 등록 (성능 최적화용)
        if (GameManager.Instance != null && GameManager.Instance.waveManager != null)
        {
            GameManager.Instance.waveManager.activeEnemies.Add(this);
        }
        
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            targetPlayer = GameManager.Instance.playerTransform;
        }

        // [ENRAGE] 스폰 20초 후 광폭화 루틴 시작 (오브젝트 파괴 시 즉시 중단)
        EnrageAfterDelayAsync(gameObject.GetCancellationTokenOnDestroy()).Forget();
    }

    protected virtual void OnDisable()
    {
        // WaveManager에서 목록 제거 (성능 최적화용)
        if (GameManager.Instance != null && GameManager.Instance.waveManager != null)
        {
            GameManager.Instance.waveManager.activeEnemies.Remove(this);
        }

        enrageCts?.Cancel();
        enrageCts?.Dispose();
        enrageCts = null;
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
        this.originalMoveSpeed = moveSpeed;

        this.currentHp = maxHp;
        this.isDead = false;
        this.lastHitTime = 0f;

        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    private async UniTaskVoid EnrageAfterDelayAsync(CancellationToken token)
    {
        bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(20.0f), cancellationToken: token).SuppressCancellationThrow();
        
        if (!isCancelled && !isDead && gameObject.activeInHierarchy)
        {
            moveSpeed *= 1.5f;
            hitCooldown *= 0.7f;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.red;
            }

            Debug.Log($"<color=red>[EnemyAI]</color> {gameObject.name} ENRAGED! (20s reached)");
        }
    }

    // --- [스킬 연동 로직 시작] ---
    public void ApplyPoison(float duration, float tickDamage)
    {
        if (!gameObject.activeInHierarchy || isDead) return;
        PoisonAsync(duration, tickDamage, gameObject.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid PoisonAsync(float duration, float tickDamage, CancellationToken token)
    {
        float timer = 0f;
        while (timer < duration && !isDead && !token.IsCancellationRequested)
        {
            TakeDamage(tickDamage);
            timer += 1f;
            bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(1f), cancellationToken: token).SuppressCancellationThrow();
            if (isCancelled) return;
        }
    }

    public void ApplyFrost(float duration, float slowdownRatio)
    {
        if (!gameObject.activeInHierarchy || isDead) return;
        FrostAsync(duration, slowdownRatio, gameObject.GetCancellationTokenOnDestroy()).Forget();
    }

    private async UniTaskVoid FrostAsync(float duration, float slowdownRatio, CancellationToken token)
    {
        moveSpeed = originalMoveSpeed * (1f - slowdownRatio);
        bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(duration), cancellationToken: token).SuppressCancellationThrow();
        if (!isCancelled && !isDead) moveSpeed = originalMoveSpeed;
    }

    public void AddStigmaStack()
    {
        stigmaStacks++;
        if (stigmaStacks >= 10)
        {
            stigmaStacks = 0; 
            TakeDamage(maxHp * 0.2f); 
        }
    }

    protected override void Update()
    {
        base.Update();
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;
        UpdateAnimation();
    }

    private void UpdateAnimation()
    {
        if (animator == null) return;
        
        bool isMoving = rb.velocity.sqrMagnitude > 0.01f;
        animator.SetBool(Necromancer.Systems.UIConstants.AnimParam_IsMoving, isMoving);

        if (rb.velocity.x > 0.01f) spriteRenderer.flipX = false;
        else if (rb.velocity.x < -0.01f) spriteRenderer.flipX = true;
    }

    private void FixedUpdate()
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
        if (targetPlayer == null) return;

        Vector2 stalkDir = (targetPlayer.position - transform.position).normalized;
        Vector2 separationDir = cachedSeparationDir;

        // [OPTIMIZATION] 분산 로직(N^2) 부하를 1/10로 감소
        // 각 유닛마다 다른 프레임에 계산하도록 dither 연산 적용
        if ((Time.frameCount + separationOffset) % 10 == 0)
        {
            separationDir = Vector2.zero;
            int neighborCount = 0;
            
            if (GameManager.Instance != null && GameManager.Instance.waveManager != null)
            {
                var enemies = GameManager.Instance.waveManager.activeEnemies;
                float sqrRadius = separationRadius * separationRadius;

                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyAI neighbor = enemies[i];
                    if (neighbor == null || neighbor == this) continue;

                    Vector2 diff = (Vector2)transform.position - (Vector2)neighbor.transform.position;
                    float sqrDist = diff.sqrMagnitude;

                    if (sqrDist < sqrRadius && sqrDist > 0.0001f)
                    {
                        separationDir += diff.normalized / Mathf.Sqrt(sqrDist);
                        neighborCount++;
                    }
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

        if (collision.CompareTag("Player"))
        {
            UnitBase targetUnit = collision.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                if (animator != null) animator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
                targetUnit.TakeDamage(attackDamage);
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
}
}
