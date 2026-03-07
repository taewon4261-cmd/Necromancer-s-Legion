// File: Assets/Necromancer/01.Scripts/Characters/UnitBase.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Necromancer
{
/// <summary>
/// 게임 내 모든 유닛(플레이어, 적, 미니언)의 공통 기반 속성 및 행위 정의
/// 추후 Entity 패턴 등의 확장성을 고려하여 추상 클래스로 설계
/// </summary>
public abstract class UnitBase : MonoBehaviour
{
    [Header("Base Stats")]
    public float maxHp = 50f;
    public float currentHp;
    public float moveSpeed = 3f;
    
    protected bool isDead = false;

    public event global::System.Action<float, float> OnHealthChanged; // (current, max)

    protected virtual void Awake()
    {
        currentHp = maxHp;
    }
    
    protected virtual void OnEnable()
    {
        // 풀매니저에 의해 재활용(SetActive)될 때마다 상태 초기화
        isDead = false;
        currentHp = maxHp;
        OnHealthChanged?.Invoke(currentHp, maxHp);
    }

    protected virtual void Update()
    {
        // 최적화를 위해 살아있을 때만 검사
        // 인스펙터에서 강제로 체력을 0으로 깎았을 때 즉각 사망 처리되도록 감지
        if (!isDead && currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 공통 데미지 연산 및 사망 판정
    /// </summary>
    /// <param name="damage">적용될 데미지 수치</param>
    public virtual void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHp -= damage;
        currentHp = Mathf.Clamp(currentHp, 0, maxHp);
        
        OnHealthChanged?.Invoke(currentHp, maxHp);

        if (currentHp <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 사망 시 트리거되는 가상 함수 (사운드, 이펙트 등 공통 로직 배치 가능)
    /// 자식 클래스에서 override하여 고유의 사망 연출 및 풀 반환 등을 구현
    /// </summary>
    protected virtual void Die()
    {
        isDead = true;
        // TODO: Base 사망 이펙트, 피격 사운드 호출 등 추가
    }
}
}
