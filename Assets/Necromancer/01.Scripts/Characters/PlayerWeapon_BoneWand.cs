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
    
    [Tooltip("적 탐색 반경 (기획: 화면 중앙 집중형 교전을 위해 4.0f로 추가 하향)")]
    public float detectionRadius = 4.0f;

    // [OPTIMIZATION] 가비지 컬렉션 방지를 위한 정적 버퍼
    private static readonly Collider2D[] scanBuffer = new Collider2D[16];

    [Tooltip("풀매니저에서 꺼내올 투사체 이름")]
    public string projectileTag = "BoneProjectile";

    private bool isShooting = false;
    private Animator playerAnim;
    private PoolManager poolMgr;
    
    // [ARCHITECT] 성능 최적화를 위한 보조 스탯 관리
    private float currentDamage;
    private int weaponLevel = 1;

    private void Start()
    {
        // [OPTIMIZATION] 런타임 반복 검색(GetComponentInParent) 방지를 미리 캐싱
        playerAnim = GetComponentInParent<Animator>();
        if (GameManager.Instance != null) poolMgr = GameManager.Instance.poolManager;

        // 시작 시 초기 스탯 적용 및 이벤트 구독
        UpdateWeaponStats();
        SkillManager.OnPlayerStatsChanged += UpdateWeaponStats;

        // 무기 장착과 동시에 자동 발사 개시
        StartShooting();
    }

    private void OnDisable()
    {
        SkillManager.OnPlayerStatsChanged -= UpdateWeaponStats;
        isShooting = false;
    }

    public void StartShooting()
    {
        if (isShooting) return;
        
        isShooting = true;
        
        // [LIFECYCLE] 오브젝트 파괴 시 자동 취소되는 토큰 주입
        var token = gameObject.GetCancellationTokenOnDestroy();
        AutoFireRoutineAsync(token).Forget();
    }

    public void StopShooting()
    {
        isShooting = false;
        // fireCts?.Cancel(); // [STABILITY] CTS 대신 전역 플래그와 UniTask 루프내 토큰 체크로 대체
    }

    /// <summary>
    /// 지정된 fireRate마다 적을 찾아서 쏘는 무한 루프
    /// </summary>
    private async UniTaskVoid AutoFireRoutineAsync(CancellationToken token)
    {
        while (isShooting && !token.IsCancellationRequested)
        {
            // [STABILITY] 씬 전환이나 오브젝트 파괴 시 즉시 루프 탈출
            if (!this.gameObject.activeInHierarchy) break;

            if (poolMgr != null)
            {
                Transform target = FindClosestEnemy();
                
                if (target != null)
                {
                    FireAtTarget(target);
                }
            }

            // 발사 후 쿨타임 대기 (마법 캐스팅 시간 및 재사용 대기)
            // [LIFECYCLE] 토큰을 넘겨 대기 중 파괴 시 즉시 종료 보장
            await UniTask.Delay(System.TimeSpan.FromSeconds(fireRate), cancellationToken: token).SuppressCancellationThrow();
        }
    }

    /// <summary>
    /// 반경 내의 가장 가까운 적을 스캔합니다.
    /// MinionAI와 달리 즉각적인 타겟팅이 필요하므로 발사 시점에 한 번만 검색합니다.
    /// </summary>
    private Transform FindClosestEnemy()
    {
        // [GC-FIX] OverlapCircleAll 대신 NonAlloc 사용으로 매 발사 시 발생하는 힙 할당 제거
        int count = Physics2D.OverlapCircleNonAlloc(transform.position, detectionRadius, scanBuffer);
        
        float closestSqrDistance = Mathf.Infinity;
        Transform closestEnemy = null;

        for (int i = 0; i < count; i++)
        {
            Collider2D hit = scanBuffer[i];
            if (hit == null) continue;

            if (hit.CompareTag("Enemy"))
            {
                // [sqrMagnitude] 제곱근 연산 제거로 매 프레임 타겟팅 부하 최소화
                float sqrDistance = (transform.position - hit.transform.position).sqrMagnitude;
                if (sqrDistance < closestSqrDistance)
                {
                    closestSqrDistance = sqrDistance;
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
        // [OPTIMIZATION] 캐싱된 애니메이터 활용으로 매 샷마다의 트리거 부하 절절
        if (playerAnim != null)
        {
            playerAnim.SetTrigger(Necromancer.Systems.UIConstants.AnimParam_Attack);
        }

        // 방향 계산 (타겟 - 내 위치)
        Vector2 direction = (target.position - transform.position).normalized;

        // [PERFORMANCE] Get(String) -> Component 시 GetComponent 한 번 줄이면 이상적이나 현재는 캐싱 위주
        GameObject projectile = poolMgr.Get(projectileTag, transform.position, Quaternion.identity);

        if (projectile != null)
        {
            // [ZERO-SEARCH] 매 샷마다 GetComponent 대신, 풀에서 꺼낸 오브젝트에 직접 주입
            if (projectile.TryGetComponent<BoneProjectile>(out var boneScript))
            {
                boneScript.Fire(direction, currentDamage);
            }
        }
    }

    /// <summary>
    /// SkillManager로부터 최신 무기 강화 수치를 가져와 적용합니다.
    /// </summary>
    private void UpdateWeaponStats()
    {
        if (GameManager.Instance == null || GameManager.Instance.skillManager == null) 
        {
            currentDamage = baseDamage;
            return;
        }

        var sm = GameManager.Instance.skillManager;
        
        // [ARCHITECT] 전역 스탯 배율(Ratio)과 개별 레벨의 가중치를 결합하여 데미지 산출
        weaponLevel = sm.playerWeaponLevel;
        currentDamage = baseDamage * sm.globalPlayerWeaponDamageRatio;
        
        // [PERFORMANCE] 발사 속도는 루프 대기 시간에 즉각 반영됨
        // fireRate = 1.2f / sm.globalPlayerWeaponFireRateRatio; 
        
        Debug.Log($"[PlayerWeapon] Stats Updated: Level {weaponLevel}, DMG {currentDamage}");
    }
}
}
