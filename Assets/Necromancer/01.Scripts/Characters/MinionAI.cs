
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
    private CancellationTokenSource scanCts;
    private Transform playerTransform;

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

        // 플레이어 위치 캐싱
        if (GameManager.Instance != null) playerTransform = GameManager.Instance.playerTransform;
        
        // 기존 토큰 초기화
        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = new CancellationTokenSource();
        
        // 스폰 즉시 주변 적 탐색 UniTask 가동
        ScanForTargetAsync(scanCts.Token).Forget();
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

        scanCts?.Cancel();
        scanCts?.Dispose();
        scanCts = null;
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
            float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
            if (distToPlayer > 18.0f)
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
        
        ChaseTarget();
    }

    private async UniTaskVoid ScanForTargetAsync(CancellationToken token)
    {
        while (!isDead && !token.IsCancellationRequested)
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) break;

            if (playerTransform != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
                if (distToPlayer > 10.0f)
                {
                    currentTarget = null;
                    await UniTask.Delay(System.TimeSpan.FromSeconds(targetScanRate), cancellationToken: token);
                    continue;
                }
            }

            if (GameManager.Instance != null && GameManager.Instance.waveManager != null)
            {
                List<EnemyAI> enemies = GameManager.Instance.waveManager.activeEnemies;
                
                float minDistance = Mathf.Infinity;
                Transform bestTarget = null;

                for (int i = 0; i < enemies.Count; i++)
                {
                    EnemyAI enemy = enemies[i];
                    if (enemy == null || !enemy.gameObject.activeInHierarchy || enemy.IsDead) continue;

                    float distFromPlayer = Vector2.Distance(playerTransform.position, enemy.transform.position);
                    if (distFromPlayer > 8.0f) continue;

                    float distance = Vector2.Distance(transform.position, enemy.transform.position);
                    if (distance < minDistance)
                    {
                        minDistance = distance;
                        bestTarget = enemy.transform;
                    }
                }
                
                currentTarget = bestTarget;
            }

            await UniTask.Delay(System.TimeSpan.FromSeconds(targetScanRate), cancellationToken: token);
        }
    }

    private void ChaseTarget()
    {
        if (currentTarget == null || !currentTarget.gameObject.activeInHierarchy)
        {
            if (playerTransform != null)
            {
                float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
                if (distToPlayer > 2.0f)
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
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (isDead || (GameManager.Instance != null && GameManager.Instance.IsGameOver)) return;
        if (Time.time < lastHitTime + hitCooldown) return;

        // 상대방이 적인지 태그로 확인
        if (collision.CompareTag("Enemy"))
        {
            UnitBase targetUnit = collision.GetComponent<UnitBase>();
            if (targetUnit != null)
            {
                SkillManager sManager = GameManager.Instance.skillManager;
                float finalDamage = attackDamage;

                if (sManager != null)
                {
                    if (sManager.hasGiantHunter && targetUnit.maxHp >= 200f) 
                    {
                        finalDamage *= 1.3f;
                    }
                }

                if (animator != null) animator.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);

                targetUnit.TakeDamage(finalDamage);
                lastHitTime = Time.time;
                
                if (sManager != null)
                {
                    EnemyAI enemyScript = targetUnit as EnemyAI;
                    if (enemyScript != null)
                    {
                        if (sManager.hasToxicBlade) enemyScript.ApplyPoison(3f, 2f);
                        if (sManager.hasFrostWeapon) enemyScript.ApplyFrost(2f, 0.3f);
                        if (sManager.hasCursedStigma) enemyScript.AddStigmaStack();
                    }

                    if (sManager.vampiricChance > 0f && Random.value <= sManager.vampiricChance)
                    {
                        PlayerController player = GameManager.Instance.playerTransform.GetComponent<PlayerController>();
                        if (player != null && player.currentHp < player.maxHp)
                        {
                            player.currentHp += 1f;
                            player.currentHp = Mathf.Clamp(player.currentHp, 0, player.maxHp);
                        }
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
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, 2f);
            foreach (var h in hits)
            {
                if (h.CompareTag("Enemy"))
                {
                    UnitBase enemyObj = h.GetComponent<UnitBase>();
                    if (enemyObj != null) enemyObj.TakeDamage(sManager.minionExplosionDamage);
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
