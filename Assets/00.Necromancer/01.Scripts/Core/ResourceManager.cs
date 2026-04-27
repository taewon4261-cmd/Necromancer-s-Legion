using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Necromancer.Systems;

namespace Necromancer.Core {
    public class ResourceManager : MonoBehaviour 
    {
        public static ResourceManager Instance;

        public int currentSoul;
        public int currentSessionSoul; // 이번 판에서 얻은 실시간 소울 (UI 표현용)
        public int unlockedStageLevel;

        [Header("Stamina System")]
        public const int MAX_STAMINA = 10;
        public const int STAMINA_RECOVERY_SECONDS = 1800; // 30분
        public const int STAMINA_COST = 1;
        public const int MAX_DAILY_STAMINA_ADS = 5;

        public int currentStamina;
        public int staminaAdsWatchedToday;
        private string lastStaminaAdDate;
        private long lastStaminaUpdateTimeTicks;

        public float SecondsUntilNextStamina { get; private set; }

        // [INSPECTOR] Resources.LoadAll 대신 Inspector 직렬화 사용 (Resources 폴더 의존 제거)
        [SerializeField] private List<LobbyUpgradeSO> upgradeSOConfig = new List<LobbyUpgradeSO>();
        private List<LobbyUpgradeSO> upgradeList = new List<LobbyUpgradeSO>();

        // [DATA-SAFETY] 정수 누적 자동 저장 — N개 쌓일 때마다 파일에 기록하여 크래시 대비
        private int essenceCountSinceLastSave = 0;
        private const int ESSENCE_AUTOSAVE_THRESHOLD = 5;
        private bool _isInitialized = false;

        // 이번 세션(판)에서 얻은 정수 - 결과창 표시용, 저장 안 함
        public Dictionary<string, int> currentSessionEssences { get; private set; } = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public void Init() {
            if (_isInitialized)
            {
                // 클라우드 로드 후 재호출: 세션 카운터·upgradeList 재설정 없이 데이터만 갱신
                if (GameManager.Instance?.SaveData?.Data != null)
                {
                    var d = GameManager.Instance.SaveData.Data;
                    currentSoul = d.currentSoul;
                    unlockedStageLevel = d.unlockedStageLevel;
                    currentStamina = d.currentStamina;
                    lastStaminaUpdateTimeTicks = d.lastStaminaUpdateTimeTicks;
                    staminaAdsWatchedToday = d.staminaAdsWatchedToday;
                    lastStaminaAdDate = d.lastStaminaAdDate;
                    UpdateStamina();
                    foreach (var u in upgradeList)
                        if (u != null) u.LoadLevel();
                    GameManager.BroadcastSoul(currentSoul);
                    Debug.Log($"<color=cyan>[ResourceManager]</color> Refreshed from cloud data. Soul: {currentSoul}");
                }
                return;
            }
            _isInitialized = true;

            currentSessionSoul = 0;
            essenceCountSinceLastSave = 0;
            currentSessionEssences.Clear();
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null) {
                var data = GameManager.Instance.SaveData.Data;
                currentSoul = data.currentSoul;
                unlockedStageLevel = data.unlockedStageLevel;
                currentStamina = data.currentStamina;
                lastStaminaUpdateTimeTicks = data.lastStaminaUpdateTimeTicks;
                staminaAdsWatchedToday = data.staminaAdsWatchedToday;
                lastStaminaAdDate = data.lastStaminaAdDate;

                UpdateStamina();
            }
            // [INSPECTOR] Inspector에서 직접 연결된 SO 리스트 사용 (Resources 폴더 불필요)
            upgradeList = upgradeSOConfig;
            if (upgradeList == null || upgradeList.Count == 0)
            {
                Debug.LogError("<color=red>[ResourceManager]</color> upgradeSOConfig is EMPTY! Assign LobbyUpgradeSO list in Inspector.");
            }

            // [DATA.md] 중앙 집중형 데이터 시스템을 사용하도록 로드 시점 일원화
            foreach (var u in upgradeList)
            {
                if (u != null) u.LoadLevel(); // 내부적으로 GameManager.SaveData를 사용
            }

