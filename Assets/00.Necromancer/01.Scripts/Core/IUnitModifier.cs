using UnityEngine;
using System.Collections.Generic;

namespace Necromancer
{
    /// <summary>
    /// 유닛에게 부여되는 상태이상이나 버프/디버프를 정의하는 인터페이스.
    /// </summary>
    public interface IUnitModifier
    {
        string ModifierId { get; }
        void OnApply(UnitBase unit);
        void OnUpdate(UnitBase unit, float deltaTime);
        void OnRemove(UnitBase unit);
        bool IsExpired { get; }
    }

    /// <summary>
    /// 모디파이어를 생성하기 위한 템플릿(팩토리) 인터페이스.
    /// </summary>
    public interface IUnitModifierTemplate
    {
        string ModifierId { get; }
        IUnitModifier CreateModifier();
    }

    // --- [Concrete Modifiers & Templates] ---

    #region Poison (DOT)
    public class PoisonModifierTemplate : IUnitModifierTemplate
    {
        public string ModifierId => "Poison";
        public float duration;
        public float tickDamage;

        public PoisonModifierTemplate(float duration, float tickDamage)
        {
            this.duration = duration;
            this.tickDamage = tickDamage;
        }

        public IUnitModifier CreateModifier() => new PoisonModifier(duration, tickDamage);
    }

    public class PoisonModifier : IUnitModifier
    {
        public string ModifierId => "Poison";
        private float duration;
        private float tickDamage;
        private float timer;
        private float tickTimer;

        public bool IsExpired => timer >= duration;

        public PoisonModifier(float duration, float tickDamage)
        {
            this.duration = duration;
            this.tickDamage = tickDamage;
        }

        public void OnApply(UnitBase unit) { timer = 0; tickTimer = 0; }
        public void OnUpdate(UnitBase unit, float deltaTime)
        {
            timer += deltaTime;
            tickTimer += deltaTime;
            if (tickTimer >= 1.0f)
            {
                unit.TakeDamage(tickDamage);
                tickTimer = 0;
            }
        }
        public void OnRemove(UnitBase unit) { }
    }
    #endregion

    #region Frost (Slow)
    public class FrostModifierTemplate : IUnitModifierTemplate
    {
        public string ModifierId => "Frost";
        public float duration;
        public float slowdownRatio;

        public FrostModifierTemplate(float duration, float slowdownRatio)
        {
            this.duration = duration;
            this.slowdownRatio = slowdownRatio;
        }

        public IUnitModifier CreateModifier() => new FrostModifier(duration, slowdownRatio);
    }

    public class FrostModifier : IUnitModifier
    {
        public string ModifierId => "Frost";
        private float duration;
        private float slowdownRatio;
        private float timer;
        private float originalSpeed;

        public bool IsExpired => timer >= duration;

        public FrostModifier(float duration, float slowdownRatio)
        {
            this.duration = duration;
            this.slowdownRatio = slowdownRatio;
        }

        public void OnApply(UnitBase unit)
        {
            originalSpeed = unit.moveSpeed;
            unit.moveSpeed *= (1f - slowdownRatio);
            timer = 0;
        }
        public void OnUpdate(UnitBase unit, float deltaTime) => timer += deltaTime;
        public void OnRemove(UnitBase unit) => unit.moveSpeed = originalSpeed;
    }
    #endregion

    #region Stigma (Stacking)
    public class StigmaModifierTemplate : IUnitModifierTemplate
    {
        public string ModifierId => "Stigma";
        public IUnitModifier CreateModifier() => new StigmaModifier();
    }

    public class StigmaModifier : IUnitModifier
    {
        public string ModifierId => "Stigma";
        private int stacks = 1;
        private const int MaxStacks = 5; // 10대 -> 5대로 완화
        public bool IsExpired => false; // 낙인은 죽을 때까지 유지 (영구 증폭)

        public void OnApply(UnitBase unit) { }
        public void OnUpdate(UnitBase unit, float deltaTime) { }
        public void OnRemove(UnitBase unit) { }

        public void AddStack() => stacks = Mathf.Min(stacks + 1, MaxStacks);
        
        // 현재 낙인에 의한 데미지 배율 반환 (1.0 + 10% 증폭)
        public float GetDamageMultiplier() => (stacks >= MaxStacks) ? 1.1f : 1.0f;
    }
    #endregion

    #region Bleeding (Percent Damage) - NEW
    public class BleedingModifierTemplate : IUnitModifierTemplate
    {
        public string ModifierId => "Bleeding";
        public IUnitModifier CreateModifier() => new BleedingModifier(5f, 0.02f);
    }

    public class BleedingModifier : IUnitModifier
    {
        public string ModifierId => "Bleeding";
        private float duration;
        private float percentDamage;
        private float timer;
        private float tickTimer;

        public bool IsExpired => timer >= duration;

        public BleedingModifier(float duration, float percentDamage)
        {
            this.duration = duration;
            this.percentDamage = percentDamage;
        }

        public void OnApply(UnitBase unit) { timer = 0; tickTimer = 0; }
        public void OnUpdate(UnitBase unit, float deltaTime)
        {
            timer += deltaTime;
            tickTimer += deltaTime;
            if (tickTimer >= 1.0f)
            {
                unit.TakeDamage(unit.maxHp * percentDamage); // 최대 체력의 % 데미지
                tickTimer = 0;
            }
        }
        public void OnRemove(UnitBase unit) { }
    }
    #endregion

    #region Stun (Disable AI) - NEW
    public class StunModifierTemplate : IUnitModifierTemplate
    {
        public string ModifierId => "Stun";
        public IUnitModifier CreateModifier() => new StunModifier(1.5f);
    }

    public class StunModifier : IUnitModifier
    {
        public string ModifierId => "Stun";
        private float duration;
        private float timer;

        public bool IsExpired => timer >= duration;

        public StunModifier(float duration) => this.duration = duration;

        public void OnApply(UnitBase unit) { timer = 0; unit.IsStunned = true; }
        public void OnUpdate(UnitBase unit, float deltaTime) => timer += deltaTime;
        public void OnRemove(UnitBase unit) => unit.IsStunned = false;
    }
    #endregion
}
