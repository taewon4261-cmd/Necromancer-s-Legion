using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using Necromancer.Core;
using Necromancer.Data;
using UnityEngine;

namespace Necromancer
{
    public static class MinionUniqueSkillExecutor
    {
        private static readonly List<UnitBase> nearbyBuffer = new List<UnitBase>(32);
        private static int activeTemporaryWarriorCount;
        private const int MaxTemporaryWarriors = 8;

        public static bool TryCast(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (caster == null || skill == null || target == null) return false;

            bool knownSkill = true;
            bool casted = skill.skillID switch
            {
                "SkeletonWarrior_Slam" => CastWarriorSlam(caster, skill, target),
                "SkeletonArcher_Multishot" => CastArcherMultishot(caster, skill, target),
                "SkeletonMage_ChainLightning" => CastMageChainLightning(caster, skill, target),
                "SkeletonWarrior_Charge" => CastWarriorCharge(caster, skill, target),
                "SkeletonArcher_FireArrow" => CastArcherFireArrow(caster, skill, target),
                "SkeletonMage_Meteor" => CastMageMeteor(caster, skill, target),
                "SkeletonWolf_Leap" => CastWolfLeap(caster, skill, target),
                "SkeletonGiant_Roar" => CastGiantRoar(caster, skill, target),
                "SkeletonKnight_Cleave" => CastKnightCleave(caster, skill, target),
                "SkeletonWolf_GiantForm" => CastWolfGiantForm(caster, skill, target),
                "SkeletonGiant_Berserk" => CastGiantBerserk(caster, skill, target),
                "SkeletonKnight_SummonWarriors" => CastKnightSummonWarriors(caster, skill, target),
                _ => UnknownSkill(out knownSkill)
            };

            if (casted && !IsDelayedFeedbackSkill(skill.skillID))
                PlayFeedback(skill, target.position);

            if (!knownSkill)
                Debug.LogWarning($"[MinionUniqueSkillExecutor] Unknown unique skillID: {skill.skillID}");

            return casted;
        }

        private static bool UnknownSkill(out bool knownSkill)
        {
            knownSkill = false;
            return false;
        }

        private static bool IsDelayedFeedbackSkill(string skillID)
        {
            return skillID == "SkeletonMage_Meteor";
        }

        private static bool CastWarriorSlam(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (!target.TryGetComponent(out IDamageable targetUnit)) return false;

            float damageMultiplier = skill.value > 0f ? skill.value : 1.5f;
            targetUnit.ApplyDamage(caster.attackDamage * damageMultiplier, caster);
            targetUnit.Unit?.AddModifier(new StunModifier(0.5f));
            return true;
        }

        private static bool CastArcherMultishot(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.poolManager == null) return false;

            Vector2 baseDir = (target.position - caster.transform.position).normalized;
            const int extraProjectiles = 2;
            const float spreadAngle = 12f;
            float damageMultiplier = skill.value > 0f ? skill.value : 1f;

            for (int i = 0; i < extraProjectiles; i++)
            {
                float angle = i == 0 ? -spreadAngle : spreadAngle;
                Vector2 finalDir = Quaternion.Euler(0, 0, angle) * baseDir;
                GameObject projGo = GameManager.Instance.poolManager.Get(caster.ProjectilePoolTag, caster.transform.position, Quaternion.identity);
                if (projGo != null && projGo.TryGetComponent<BoneProjectile>(out var proj))
                    proj.Fire(finalDir, caster.attackDamage * damageMultiplier, caster, caster.ProjectilePoolTag);
            }

            return true;
        }

        private static bool CastMageChainLightning(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            float damageMultiplier = skill.value > 0f ? skill.value : 1.2f;
            int hitCount = 0;

            if (target.TryGetComponent(out IDamageable primaryTarget))
            {
                primaryTarget.ApplyDamage(caster.attackDamage * damageMultiplier, caster);
                hitCount++;
            }

            nearbyBuffer.Clear();
            GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(target.position, skill.range, nearbyBuffer);
            for (int i = 0; i < nearbyBuffer.Count && hitCount < 3; i++)
            {
                UnitBase unit = nearbyBuffer[i];
                if (!IsValidEnemy(unit, target)) continue;

                unit.ApplyDamage(caster.attackDamage * damageMultiplier, caster);
                PlayFeedback(skill, unit.transform.position);
                hitCount++;
            }

            return hitCount > 0;
        }

        private static bool CastWarriorCharge(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            Vector3 start = caster.transform.position;
            Vector3 direction = (target.position - start).normalized;
            float distance = Mathf.Min(skill.range > 0f ? skill.range : 3f, Vector3.Distance(start, target.position));
            Vector3 end = start + direction * distance;
            float damageMultiplier = skill.value > 0f ? skill.value : 1.8f;

            caster.transform.position = end;
            ApplyAreaDamage(caster, end, 1.6f, caster.attackDamage * damageMultiplier, maxHits: 5);
            return true;
        }

