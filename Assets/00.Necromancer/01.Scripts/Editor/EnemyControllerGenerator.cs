#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AddressableAssets;
using System.Collections.Generic;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] 02.Data/Generated 의 EnemyData SO를 스캔해
    /// 적마다 AnimatorOverrideController를 자동 생성·클립 연결·SO 링크합니다.
    ///
    /// 새 적 추가 시:
    ///   1. Assets/00.Necromancer/04.Sprites/Enemy/ 에 PNG 스프라이트 시트 추가
    ///      명명 규칙: Enemy_{N:02}_{Move|Attack|Die}.png  (예: Enemy_03_Move.png)
    ///   2. [Full Pipeline] Generate Enemy Animations + Controllers 실행
    ///      → 클립 자동 생성 → OC 자동 생성 → SO 자동 연결 한 번에 완료
    ///
    /// prefix 추출 방식: SO 파일명의 숫자를 0 패딩하여 클립명과 일치시킴
    ///   Enemy_3_훈련병.asset → 숫자 3 → prefix "Enemy_03" → Enemy_03_Move.anim
    /// </summary>
    public class EnemyControllerGenerator
    {
        private const string SO_PATH       = "Assets/00.Necromancer/02.Data/Generated";
        private const string OC_OUTPUT     = "Assets/00.Necromancer/04.Sprites/Enemy";
        private const string TEMPLATE_PATH = "Assets/00.Necromancer/04.Sprites/Enemy/OC_Enemy_Peasant.overrideController";
        private const string ANIM_ROOT     = "Assets/00.Necromancer/04.Sprites/Enemy";

        [MenuItem("Necromancer/Generate Enemy Override Controllers")]
        public static void GenerateAll()
        {
            var template = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(TEMPLATE_PATH);
            if (template == null)
            {
                Debug.LogError("[EnemyControllerGen] 템플릿 OC를 찾을 수 없습니다: " + TEMPLATE_PATH);
                return;
            }

            RuntimeAnimatorController baseController = template.runtimeAnimatorController;

            var templateSlots = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            template.GetOverrides(templateSlots);

            string[] soGuids = AssetDatabase.FindAssets("t:EnemyData", new[] { SO_PATH });

            int created = 0, linked = 0, skipped = 0;

            foreach (string soGuid in soGuids)
            {
                string soPath = AssetDatabase.GUIDToAssetPath(soGuid);
                var data = AssetDatabase.LoadAssetAtPath<EnemyData>(soPath);
                if (data == null) continue;

                // SO 파일명에서 숫자를 뽑아 0 패딩 → 클립명과 일치시킴
                // Enemy_3_훈련병.asset → "Enemy_03"
                string prefix = ExtractPrefixFromFilename(soPath);
                if (string.IsNullOrEmpty(prefix))
                {
                    Debug.LogWarning($"[EnemyControllerGen] SO 파일명 숫자 추출 실패, 건너뜀: {soPath}");
                    skipped++;
                    continue;
                }

                // 이 prefix에 해당하는 클립이 하나라도 있는지 먼저 확인
                if (!HasAnyClip(prefix))
                {
                    Debug.Log($"[EnemyControllerGen] 클립 없음, 건너뜀: {prefix} ({data.enemyID})");
                    skipped++;
                    continue;
                }

                string ocPath = $"{OC_OUTPUT}/OC_{prefix}.overrideController";
                var oc = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ocPath);

                if (oc == null)
                {
                    oc = new AnimatorOverrideController(baseController);
                    AssetDatabase.CreateAsset(oc, ocPath);
                    created++;
                }

                // 슬롯에 클립 연결
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                oc.GetOverrides(overrides);

                bool dirty = false;
                for (int j = 0; j < overrides.Count; j++)
                {
                    AnimationClip original = overrides[j].Key;
                    if (original == null) continue;

                    string keyword = ExtractKeyword(original.name);
                    if (keyword == null) continue;

                    AnimationClip found = FindClip(prefix, keyword);
                    if (found != null && overrides[j].Value != found)
                    {
                        overrides[j] = new KeyValuePair<AnimationClip, AnimationClip>(original, found);
                        dirty = true;
                    }
                }

                if (dirty)
                {
                    oc.ApplyOverrides(overrides);
                    EditorUtility.SetDirty(oc);
                }

                // EnemyData SO의 animatorController 필드에 연결 (AssetReferenceT)
                string ocGuid2 = AssetDatabase.AssetPathToGUID(ocPath);
                if (data.animatorController == null || data.animatorController.AssetGUID != ocGuid2)
                {
                    data.animatorController = new AssetReferenceT<RuntimeAnimatorController>(ocGuid2);
                    EditorUtility.SetDirty(data);
                    linked++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=gold><b>[EnemyControllerGen]</b></color> 완료! " +
                      $"생성 {created}개 / SO 연결 {linked}개 / 건너뜀 {skipped}개 / 총 {soGuids.Length}개");
        }

        /// <summary>SO 파일명 "Enemy_3_훈련병.asset" → "Enemy_03" (0 패딩 적용)</summary>
        private static string ExtractPrefixFromFilename(string soPath)
        {
            string fileName = System.IO.Path.GetFileNameWithoutExtension(soPath); // "Enemy_3_훈련병"
            string[] parts = fileName.Split('_');
            // parts[0]="Enemy", parts[1]="3", parts[2]="훈련병"
            if (parts.Length < 2) return null;
            if (!int.TryParse(parts[1], out int num)) return null;
            return $"Enemy_{num:D2}"; // "Enemy_03"
        }

        private static bool HasAnyClip(string prefix)
        {
            string[] keywords = { "Move", "Attack", "Die" };
            foreach (string kw in keywords)
            {
                string[] guids = AssetDatabase.FindAssets($"{prefix}_{kw} t:AnimationClip", new[] { ANIM_ROOT });
                if (guids.Length > 0) return true;
            }
            return false;
        }

        private static string ExtractKeyword(string clipName)
        {
            string lower = clipName.ToLower();
            if (lower.Contains("move"))   return "Move";
            if (lower.Contains("attack")) return "Attack";
            if (lower.Contains("die"))    return "Die";
            return null;
        }

        private static AnimationClip FindClip(string prefix, string keyword)
        {
            string[] guids = AssetDatabase.FindAssets($"{prefix}_{keyword} t:AnimationClip", new[] { ANIM_ROOT });
            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
            return null;
        }
    }
}
#endif
