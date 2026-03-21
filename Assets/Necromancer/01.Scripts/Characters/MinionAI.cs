// File: Assets/Necromancer/01.Scripts/Characters/MinionAI.cs
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


    private Rigidbody2D rb;
    private Transform currentTarget;
    private float lastHitTime;
    private float spawnTime;
    
    // UniTask 취소 토큰
    private CancellationTokenSource scanCts;

    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody2D>();
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
        
        // 기존 토큰 초기화
        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = new CancellationTokenSource();
        
        // 스폰 즉시 주변 적 탐색 UniTask 가동
        ScanForTargetAsync(scanCts.Token).Forget();
    }

    /// <summary>
    /// SkillManager가 중앙에서 들고 있는 누적 글로벌 버프(특성)를
    /// 미니언이 스폰되는(켜지는) 이 한 번의 순간에만 자기 스탯에 곱해서 복사해옵니다.
    /// (이를 통해 매 프레임 수백 마리의 스탯을 관리하는 렉을 없앱니다.)
    /// </summary>
    private void ApplyGlobalBuffs()
    {
        if (GameManager.Instance != null && GameManager.Instance.skillManager != null)
        {
            SkillManager sManager = GameManager.Instance.skillManager;
            
            // 프리팹 원본에 손상을 주지 않기 위해 기본값(하드코딩 또는 보관된 원본값)을 기준으로 곱합니다.
            // 여기서는 단순화를 위해 매번 곱해지는 누적 오류를 막고자 프로퍼티 초기화 로직을 생략하나,
            // 1주차 프로토타입 기준에서는 아래와 같이 단순 덮어쓰기 방식으로 작동시킵니다 (원본값이 필요하면 별도 보관 권장).
            this.maxHp = 50f * sManager.globalMinionHpBonusRatio;
            this.moveSpeed = 3f * sManager.globalMinionSpeedBonusRatio;
            this.attackDamage = 15f * sManager.globalMinionDamageBonusRatio;
        }
    }

    private void OnDisable()
    {
        // 이벤트 구독 해제 (메모리 누수 방지)
        SkillManager.OnMinionStatsChanged -= ApplyGlobalBuffs;

        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = null;
    }

    protected override void Update()
    {
        base.Update();
        if (isDead) return;

        UpdateAnimation();

        // 수명 체크 로직
        if (Time.time > spawnTime + lifeTime)
        {
            Die(); // 수명이 다하면 스스로 소멸
        }
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
        if (isDead) return;
        
        ChaseTarget();
    }

    /// <summary>
    /// 매 프레임 Vector3.Distance를 돌리는 최악의 사태(O(N^2))를 방지하기 위해, 지정된 0.5초 주기로만 타겟을 찾습니다. (UniTask 적용)
    /// </summary>
    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        while (!isDead && !token.IsCancellationRequested)
        {
            // 전역 GameManager의 WaveManager가 관리하는 최적화된 리스트 활용 (성능 최적화)
            if (GameManager.Instance != null && GameManager.Instance.waveManager != null)
            {
                List<EnemyAI> enemies = GameManager.Instance.waveManager.activeEnemies;
                
                float minDistance = Mathf.Infinity;
                Transform bestTarget = null;

                // 적 목록을 순회하며 가장 가까운 타겟 검색 (리스트 i-for문이 foreach보다 약간 더 빠름)
                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyAI enemy = enemies[i];
                    if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.IsDead) continue;

                    float distance = Vector2.Distance(transform.position, enemy.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestTarget = enemy.transform;
                    }
                }
                
                currentTarget = bestTarget;
            }

            // 다음 스캔 주기까지 대기
            await UniTask.Delay(System.TimeSpan.FromSeconds(targetScanRate), cancellationToken: token);
        }
    }

    /// <summary>
    /// 찾은 타겟을 향해 이동
    /// </summary>
    private void ChaseTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            rb.velocity = Vector2.zero; // 타겟이 없으면 대기 (또는 오라 부근으로 복귀 로직 추가 가능)
            return;
        }

        Vector2 direction = (currentTarget.position - transform.position).normalized;
        rb.velocity = direction * moveSpeed;
    }

    /// <summary>
    /// 물리 충돌 판정 (적 타격 로직)
    /// </summary>
    private void OnCollisionStay2D(Collision2D collision)
    {
        if (isDead || Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 적인지 태그로 확인
        if (collision.gameObject.CompareTag("Enemy"))
        {
            UnitBase targetUnit = collision.gameObject.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                SkillManager sManager = GameManager.Instance.skillManager;
                float finalDamage = attackDamage;

                if (sManager != null)
                {
                    // [스킬 연동] 20. 거인 사냥꾼: 정예/보스 몹에게 데미지 증폭 (임시로 EnemyAI에 isBoss 필드가 있다고 가정하거나, maxHp로 퉁침)
                    if (sManager.hasGiantHunter && targetUnit.maxHp >= 200f) 
                    {
                        finalDamage *= 1.3f;
                    }
                }

                // 적 타격 성공 및 애니메이션 재생!
                if (animator != null) animator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);

                targetUnit.TakeDamage(finalDamage);
                lastHitTime = Time.time;
                
                if (sManager != null)
                {
                    // [스킬 연동] 15. 독성 칼날 / 16. 서리 무기 / 19. 저주받은 낙인
                    EnemyAI enemyScript = targetUnit as EnemyAI;
                    if (enemyScript != null)
                    {
                        if (sManager.hasToxicBlade) enemyScript.ApplyPoison(3f, 2f); // 3초간 초당 2 데미지
                        if (sManager.hasFrostWeapon) enemyScript.ApplyFrost(2f, 0.3f); // 2초간 이속 30% 감소
                        if (sManager.hasCursedStigma) enemyScript.AddStigmaStack(); // 10대 맞으면 피해증폭 20%
                    }

                    // [스킬 연동] 12. 흡혈의 이빨: 미니언 타격 시 일정 확률로 플레이어 본체 회복
                    if (sManager.vampiricChance > 0f && Random.value <= sManager.vampiricChance)
                    {
                        PlayerController player = GameManager.Instance.playerTransform.GetComponent<PlayerController>();
                        if (player != null && player.currentHp < player.maxHp)
                        {
                            player.currentHp += 1f; // 1 회복
                            player.currentHp = Mathf.Clamp(player.currentHp, 0, player.maxHp);
                        }
                    }
                }

                // TODO: 뼈 부딪히는 타격 사운드 호출
            }
        }
    }

    /// <summary>
    /// 미니언 사망 또는 수명 만료 처리
    /// </summary>
    protected override void Die()
    {
        base.Die();
        
        rb.velocity = Vector2.zero;
        
        // [스킬 연동] 13. 연쇄 폭발: 사망 시 반경 2m 내 적에게 광역 피해
        SkillManager sManager = GameManager.Instance.skillManager;
        if (sManager != null && sManager.minionExplosionDamage > 0f)
        {
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f);
            foreach (var h in hits)
            {
                if (h.CompareTag("Enemy"))
                {
                    UnitBase enemyObj = h.GetComponent<UnitBase>();
                    if (enemyObj != null) enemyObj.TakeDamage(sManager.minionExplosionDamage);
                }
            }
            // Debug.Log("[SkillEffect] 펑! 미니언 폭발 데미지 발생");
        }
        
        // TODO: 미니언 특유의 소멸 파티클 (가루가 되는 연출)

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
