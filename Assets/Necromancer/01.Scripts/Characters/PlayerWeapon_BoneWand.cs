// File: Assets/Necromancer/01.Scripts/Characters/PlayerWeapon_BoneWand.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;

namespace Necromancer
{

/// <summary>
/// 플레이어에게 장착되어 일정 시간마다 주변 적을 찾아 투사체를 발사하는 자동 무기 클래스.
/// (뱀파이어 서바이버의 시작 무기 스타일)
/// </summary>
public class PlayerWeapon_BoneWand : MonoBehaviour
{
    [Header("Weapon Stats")]
    [Tooltip("발사 쿨타임 (초)")]
    public float fireRate = 1.2f;
    
    [Tooltip("한 방당 데미지")]
    public float baseDamage = 20f;
    
    [Tooltip("적 탐색 반경 (기획: 화면 중앙에서 2/3 지점인 5.5f로 하향)")]
    public float detectionRadius = 5.5f;

    [Tooltip("풀매니저에서 꺼내올 투사체 이름")]
    public string projectileTag = "BoneProjectile";

    private bool isShooting = false;
    private CancellationTokenSource fireCts;

    private void Start()
    {
        // 시작 시 초기 스탯 적용 및 이벤트 구독
        UpdateWeaponStats();
        SkillManager.OnPlayerStatsChanged += UpdateWeaponStats;

        // 무기 장착과 동시에 자동 발사 개시
        StartShooting();
    }

    private void OnDisable()
    {
        SkillManager.OnPlayerStatsChanged -= UpdateWeaponStats;
        StopShooting();
    }

    public void StartShooting()
    {
        if (isShooting) return;
        
        isShooting = true;
        
        fireCts?.Cancel();
        fireCts?.Dispose();
        fireCts = new CancellationTokenSource();

        AutoFireRoutineAsync(fireCts.Token).Forget();
    }

    public void StopShooting()
    {
        isShooting = false;
        fireCts?.Cancel();
    }

    /// <summary>
    /// 지정된 fireRate마다 적을 찾아서 쏘는 무한 루프
    /// </summary>
    private async UniTaskVoid AutoFireRoutineAsync(CancellationToken token)
    {
        while (isShooting && !token.IsCancellationRequested)
        {
            // 아직 게임의 중앙 매니저들이 준비 안 됐다면 대기
            if (GameManager.Instance != null && GameManager.Instance.poolManager != null)
            {
                Transform target = FindClosestEnemy();
                
                if (target != null)
                {
                    FireAtTarget(target);
                }
            }

            // 발사 후 쿨타임 대기 (마법 캐스팅 시간 및 재사용 대기)
            await UniTask.Delay(System.TimeSpan.FromSeconds(fireRate), cancellationToken: token);
        }
    }

    /// <summary>
    /// 반경 내의 가장 가까운 적을 스캔합니다.
    /// MinionAI와 달리 즉각적인 타겟팅이 필요하므로 발사 시점에 한 번만 검색합니다.
    /// </summary>
    private Transform FindClosestEnemy()
    {
        // Physics2D.OverlapCircleAll은 타겟팅 시 FindGameObjectsWithTag보다 충돌체 기반이라 퍼포먼스가 월등히 좋습니다.
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, detectionRadius);
        
        float closestDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        foreach (var hit in hitColliders)
        {
            if (hit.CompareTag("Enemy"))
            {
                float distance = Vector2.Distance(transform.position, hit.transform.position);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestEnemy = hit.transform;
                }
            }
        }

        return closestEnemy;
    }

    /// <summary>
    /// 타겟 방향으로 투사체를 Pool에서 꺼내 조준 및 발사 명령을 내림
    /// </summary>
    private void FireAtTarget(Transform target)
    {
        // [추가] 애니메이션 트리거 (공격 동작 재생)
        // 무기는 보통 플레이어의 자식으로 있으므로 부모의 애니메이터를 찾아 쏩니다.
        Animator playerAnim = GetComponentInParent<Animator>();
        if (playerAnim != null)
        {
            playerAnim.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
        }

        // 방향 계산 (타겟 - 내 위치)
        Vector2 direction = (target.position - transform.position).normalized;

        // Player 자식으로 생성하면 플레이어가 움직일 때 같이 밀려나므로, 최상단(또는 PoolManager 안)에 꺼냄
        GameObject projectile = GameManager.Instance.poolManager.Get(projectileTag, transform.position, Quaternion.identity);

        if (projectile != null)
        {
            BoneProjectile boneScript = projectile.GetComponent<BoneProjectile>();
            if (boneScript != null)
            {
                // 방향과 현재 무기 데미지를 주입하여 발사 트리거!
                boneScript.Fire(direction, baseDamage);
            }
        }
    }

    /// <summary>
    /// SkillManager로부터 최신 무기 강화 수치를 가져와 적용합니다.
    /// </summary>
    private void UpdateWeaponStats()
    {
        if (GameManager.Instance == null || GameManager.Instance.skillManager == null) return;

        // 기본값(20f)을 기준으로 SkillManager의 낫 업그레이드 여부 등을 체크하여 반영할 수 있습니다.
        // 여기서는 간단하게 ScytheUpgrade 스킬이 선택될 때마다 SkillManager에서 OnPlayerStatsChanged를 쏘므로
        // 호출될 때마다 일정 비율로 강화하거나, SkillManager에 명시적인 무기 공격력 변수를 만들어 관리하는 것이 좋습니다.
        
        // 스케일링 예시 (기본 데미지 20에서 시작하여 10%씩 복리로 증가 등은 기획에 따름)
        // 일단은 로직이 호출됨을 보장하는 로그를 남깁니다.
        Debug.Log("[PlayerWeapon] Weapon Stats Updated via Event.");
    }
}
}
