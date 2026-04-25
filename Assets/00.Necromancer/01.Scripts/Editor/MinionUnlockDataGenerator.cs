#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEngine.AddressableAssets;
using Necromancer.Data;

namespace Necromancer.Editor
{
    /// <summary>
    /// [EDITOR] 6종 미니언 해금 ScriptableObject 에셋을 일괄 생성합니다.
    /// 생성 위치: Assets/Resources/Minions/ (Resources.LoadAll 런타임 로딩 경로)
    /// 7번째 미니언 추가 시 이 스크립트에 항목 1줄만 추가하면 됩니다. (OCP)
    /// </summary>
    public class MinionUnlockDataGenerator
    {
        private const string DATA_PATH = "Assets/00.Necromancer/02.Data/Minions";
        private const string ICON_PATH = "Assets/00.Necromancer/04.Sprites/MinionIcons";

        [MenuItem("Necromancer/Generate Minion Unlock SOs (New Architecture)")]
        public static void GenerateAll()
        {
            EnsureFolders();

            // ────────────────────────────────────────────────────────────────
            // 미니언 정의 테이블
            // 컬럼: fileName, minionID, minionTag, minionName, targetEnemyID,
            //        soulCost, essenceCost, tier, description
            // ────────────────────────────────────────────────────────────────
            CreateOrUpdate("Minion_01_Warrior",
                minionID:      "Minion_01_Warrior",
                minionTag:     "Minion",
                minionName:    "해골 전사",
                targetEnemyID: "Enemy_01_Peasant",
                soulCost:      0,
                essenceCost:   0,
                tier:          MinionTier.Bronze,
                desc:          "기본 근접 전투 미니언. 처음부터 해금된 상태로 시작합니다.");

            CreateOrUpdate("Minion_02_Archer",
                minionID:      "Minion_02_Archer",
                minionTag:     "Minion_Archer",
                minionName:    "해골 궁수",
                targetEnemyID: "Enemy_02_Archer",
                soulCost:      300,
                essenceCost:   10,
                tier:          MinionTier.Bronze,
                desc:          "원거리 투사체 공격. 군중 속에서 후방 지원에 특화됩니다.");

            CreateOrUpdate("Minion_03_Mage",
                minionID:      "Minion_03_Mage",
                minionTag:     "Minion_Mage",
                minionName:    "해골 마법사",
                targetEnemyID: "Enemy_03_Mage",
                soulCost:      600,
                essenceCost:   8,
                tier:          MinionTier.Silver,
                desc:          "광역 마법 공격. 밀집된 적을 상대로 폭발적인 화력을 발휘합니다.");

            CreateOrUpdate("Minion_04_Giant",
                minionID:      "Minion_04_Giant",
                minionTag:     "Minion_Giant",
                minionName:    "해골 거인",
                targetEnemyID: "Enemy_04_Knight",
                soulCost:      1000,
                essenceCost:   15,
                tier:          MinionTier.Silver,
                desc:          "최고 수준의 체력과 돌진 충격. 전선을 유지하는 탱커 역할입니다.");

            CreateOrUpdate("Minion_05_Shaman",
                minionID:      "Minion_05_Shaman",
                minionTag:     "Minion_Shaman",
                minionName:    "해골 주술사",
                targetEnemyID: "Enemy_05_Shaman",
                soulCost:      1500,
                essenceCost:   12,
                tier:          MinionTier.Gold,
                desc:          "주변 아군 미니언에게 보호막을 부여하는 지원형 미니언입니다.");

            CreateOrUpdate("Minion_06_Knight",
                minionID:      "Minion_06_Knight",
                minionTag:     "Minion_Knight",
                minionName:    "어둠의 기사",
                targetEnemyID: "Enemy_06_Champion",
                soulCost:      2000,
                essenceCost:   20,
                tier:          MinionTier.Gold,
                desc:          "강화된 갑옷과 무기를 갖춘 최강의 근접 미니언. 챔피언 급 적에게서 정수를 획득합니다.");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=gold><b>[MinionGenerator]</b></color> 6종 미니언 해금 에셋 생성/동기화 완료! (Path: 00.Necromancer/02.Data/Minions)");
        }

        private static void CreateOrUpdate(
            string fileName, string minionID, string minionTag, string minionName,
            string targetEnemyID, int soulCost, int essenceCost, MinionTier tier, string desc)
        {
            string fullPath = $"{DATA_PATH}/{fileName}.asset";
            MinionUnlockSO so = AssetDatabase.LoadAssetAtPath<MinionUnlockSO>(fullPath);

            if (so == null)
            {
                so = ScriptableObject.CreateInstance<MinionUnlockSO>();
                AssetDatabase.CreateAsset(so, fullPath);
            }

            so.minionID           = minionID;
            so.minionTag          = minionTag;
            so.minionName         = minionName;
            so.targetEnemyID      = targetEnemyID;
            so.unlockCost_Soul    = soulCost;
            so.unlockCost_Essence = essenceCost;
            so.tier               = tier;
            so.description        = desc;

            // 아이콘 자동 바인딩 (AssetReferenceSprite)
            string[] guids = AssetDatabase.FindAssets(fileName, new[] { ICON_PATH });
            if (guids.Length > 0)
            {
                string spriteGuid = guids[0];
                if (so.minionIcon == null || so.minionIcon.AssetGUID != spriteGuid)
                    so.minionIcon = new AssetReferenceSprite(spriteGuid);
            }
            else
            {
                Debug.LogWarning($"[MinionGenerator] 아이콘 미발견: {fileName} (나중에 인스펙터에서 직접 할당하세요)");
            }

            EditorUtility.SetDirty(so);
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer"))
                AssetDatabase.CreateFolder("Assets", "00.Necromancer");

            if (!AssetDatabase.IsValidFolder("Assets/00.Necromancer/02.Data"))
                AssetDatabase.CreateFolder("Assets/00.Necromancer", "02.Data");

            if (!AssetDatabase.IsValidFolder(DATA_PATH))
                AssetDatabase.CreateFolder("Assets/00.Necromancer/02.Data", "Minions");
        }
    }
}
#endif
