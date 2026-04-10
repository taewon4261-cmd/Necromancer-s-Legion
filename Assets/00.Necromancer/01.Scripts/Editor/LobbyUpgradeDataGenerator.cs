using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using Necromancer;

namespace Necromancer.Editor
{
    // [InitializeOnLoad]
    public class LobbyUpgradeDataGenerator
    {
        private const string SAVE_PATH = "Assets/Necromancer/02.Data/Upgrades/";

        static LobbyUpgradeDataGenerator()
        {
            // 컴파일 즉시 자동 실행
            GenerateAssets();
        }

        public static void GenerateAssets()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Necromancer/02.Data")) AssetDatabase.CreateFolder("Assets/Necromancer", "02.Data");
            if (!AssetDatabase.IsValidFolder("Assets/Necromancer/02.Data/Upgrades")) AssetDatabase.CreateFolder("Assets/Necromancer/02.Data", "Upgrades");

            var dataList = new List<UpgradeDataSpec>
            {
                new UpgradeDataSpec("01_Upgrade_Resurrection", "부활의 메아리", "Upgrade_Resurrection_Lv", UpgradeStatType.Resurrection, "적 처치 시 미니언 부활 확률 +2%", 15, 1500, 1.0f, 2.0f),
                new UpgradeDataSpec("02_Upgrade_Health", "강인한 신체", "Upgrade_Health_Lv", UpgradeStatType.Health, "최대 체력 +10 증가", 10, 500, 0.5f, 10.0f),
                new UpgradeDataSpec("03_Upgrade_Attack", "사신의 낫", "Upgrade_Attack_Lv", UpgradeStatType.AttackDamage, "기본 공격(낫) 데미지 +5 증가", 10, 600, 0.6f, 5.0f),
                new UpgradeDataSpec("04_Upgrade_MoveSpeed", "가벼운 발걸음", "Upgrade_MoveSpeed_Lv", UpgradeStatType.MoveSpeed, "플레이어 이동 속도 +0.2 증가", 5, 800, 0.8f, 0.2f),
                new UpgradeDataSpec("05_Upgrade_AuraRange", "죽음의 오라", "Upgrade_AuraRange_Lv", UpgradeStatType.AuraRange, "부활 인식 범위 +0.5m 증가", 5, 700, 0.7f, 0.5f),
                new UpgradeDataSpec("06_Upgrade_SoulGain", "탐욕스러운 눈", "Upgrade_SoulGain_Lv", UpgradeStatType.SoulGain, "게임 종료 시 영혼 획득 +10%", 10, 1000, 0.6f, 10.0f),
                new UpgradeDataSpec("07_Upgrade_ExpGain", "명민한 영혼", "Upgrade_ExpGain_Lv", UpgradeStatType.ExpGain, "경험치 획득량 +10% 증가", 10, 900, 0.6f, 10.0f),
                new UpgradeDataSpec("08_Upgrade_Magnet", "영혼 갈무리", "Upgrade_MagnetRange_Lv", UpgradeStatType.MagnetRange, "보석 자석 흡수 범위 +1.0m 증가", 5, 600, 0.5f, 1.0f),
                new UpgradeDataSpec("09_Upgrade_MinionDamage", "군단의 분노", "Upgrade_MinionDamage_Lv", UpgradeStatType.MinionDamage, "모든 미니언 공격력 +2 증가", 10, 1200, 0.9f, 2.0f),
                new UpgradeDataSpec("10_Upgrade_MinionSpeed", "군단의 진격", "Upgrade_MinionSpeed_Lv", UpgradeStatType.MinionSpeed, "모든 미니언 이동 속도 +0.3 증가", 10, 1100, 0.9f, 0.3f),
                new UpgradeDataSpec("11_Upgrade_Reroll", "운명 뒤틀기", "Upgrade_RerollCount_Lv", UpgradeStatType.RerollCount, "레벨업 스킬 리롤 횟수 +1회", 3, 2000, 1.5f, 1.0f),
                new UpgradeDataSpec("12_Upgrade_CDR", "영혼의 순환", "Upgrade_CDR_Lv", UpgradeStatType.CooldownReduction, "재사용 대기시간 -5% 감소", 6, 1500, 1.0f, 0.05f),
                new UpgradeDataSpec("13_Upgrade_SkullPact", "묘지기의 서약", "Upgrade_StartMinionCount_Lv", UpgradeStatType.StartMinionCount, "시작 시 기본 미니언 수 +1마리 증가", 5, 1200, 0.8f, 1.0f)
            };

            foreach (var spec in dataList)
            {
                string path = SAVE_PATH + spec.fileName + ".asset";
                LobbyUpgradeSO asset = AssetDatabase.LoadAssetAtPath<LobbyUpgradeSO>(path);

                if (asset == null)
                {
                    asset = ScriptableObject.CreateInstance<LobbyUpgradeSO>();
                    AssetDatabase.CreateAsset(asset, path);
                }

                asset.upgradeName = spec.uiName;
                asset.saveKey = spec.saveKey;
                asset.statType = spec.statType;
                asset.description = spec.description;
                asset.maxLevel = spec.maxLevel;
                asset.baseCost = spec.baseCost;
                asset.costIncreaseFactor = spec.costFactor;
                asset.valuePerLevel = spec.valPerLv;

                EditorUtility.SetDirty(asset);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green><b>[UPGRADE DATA SYNC]</b></color> 13종의 업그레이드 에셋이 테이블 사양에 맞춰 자동 동기화되었습니다.");
        }

        private struct UpgradeDataSpec
        {
            public string fileName;
            public string uiName;
            public string saveKey;
            public UpgradeStatType statType;
            public string description;
            public int maxLevel;
            public int baseCost;
            public float costFactor;
            public float valPerLv;

            public UpgradeDataSpec(string f, string u, string s, UpgradeStatType st, string d, int m, int b, float cf, float v)
            {
                fileName = f; uiName = u; saveKey = s; statType = st; description = d;
                maxLevel = m; baseCost = b; costFactor = cf; valPerLv = v;
            }
        }
    }
}
