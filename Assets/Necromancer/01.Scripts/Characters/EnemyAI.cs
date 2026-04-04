
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

    private Transform targetPlayer;
    private Rigidbody2D rb;
    private float lastHitTime;
    private CancellationTokenSource enrageCts;

    // --- [스킬 연동: 상태이상 디버프 변수들] ---
    private float originalMoveSpeed;
    private int stigmaStacks = 0;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
    }

    protected override void OnEnable()
    {
        // base.OnEnable()은 Setup()에서 수동으로 호출하거나, 
        // 데이터 주입 직후에 실행되도록 WaveManager에서 제어하는 것이 안전하지만,
        // 여기서는 기본 UnitBase의 초기화(isDead, currentHp)를 수행합니다.
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

        // [ENRAGE] 스폰 20초 후 광폭화 루틴 시작
        enrageCts?.Cancel();
        enrageCts?.Dispose();
        enrageCts = new CancellationTokenSource();
        EnrageAfterDelayAsync(enrageCts.Token).Forget();
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

    /// <summary>
    /// Pool에서 꺼낸 직후, 현재 스테이지 난이도 배율을 반영하여 적의 스탯을 설정합니다.
    /// </summary>
    public void Setup(EnemyData enemyData)
    {
        this.data = enemyData;
        if (data == null) return;

        // 중앙 CombatManager에서 배율 가져오기
        float hpMult = 1f;
        float dmgMult = 1f;

        if (GameManager.Instance != null && GameManager.Instance.Combat != null)
        {
            hpMult = GameManager.Instance.Combat.enemyHpMultiplier;
            dmgMult = GameManager.Instance.Combat.enemyDamageMultiplier;
        }

        // 스탯 결정 (배율 적용)
        this.maxHp = data.maxHp * hpMult;
        this.attackDamage = data.attackDamage * dmgMult;
        this.moveSpeed = data.moveSpeed;
        this.originalMoveSpeed = moveSpeed;

        // 스탯이 바뀌었으므로 체력 리셋
        this.currentHp = maxHp;
        this.isDead = false;

        if (spriteRenderer != null) spriteRenderer.color = Color.white;
    }

    /// <summary>
    /// 스폰 후 일정 시간(20초)이 지나면 적을 광폭화시켜 플레이어를 압박합니다.
    /// </summary>
    private async UniTaskVoid EnrageAfterDelayAsync(CancellationToken token)
    {
        // 20초 대기 (UniTask 활용으로 성능 최적화)
        bool isCancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(20.0f), cancellationToken: token).SuppressCancellationThrow();
        
        if (!isCancelled && !isDead && gameObject.activeInHierarchy)
        {
            // 1. 이동 속도 1.5배 증가
            moveSpeed *= 1.5f;

            // 2. 공격 쿨타임 30% 단축
            hitCooldown *= 0.7f;

            // 3. 시각적 연출 (빨간색)
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
            stigmaStacks = 0; // 초기화
            // [스킬 효과] 저주받은 낙인: 10스택 시 잃은 체력 비례 또는 고정 피해
            TakeDamage(maxHp * 0.2f); 
            // Debug.Log("[SkillEffect] 저주받은 낙인 10스택 폭발!");
        }
    }
    // --- [스킬 연동 로직 끝] ---

    protected override void Update()
    {
        base.Update();
        if (isDead) return;
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
        if (isDead || targetPlayer == null) return;
        
        ChasePlayer();
    }

    /// <summary>
    /// 플레이어를 향해 직선 이동 (최적화를 위해 A* 대신 단순 방향 벡터 사용)
    /// </summary>
    private void ChasePlayer()
    {
        // 1. 방향 구하기 (목표지점 - 내위치)
        Vector2 direction = (targetPlayer.position - transform.position).normalized;
        
        // 2. 물리적 이동
        rb.velocity = direction * moveSpeed;
    }

    /// <summary>
    /// 물리 충돌 판정 (플레이어 타격 로직)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        // 최적화: 내가 죽었거나 쿨타임 중이면 연산 스킵
        if (isDead || Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 플레이어인지 태그로 확인
        if (collision.gameObject.CompareTag("Player"))
        {
            UnitBase targetUnit = collision.gameObject.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                // 타격 성공 및 애니메이션 재생
                if (animator != null) animator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
                
                targetUnit.TakeDamage(attackDamage);
                lastHitTime = Time.time; // 쿨타임 초기화
                
                // TODO: 플레이어 피격 이펙트/사운드 호출
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        base.TakeDamage(damage);

        // [Polishing] 타격 연출 실행
        if (GameManager.Instance != null && GameManager.Instance.feedbackManager != null && gameObject.activeInHierarchy)
        {
            float duration = (data != null && data.isElite) ? 0.12f : 0.05f;
            float magnitude = (data != null && data.isElite) ? 0.15f : 0.08f;
            
            GameManager.Instance.feedbackManager.ShakeCamera(duration, magnitude);
            // [DEPRECATED] 대규모 전투 최적화 및 셰이더 에러 방지를 위해 이펙트 미출력
            // GameManager.Instance.feedbackManager.PlayHitEffect(transform.position, "HitEffect");
        }
    }

    /// <summary>
    /// 적 사망 시 보석 드랍 및 부활 시스템 호출
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        rb.velocity = Vector2.zero;
        
        if (GameManager.Instance != null)
        {
            // 💎 1. 죽은 자리에 경험치 보석(ExpGem) 확정 드랍
            if (GameManager.Instance.poolManager != null)
            {
                GameManager.Instance.poolManager.Get("ExpGem", transform.position, Quaternion.identity);
            }

            // 🔮 2. 부활 주사위 굴려달라고 중앙 통제실(GameManager)에 요청
            GameManager.Instance.TryReviveAsMinion(transform.position);
        }

        // 3. 사망 연출 (TODO: 폭발 파티클, 피 흘림 등)
        
        // 3. 내 시체 치우기 (Destroy 대신 풀매니저 반납)
        if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
        {
            // 인스펙터 풀 세팅에서 설정한 이름(예: "Enemy_Peasant")을 하드코딩하지 않고 본인의 (Clone) 뗀 이름으로 반환하는 게 좋으나,
            // 1주차 프로토타입이므로 직관적으로 "Enemy" 로 고정 반환 (EnemySpawner와 태그를 맞춰야 함)
            GameManager.Instance.poolManager.Release("Enemy", gameObject);
        }
        else
        {
            // 게임매니저가 없다면 어쩔 수 없이 임시 파괴
            Destroy(gameObject);
        }
    }
}
}
