#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Necromancer.Data;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] 02.Data/Minions 의 MinionUnlockSO 를 스캔해
    /// 미니언마다 Override Controller 를 자동 생성·클립 연결·SO 링크합니다.
    /// 새 미니언 추가 시 SO와 애니메이션 클립만 넣고 이 메뉴를 한 번 실행하면 됩니다.
    ///
    /// 클립 명명 규칙: {파일명 앞 두 토큰}_{Move|Attack|Die}
    ///   예) Minion_02_SkeletonWolf.asset → Minion_02_Move / Minion_02_Attack / Minion_02_Die
    /// </summary>
    public class MinionControllerGenerator
    {
        private const string SO_PATH       = "Assets/00.Necromancer/02.Data/Minions";
        private const string OC_OUTPUT     = "Assets/00.Necromancer/04.Sprites/Minion";
        private const string TEMPLATE_PATH = "Assets/00.Necromancer/04.Sprites/Minion/OC_Minion_Peasant.overrideController";
        private const string ANIM_ROOT     = "Assets/00.Necromancer/04.Sprites";

        [MenuItem("Necromancer/Generate Minion Override Controllers")]
        public static void GenerateAll()
        {
            AnimatorOverrideController template =
                AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(TEMPLATE_PATH);

            if (template == null)
            {
                Debug.LogError("[MinionControllerGen] 템플릿 OC 를 찾을 수 없습니다: " + TEMPLATE_PATH);
                return;
            }

            RuntimeAnimatorController baseController = template.runtimeAnimatorController;

            // 템플릿의 슬롯 구조 파악 (원본 클립 이름으로 Move/Attack/Die 매핑)
            var templateSlots = new List<KeyValuePair<AnimationClip, AnimationClip>>();
            template.GetOverrides(templateSlots);

            string[] soGuids = AssetDatabase.FindAssets("t:MinionUnlockSO", new[] { SO_PATH });

            int created = 0;
            int linked  = 0;

            for (int i = 0; i < soGuids.Length; i++)
            {
                string soPath = AssetDatabase.GUIDToAssetPath(soGuids[i]);
                MinionUnlockSO so = AssetDatabase.LoadAssetAtPath<MinionUnlockSO>(soPath);
                if (so == null) continue;

                // "Minion_02_SkeletonWolf" → prefix "Minion_02"
                string assetName = System.IO.Path.GetFileNameWithoutExtension(soPath);
                string[] parts   = assetName.Split('_');
                if (parts.Length < 2) continue;
                string prefix = parts[0] + "_" + parts[1]; // "Minion_02"

                string ocPath = $"{OC_OUTPUT}/OC_{assetName}.overrideController";
                AnimatorOverrideController oc =
                    AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(ocPath);

                if (oc == null)
                {
                    oc = new AnimatorOverrideController(baseController);
                    AssetDatabase.CreateAsset(oc, ocPath);
                    created++;
                }

                // 슬롯마다 prefix_Move / prefix_Attack / prefix_Die 클립 탐색 후 적용
                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                oc.GetOverrides(overrides);

                bool dirty = false;
                for (int j = 0; j < overrides.Count; j++)
                {
                    AnimationClip original = overrides[j].Key;
                    if (original == null) continue;

                    string keyword   = ExtractKeyword(original.name);
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

                // SO 의 animatorController 필드에 연결
                if (so.animatorController != oc)
                {
                    so.animatorController = oc;
                    EditorUtility.SetDirty(so);
                    linked++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"<color=gold><b>[MinionControllerGen]</b></color> 완료! " +
                      $"생성 {created}개 / SO 연결 {linked}개 / 총 처리 {soGuids.Length}개");
        }

        // 클립 이름에서 Move / Attack / Die 키워드를 추출합니다.
        private static string ExtractKeyword(string clipName)
        {
            string lower = clipName.ToLower();
            if (lower.Contains("move"))   return "Move";
            if (lower.Contains("attack")) return "Attack";
            if (lower.Contains("die"))    return "Die";
            return null;
        }

        // ANIM_ROOT 하위에서 "{prefix}_{keyword}" 이름의 AnimationClip 을 탐색합니다.
        private static AnimationClip FindClip(string prefix, string keyword)
        {
            string[] guids = AssetDatabase.FindAssets(
                $"{prefix}_{keyword} t:AnimationClip", new[] { ANIM_ROOT });

            if (guids.Length > 0)
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    AssetDatabase.GUIDToAssetPath(guids[0]));

            return null;
        }
    }
}
#endif