            // [STABILITY] 모든 데이터 로드 및 업그레이드 레벨 동기화가 완료된 후 강제 브로드캐스트
            GameManager.BroadcastSoul(currentSoul);
            Debug.Log($"<color=green>[ResourceManager]</color> Initialization complete. Broadcasted soul: {currentSoul}");

            // [NOTIF] 초기화 시점에 기존 알림 취소
            if (GameManager.Instance != null && GameManager.Instance.Notification != null)
            {
                GameManager.Instance.Notification.CancelAllNotifications();
            }
        }

        public void UpdateStamina()
        {
            string todayUtc = DateTime.UtcNow.ToString("yyyy-MM-dd");
            if (lastStaminaAdDate != todayUtc)
            {
                staminaAdsWatchedToday = 0;
                lastStaminaAdDate = todayUtc;
                SaveStamina(true);
            }

            if (currentStamina >= MAX_STAMINA)
            {
                SecondsUntilNextStamina = STAMINA_RECOVERY_SECONDS;
                lastStaminaUpdateTimeTicks = DateTime.UtcNow.Ticks;
                SaveStamina();
                return;
            }

            DateTime now = DateTime.UtcNow;
            DateTime lastUpdate = new DateTime(lastStaminaUpdateTimeTicks);

            if (lastStaminaUpdateTimeTicks == 0)
            {
                lastStaminaUpdateTimeTicks = now.Ticks;
                SaveStamina();
                return;
            }

            if (lastUpdate > now)
            {
                Debug.LogWarning("[ResourceManager] Time rollback detected. Resetting stamina update time.");
                lastStaminaUpdateTimeTicks = now.Ticks;
                lastUpdate = now;
            }

            TimeSpan elapsed = now - lastUpdate;
            double totalSeconds = elapsed.TotalSeconds;

            if (totalSeconds >= STAMINA_RECOVERY_SECONDS)
            {
                int recoverAmount = (int)(totalSeconds / STAMINA_RECOVERY_SECONDS);
                currentStamina = Mathf.Min(MAX_STAMINA, currentStamina + recoverAmount);
                lastStaminaUpdateTimeTicks = lastUpdate.AddSeconds(recoverAmount * STAMINA_RECOVERY_SECONDS).Ticks;
                SaveStamina(true);
            }

            double remainingSeconds = STAMINA_RECOVERY_SECONDS - (totalSeconds % STAMINA_RECOVERY_SECONDS);
            SecondsUntilNextStamina = (float)remainingSeconds;
        }

        public bool HasEnoughStamina(int amount) => currentStamina >= amount;

        public void ConsumeStamina(int amount)
        {
            if (currentStamina >= amount)
            {
                int prevStamina = currentStamina;
                currentStamina -= amount;

                if (prevStamina >= MAX_STAMINA && currentStamina < MAX_STAMINA)
                {
                    lastStaminaUpdateTimeTicks = DateTime.UtcNow.Ticks;
                    Debug.Log("<color=cyan>[Stamina]</color> Stamina dropped below max. Timer Reset.");
                }

                SaveStamina(true);
            }
        }

        public void AddStamina(int amount)
        {
            currentStamina += amount;
            staminaAdsWatchedToday++;
            lastStaminaAdDate = DateTime.UtcNow.ToString("yyyy-MM-dd");
            SaveStamina(true);
        }

        private void SaveStamina(bool forcePhysicalSave = false)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                var data = GameManager.Instance.SaveData.Data;
                data.currentStamina = currentStamina;
                data.lastStaminaUpdateTimeTicks = lastStaminaUpdateTimeTicks;
                data.staminaAdsWatchedToday = staminaAdsWatchedToday;
                data.lastStaminaAdDate = lastStaminaAdDate;