        private static bool CastArcherFireArrow(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            float tickDamage = caster.attackDamage * (skill.value > 0f ? skill.value : 0.45f);
            nearbyBuffer.Clear();
            GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(target.position, skill.range > 0f ? skill.range : 2.2f, nearbyBuffer);

            int hitCount = 0;
            for (int i = 0; i < nearbyBuffer.Count; i++)
            {
                UnitBase unit = nearbyBuffer[i];
                if (!IsValidEnemy(unit, null)) continue;

                unit.AddModifier(new PoisonModifier(3f, tickDamage));
                hitCount++;
            }

            return hitCount > 0;
        }

        private static bool CastMageMeteor(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            Vector3 impactPosition = target.position;
            MeteorDelayAsync(caster, skill, impactPosition, caster.UniqueSkillToken).Forget();
            return true;
        }

        private static bool CastWolfLeap(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            Vector3 direction = (target.position - caster.transform.position).normalized;
            caster.transform.position = target.position - direction * 0.75f;

            if (!target.TryGetComponent(out IDamageable targetUnit)) return false;

            float damageMultiplier = skill.value > 0f ? skill.value : 1.4f;
            targetUnit.ApplyDamage(caster.attackDamage * damageMultiplier, caster);
            targetUnit.Unit?.AddModifier(new FrostModifier(1.2f, 0.35f));
            return true;
        }

        private static bool CastGiantRoar(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            float radius = skill.range > 0f ? skill.range : 3f;
            float slowRatio = skill.value > 0f ? Mathf.Clamp01(skill.value) : 0.3f;
            int affected = 0;

            nearbyBuffer.Clear();
            GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(caster.transform.position, radius, nearbyBuffer);
            for (int i = 0; i < nearbyBuffer.Count; i++)
            {
                UnitBase unit = nearbyBuffer[i];
                if (!IsValidEnemy(unit, null)) continue;

                unit.AddModifier(new FrostModifier(2.5f, slowRatio));
                affected++;
            }

            return affected > 0;
        }

        private static bool CastKnightCleave(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.unitManager == null) return false;

            Vector3 origin = caster.transform.position;
            Vector3 forward = (target.position - origin).normalized;
            float radius = skill.range > 0f ? skill.range : 2.1f;
            float damageMultiplier = skill.value > 0f ? skill.value : 1.25f;
            int hitCount = 0;

            nearbyBuffer.Clear();
            GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(origin, radius, nearbyBuffer);
            for (int i = 0; i < nearbyBuffer.Count && hitCount < 4; i++)
            {
                UnitBase unit = nearbyBuffer[i];
                if (!IsValidEnemy(unit, null)) continue;

                Vector3 toUnit = (unit.transform.position - origin).normalized;
                if (Vector3.Dot(forward, toUnit) < 0.15f) continue;

                unit.ApplyDamage(caster.attackDamage * damageMultiplier, caster);
                hitCount++;
            }

