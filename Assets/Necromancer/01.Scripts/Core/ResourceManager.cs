using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace Necromancer.Core {
    public class ResourceManager : MonoBehaviour {
        public int currentSoul;
        public int currentSessionSoul; // 이번 판에서 얻은 실시간 소울 (UI 표현용)
        public int unlockedStageLevel;
        private List<LobbyUpgradeSO> upgradeList = new List<LobbyUpgradeSO>();
        public void Init() {
            currentSessionSoul = 0; // [DATA-SAFETY] 새로운 세션 시작 시 획득량 초기화
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null) {
                var data = GameManager.Instance.SaveData.Data;
                currentSoul = data.currentSoul;
                unlockedStageLevel = data.unlockedStageLevel;
            }
            upgradeList = Resources.LoadAll<LobbyUpgradeSO>("Upgrades").ToList();
            
            // [DATA.md] 중앙 집중형 데이터 시스템을 사용하도록 로드 시점 일원화
            foreach (var u in upgradeList) 
            {
                if (u != null) u.LoadLevel(); // 내부적으로 GameManager.SaveData를 사용
            }

            // [STABILITY] 모든 데이터 로드 및 업그레이드 레벨 동기화가 완료된 후 강제 브로드캐스트
            GameManager.BroadcastSoul(currentSoul);
            Debug.Log($"<color=green>[ResourceManager]</color> Initialization complete. Broadcasted soul: {currentSoul}");
        }
        public float GetUpgradeValue(UpgradeStatType t) => upgradeList?.Where(u => u != null && u.statType == t).Sum(u => u.GetTotalStatValue()) ?? 0f;
        public bool IsStageUnlocked(int id) => id <= unlockedStageLevel;
        public void UnlockLevel(int id) { 
            if (id > unlockedStageLevel) { 
                unlockedStageLevel = id; 
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.unlockedStageLevel = id;
                    GameManager.Instance.SaveData.Save();
                }
            } 
        }

        /// <summary>
        /// 인게임에서 적을 처치하거나 보석을 먹어 소울을 획득할 때 호출
        /// </summary>
        public void AddSoul(int a) { 
            currentSessionSoul += a; 
            currentSoul += a; // [DATA-SAFETY] 이번 판 획득량을 즉시 전체 지갑에 합산

            // 데이터 동합성 유지 (게임 종료 전 비정상 종료 시 피해 최소화)
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                // [NOTE] 매 획득마다 저장(File I/O)하면 부하가 생길 수 있으므로 메모리 값만 유지하고 종료 시 일괄 저장
            }

            // UI에는 세션 소울(이번 판 획득량)만 전달하여 0부터 시작하게 함
            GameManager.BroadcastSoul(currentSessionSoul);
        }

        private void OnApplicationQuit()
        {
            // 강제 종료 시에도 데이터 유실을 막기 위한 최후의 방어선
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                GameManager.Instance.SaveData.Save();
                Debug.Log("<color=orange>[ResourceManager]</color> Application quitting. Forced Save executed.");
            }
        }

        /// <summary>
        /// 게임이 정상 종료(클리어/실패)될 때 현재 지갑 상태를 최종 저장
        /// </summary>
        public void CommitSessionSoul() {
            // [NOTE] 이미 AddSoul에서 currentSoul에 실시간 가산되었으므로, 여기서는 중복 합산을 피하고 저장만 수행합니다.
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                GameManager.Instance.SaveData.Save();
                
                // 결과창 등에서 사용된 뒤, 다음 세션을 위해 세션 소울 초기화 (필요 시)
                Debug.Log($"<color=green>[ResourceManager]</color> Session committed and saved. Total: {currentSoul}");
            }
        }

        public bool SpendSoul(int a) { 
            if (currentSoul >= a) { 
                currentSoul -= a; 
                GameManager.BroadcastSoul(currentSoul);
                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                    GameManager.Instance.SaveData.Save();
                }
                return true; 
            } return false; 
        }
    }
}