                if (forcePhysicalSave)
                    GameManager.Instance.SaveData.Save();
            }
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
            // [UPGRADE] 영혼 획득량 보너스 적용 (정수 퍼센트 -> 소수점 퍼센트 변환)
            float bonus = GetUpgradeValue(UpgradeStatType.SoulGain) * 0.01f; 
            int finalAmount = Mathf.Max(1, Mathf.RoundToInt(a * (1f + bonus)));

            currentSessionSoul += finalAmount;
            currentSoul += finalAmount; // [DATA-SAFETY] 이번 판 획득량을 즉시 전체 지갑에 합산
            // [SOUND] 소울 획득 효과음 재생
            if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSoulGain);
            }

            // 데이터 동합성 유지 (게임 종료 전 비정상 종료 시 피해 최소화)
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                // [NOTE] 매 획득마다 저장(File I/O)하면 부하가 생길 수 있으므로 메모리 값만 유지하고 종료 시 일괄 저장
            }

            GameManager.BroadcastSoul(currentSoul);               // 로비 UI (UpgradeUI, MinionAltarUI) — 전체 보유량
            GameManager.BroadcastSessionSoul(currentSessionSoul); // 인게임 HUD — 세션 획득량 (0부터 시작)
        }

        private void OnApplicationPause(bool pause)
        {
            if (pause)
            {
                // [NOTIF] 백그라운드 전환 시 알림 예약
                ScheduleStaminaNotification();
            }
            else
            {
                // [NOTIF] 포그라운드 복귀 시 알림 취소 및 스태미나 갱신
                UpdateStamina();
                if (GameManager.Instance != null && GameManager.Instance.Notification != null)
                {
                    GameManager.Instance.Notification.CancelAllNotifications();
                }
            }
        }

        private void OnApplicationQuit()
        {
            // [NOTIF] 앱 종료 시 알림 예약
            ScheduleStaminaNotification();

            // 강제 종료 시에도 데이터 유실을 막기 위한 최후의 방어선
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                GameManager.Instance.SaveData.Save();
                Debug.Log("<color=orange>[ResourceManager]</color> Application quitting. Forced Save executed.");
            }
        }

        private void ScheduleStaminaNotification()
        {
            if (currentStamina < MAX_STAMINA && GameManager.Instance != null && GameManager.Instance.Notification != null)
            {
                int staminaNeeded = MAX_STAMINA - currentStamina;
                float totalSeconds = ((staminaNeeded - 1) * STAMINA_RECOVERY_SECONDS) + SecondsUntilNextStamina;
                
                GameManager.Instance.Notification.ScheduleStaminaNotification(Mathf.RoundToInt(totalSeconds));
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

                // [SOUND] 업그레이드 완료(구매) 효과음 재생
                if (GameManager.Instance != null && GameManager.Instance.Sound != null) {
                    GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxUpgrade);
                }

                if (GameManager.Instance != null && GameManager.Instance.SaveData != null) {
                    GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                    GameManager.Instance.SaveData.Save();
                }
                return true; 
            } return false; 
        }

        #region Minion Unlock System (Altar of Souls)
        /// <summary>
        /// [LOGIC] 적 처치 시 획득한 정수를 데이터에 가산합니다.
        /// </summary>
        public void AddEssence(string enemyID, int amount)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null) return;
            var data = GameManager.Instance.SaveData.Data;

            if (!data.minionEssences.ContainsKey(enemyID))
                data.minionEssences[enemyID] = 0;

            data.minionEssences[enemyID] += amount;

            // 세션 기록 (결과창 표시용)
            if (!currentSessionEssences.ContainsKey(enemyID))
                currentSessionEssences[enemyID] = 0;
            currentSessionEssences[enemyID] += amount;

            // [DATA-SAFETY] 5개 누적마다 자동 저장 — 크래시 시 최대 4개만 유실
            essenceCountSinceLastSave += amount;
            if (essenceCountSinceLastSave >= ESSENCE_AUTOSAVE_THRESHOLD)
            {
                essenceCountSinceLastSave = 0;
                GameManager.Instance.SaveData.Save();
                Debug.Log($"<color=blue>[ResourceManager]</color> Essence auto-saved. (threshold: {ESSENCE_AUTOSAVE_THRESHOLD})");
            }

            Debug.Log($"<color=blue>[ResourceManager]</color> Essence Gained: {enemyID} (+{amount})");
        }

        public int GetEssenceCount(string enemyID)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null) return 0;
            return GameManager.Instance.SaveData.Data.minionEssences.TryGetValue(enemyID, out int count) ? count : 0;
        }

        /// <summary>
        /// [LOGIC] 정수를 차감합니다. 보유량이 부족하면 false를 반환합니다.
        /// </summary>
        public bool SpendEssence(string enemyID, int amount)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null) return false;
            var data = GameManager.Instance.SaveData.Data;
            int owned = data.minionEssences.TryGetValue(enemyID, out int c) ? c : 0;
            if (owned < amount) return false;
            data.minionEssences[enemyID] = owned - amount;
            return true;
        }

        public bool IsMinionUnlocked(string minionID)
        {
            if (GameManager.Instance == null || GameManager.Instance.SaveData == null) return false;
            return GameManager.Instance.SaveData.Data.unlockedMinionIDs.Contains(minionID);
        }

        /// <summary>
        /// [LOGIC] 비용(소울+정수)을 검사하고 미니언을 영구 해금합니다.
        /// </summary>
        public bool TryUnlockMinion(Necromancer.Data.MinionUnlockSO minionData)
        {
            if (minionData == null || IsMinionUnlocked(minionData.minionID)) return false;

            int currentEssence = GetEssenceCount(minionData.targetEnemyID);
            if (currentSoul < minionData.unlockCost_Soul || currentEssence < minionData.unlockCost_Essence)
                return false;

            var data = GameManager.Instance.SaveData.Data;

            // 소울 차감 (SpendSoul 대신 직접 수정 — Save() 중복 호출 방지)
            currentSoul -= minionData.unlockCost_Soul;
            data.currentSoul = currentSoul;
            GameManager.BroadcastSoul(currentSoul);

            // 정수 차감 (SpendEssence 대신 직접 수정 — 동일 이유)
            data.minionEssences[minionData.targetEnemyID] = currentEssence - minionData.unlockCost_Essence;

            // 해금 등록
            data.unlockedMinionIDs.Add(minionData.minionID);

            // 모든 데이터 변경 완료 후 단 한 번만 저장
            GameManager.Instance.SaveData.Save();

            if (GameManager.Instance?.Sound != null)
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxUpgrade);

            Debug.Log($"<color=gold>[ResourceManager]</color> Minion Permanently Unlocked: {minionData.minionName}");
            return true;
        }
        #endregion
        /// <summary>
        /// [REWARD-AD] 결과창 2배 보상 광고 시청 완료 시 호출.
        /// 이번 판 획득 소울(bonus)만큼 추가 지급하고 Firestore까지 즉시 동기화합니다.
        /// </summary>
        public void DoubleSessionSoul(int bonus)
        {
            currentSoul += bonus;
            GameManager.BroadcastSoul(currentSoul);

            if (GameManager.Instance != null && GameManager.Instance.SaveData != null)
            {
                GameManager.Instance.SaveData.Data.currentSoul = currentSoul;
                GameManager.Instance.SaveData.Save(); // 로컬 저장 + Firestore 자동 업로드
                Debug.Log($"<color=green>[ResourceManager]</color> DoubleSessionSoul: +{bonus} 지급 완료. 총 소울: {currentSoul}");
            }
        }

        /// <summary>
        /// [NEW] 특정 업그레이드의 현재 레벨을 조회합니다. (GameManager에서 호출)
        /// </summary>
        public int GetUpgradeLevel(string saveKey)
        {
            if (GameManager.Instance != null && GameManager.Instance.SaveData != null && GameManager.Instance.SaveData.Data != null)
            {
                if (GameManager.Instance.SaveData.Data.upgradeDict.TryGetValue(saveKey, out int level))
                {
                    return level;
                }
            }
            return 0;
        }
    }
}