            return hitCount > 0;
        }

        private static bool CastWolfGiantForm(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            float damageMultiplier = skill.value > 0f ? skill.value : 0.5f;
            TimedSelfBuffAsync(caster, hpBonusRatio: 0.35f, damageBonusRatio: damageMultiplier, scaleMultiplier: 1.3f, duration: 6f, caster.UniqueSkillToken).Forget();
            return true;
        }

        private static bool CastGiantBerserk(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            float cooldownMultiplier = skill.value > 0f ? Mathf.Clamp(skill.value, 0.35f, 0.9f) : 0.55f;
            TimedCooldownBuffAsync(caster, cooldownMultiplier, duration: 5f, caster.UniqueSkillToken).Forget();
            return true;
        }

        private static bool CastKnightSummonWarriors(MinionAI caster, MinionUniqueSkillData skill, Transform target)
        {
            if (GameManager.Instance?.poolManager == null || GameManager.Instance?.minionUnlockDataList == null) return false;

            MinionUnlockSO warriorData = null;
            var list = GameManager.Instance.minionUnlockDataList;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] != null && list[i].minionID == "SkeletonWarrior")
                {
                    warriorData = list[i];
                    break;
                }
            }

            if (warriorData == null) return false;

            int availableSlots = GetAvailableTemporaryWarriorSlots();
            if (availableSlots <= 0) return false;

            int summonCount = Mathf.Min(Mathf.Clamp(Mathf.RoundToInt(skill.value > 0f ? skill.value : 2f), 1, 3), availableSlots);
            for (int i = 0; i < summonCount; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, 0f, i * (360f / summonCount)) * Vector3.right * 0.85f;
                GameObject minionObj = GameManager.Instance.poolManager.Get("Minion", caster.transform.position + offset, Quaternion.identity);
                if (minionObj != null && minionObj.TryGetComponent<MinionAI>(out var ai))
                {
                    ai.Initialize(warriorData, "Minion");
                    activeTemporaryWarriorCount++;
                    TemporarySummonAsync(ai, 8f, caster.UniqueSkillToken).Forget();
                }
            }

            return true;
        }

        private static int GetAvailableTemporaryWarriorSlots()
        {
            int hardLimit = MaxTemporaryWarriors;
            int globalSlots = hardLimit;

            SkillManager skillManager = GameManager.Instance?.skillManager;
            UnitManager unitManager = GameManager.Instance?.unitManager;
            if (skillManager != null)
            {
                int tempBudget = Mathf.Clamp(Mathf.CeilToInt(skillManager.currentMaxMinions * 0.2f), 1, hardLimit);
                hardLimit = Mathf.Min(hardLimit, tempBudget);

                if (unitManager != null)
                    globalSlots = Mathf.Max(0, skillManager.currentMaxMinions - unitManager.CountActiveMinions());
            }

            return Mathf.Min(hardLimit - activeTemporaryWarriorCount, globalSlots);
        }

        private static async UniTaskVoid TimedSelfBuffAsync(
            MinionAI caster,
            float hpBonusRatio,
            float damageBonusRatio,
            float scaleMultiplier,
            float duration,
            CancellationToken token)
        {
            if (caster == null || caster.IsDead) return;

            float originalMaxHp = caster.maxHp;
            float originalDamage = caster.attackDamage;
            Vector3 originalScale = caster.transform.localScale;

            caster.maxHp = originalMaxHp * (1f + hpBonusRatio);
            caster.currentHp += caster.maxHp - originalMaxHp;
            caster.attackDamage = originalDamage * (1f + damageBonusRatio);
            caster.transform.localScale = originalScale * scaleMultiplier;

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token).SuppressCancellationThrow();
            }
            finally
            {
                if (caster != null)
                {
                    float hpRatio = caster.maxHp > 0f ? caster.currentHp / caster.maxHp : 1f;
                    caster.maxHp = originalMaxHp;
                    caster.currentHp = Mathf.Min(originalMaxHp, originalMaxHp * hpRatio);
                    caster.attackDamage = originalDamage;
                    caster.transform.localScale = originalScale;
                }
            }
        }

        private static async UniTaskVoid TimedCooldownBuffAsync(MinionAI caster, float cooldownMultiplier, float duration, CancellationToken token)
        {
            if (caster == null || caster.IsDead) return;

            float originalCooldown = caster.hitCooldown;
            caster.hitCooldown = Mathf.Max(0.05f, originalCooldown * cooldownMultiplier);

            try
            {
                await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: token).SuppressCancellationThrow();
            }
            finally
            {
                if (caster != null)
                    caster.hitCooldown = originalCooldown;
            }
        }

        private static async UniTaskVoid TemporarySummonAsync(MinionAI summon, float duration, CancellationToken ownerToken)
        {
            try
            {
                bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(duration), cancellationToken: ownerToken).SuppressCancellationThrow();
                if (!cancelled && summon != null && !summon.IsDead && summon.gameObject.activeInHierarchy)
                    summon.TakeDamage(summon.maxHp, summon);
            }
            finally
            {
                activeTemporaryWarriorCount = Mathf.Max(0, activeTemporaryWarriorCount - 1);
            }
        }

        private static async UniTaskVoid MeteorDelayAsync(MinionAI caster, MinionUniqueSkillData skill, Vector3 impactPosition, CancellationToken token)
        {
            bool cancelled = await UniTask.Delay(TimeSpan.FromSeconds(0.7f), cancellationToken: token).SuppressCancellationThrow();
            if (cancelled || token.IsCancellationRequested || caster == null || caster.IsDead || !caster.gameObject.activeInHierarchy) return;

            float damageMultiplier = skill.value > 0f ? skill.value : 2.2f;
            ApplyAreaDamage(caster, impactPosition, skill.range > 0f ? skill.range : 2.6f, caster.attackDamage * damageMultiplier, maxHits: 12);
            PlayFeedback(skill, impactPosition);
        }

        private static void ApplyAreaDamage(MinionAI caster, Vector3 position, float radius, float damage, int maxHits)
        {
            nearbyBuffer.Clear();
            GameManager.Instance.unitManager.GetNearbyUnitsNonAlloc(position, radius, nearbyBuffer);

            int hitCount = 0;
            for (int i = 0; i < nearbyBuffer.Count && hitCount < maxHits; i++)
            {
                UnitBase unit = nearbyBuffer[i];
                if (!IsValidEnemy(unit, null)) continue;

                unit.ApplyDamage(damage, caster);
                hitCount++;
            }
        }

        private static bool IsValidEnemy(UnitBase unit, Transform excludedTarget)
        {
            if (unit == null || unit.IsDead || unit is MinionAI || unit is PlayerController) return false;
            return excludedTarget == null || unit.transform != excludedTarget;
        }

        private static void PlayFeedback(MinionUniqueSkillData skill, Vector3 position)
        {
            if (skill == null) return;

            if (!string.IsNullOrEmpty(skill.effectPoolTag) && GameManager.Instance?.poolManager != null)
                GameManager.Instance.poolManager.Get(skill.effectPoolTag, position, Quaternion.identity);

            if (skill.soundCue != null && GameManager.Instance?.Sound != null)
                GameManager.Instance.Sound.PlaySFX(skill.soundCue, SfxPriority.High, 0.1f);
        }
    }
}
