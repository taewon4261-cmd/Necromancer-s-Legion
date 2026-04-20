
using System.Collections.Generic;
using UnityEngine;
using Necromancer.Core;

namespace Necromancer
{
    /// <summary>
    /// [ARCHITECT] 통합 유닛 관리자 (Unified Update Manager + Spatial Partitioning)
    /// 모든 유닛의 Update/FixedUpdate를 중앙에서 관리하고, 격자 기반 탐색을 제공합니다.
    /// </summary>
    public class UnitManager : MonoBehaviour
    {
        [Header("Update Optimization")]
        public List<UnitBase> allUnits = new List<UnitBase>(512);
        // [STABILITY] 프레임 중간에 리스트가 수정되어 에러나는 것을 방지하기 위한 버퍼
        private List<UnitBase> pendingRegister = new List<UnitBase>(64);
        private List<UnitBase> pendingUnregister = new List<UnitBase>(64);

        [Header("Minion Spawn Settings")]
        public float baseReviveChance = 30f;
        public string minionPoolTag = "Minion";
        private List<Necromancer.Data.MinionUnlockSO> unlockedMinionDatas = new List<Necromancer.Data.MinionUnlockSO>();

        [Header("Grid Partitioning Settings")]
        [Tooltip("격자 한 칸의 크기 (유닛의 평균 탐색 반경 고려)")]
        public float gridCellSize = 2.0f;
        
        // 격자 좌표별 유닛 리스트 (공간 분할)
        private Dictionary<Vector2Int, List<UnitBase>> unitGrid = new Dictionary<Vector2Int, List<UnitBase>>(128);

        private void Awake()
        {
            allUnits.Clear();
            unitGrid.Clear();
            pendingRegister.Clear();
            pendingUnregister.Clear();
        }

        public void RegisterUnit(UnitBase unit)
        {
            // 즉시 추가하지 않고 대기열에 삽입하여 루프 안전성 확보
            if (!allUnits.Contains(unit) && !pendingRegister.Contains(unit))
            {
                pendingRegister.Add(unit);
                // [CAUTION] 등록 직후에는 아직 allUnits에 없으므로, 한 프레임 뒤부터 업데이트됨
            }
            
            // 제거 대기열에 있다면 취소
            if (pendingUnregister.Contains(unit)) pendingUnregister.Remove(unit);
        }

        public void UnregisterUnit(UnitBase unit)
        {
            if (!pendingUnregister.Contains(unit))
            {
                pendingUnregister.Add(unit);
            }
            
            // 등록 대기열에 있다면 취소
            if (pendingRegister.Contains(unit)) pendingRegister.Remove(unit);
        }

        private void Update()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

            float deltaTime = Time.deltaTime;
            
            // 1. 중앙 집중형 수동 업데이트 호출 (역순 순회로 제거 대응은 되어있으나, 로직 분리를 위해 유지)
            for (int i = allUnits.Count - 1; i >= 0; i--)
            {
                var unit = allUnits[i];
                if (unit == null || unit.IsDead) continue;
                
                unit.ManualUpdate(deltaTime);
                
                // [OPTIMIZATION] 격자 갱신 최적화: 유닛이 마지막 갱신 위치에서 0.5m 이상 이동했을 때만 갱신
                // 정지 상태거나 이동 거리가 짧으면 갱신 연산을 생략하여 CPU 점유율 절감 (Master's Directive)
                float sqrDist = (unit.transform.position - unit.LastGridUpdatePos).sqrMagnitude;
                if (sqrDist >= 0.25f) // 0.5f * 0.5f = 0.25f
                {
                    UpdateGridPosition(unit, false);
                }
            }

            // 2. [STABILITY] 루프가 끝난 뒤 대기열 처리 (Collection Modified Error 방지)
            if (pendingUnregister.Count > 0)
            {
                for (int i = 0; i < pendingUnregister.Count; i++)
                {
                    var unit = pendingUnregister[i];
                    allUnits.Remove(unit);
                    RemoveFromGrid(unit);
                }
                pendingUnregister.Clear();
            }

            if (pendingRegister.Count > 0)
            {
                for (int i = 0; i < pendingRegister.Count; i++)
                {
                    var unit = pendingRegister[i];
                    if (!allUnits.Contains(unit))
                    {
                        allUnits.Add(unit);
                        UpdateGridPosition(unit, true);
                    }
                }
                pendingRegister.Clear();
            }
        }

        private void FixedUpdate()
        {
            if (GameManager.Instance != null && GameManager.Instance.IsGameOver) return;

            float fixedDeltaTime = Time.fixedDeltaTime;
            for (int i = allUnits.Count - 1; i >= 0; i--)
            {
                var unit = allUnits[i];
                if (unit == null || unit.IsDead) continue;

                unit.ManualFixedUpdate(fixedDeltaTime);
            }
        }

        /// <summary>
        /// [CLEANUP] 현재 등록된 모든 유닛 정보를 초기화합니다.
        /// 씬 전환 시 강제 호출됩니다.
        /// </summary>
        public void ClearAll()
        {
            allUnits.Clear();
            unitGrid.Clear();
            pendingRegister.Clear();
            pendingUnregister.Clear();
            
            Debug.Log("<color=orange>[UnitManager]</color> All registered unit data cleared.");
        }


        #region Spatial Partitioning (Grid)

        private Vector2Int WorldToGrid(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / gridCellSize),
                Mathf.FloorToInt(worldPos.y / gridCellSize)
            );
        }

        private void UpdateGridPosition(UnitBase unit, bool forceUpdate)
        {
            Vector3 currentPos = unit.transform.position;
            Vector2Int newGridPos = WorldToGrid(currentPos);
            
            if (!forceUpdate && unit.CurrentGridPos == newGridPos) 
            {
                // 격자 칸이 바뀌지 않았더라도, 마지막 갱신 위치는 현재 위치로 동기화하여 거리 체크 기준점 유지
                unit.LastGridUpdatePos = currentPos;
                return;
            }

            // 이전 위치 제거
            RemoveFromGrid(unit);

            // 새 위치 등록
            unit.CurrentGridPos = newGridPos;
            unit.LastGridUpdatePos = currentPos; // [OPTIMIZATION] 갱신 시점의 좌표 기록
            if (!unitGrid.TryGetValue(newGridPos, out var list))
            {
                list = new List<UnitBase>(16);
                unitGrid[newGridPos] = list;
            }
            list.Add(unit);
        }

        private void RemoveFromGrid(UnitBase unit)
        {
            if (unitGrid.TryGetValue(unit.CurrentGridPos, out var list))
            {
                list.Remove(unit);
            }
        }

        /// <summary>
        /// [O(Local)] 주변 인접 격자의 유닛들만 반환합니다. (새 리스트를 할당하므로 주의)
        /// </summary>
        public List<UnitBase> GetNearbyUnits(Vector3 position, float radius)
        {
            List<UnitBase> nearby = new List<UnitBase>(32);
            GetNearbyUnitsNonAlloc(position, radius, nearby);
            return nearby;
        }

        /// <summary>
        /// [O(Local)] 주변 인접 격자의 유닛들을 외부에서 제공받은 리스트에 채웁니다. (GC Free)
        /// </summary>
        public void GetNearbyUnitsNonAlloc(Vector3 position, float radius, List<UnitBase> results)
        {
            results.Clear();
            Vector2Int centerGrid = WorldToGrid(position);
            int range = Mathf.CeilToInt(radius / gridCellSize);
            float sqrRadius = radius * radius;

            for (int x = -range; x <= range; x++)
            {
                for (int y = -range; y <= range; y++)
                {
                    Vector2Int gridPos = centerGrid + new Vector2Int(x, y);
                    if (unitGrid.TryGetValue(gridPos, out var list))
                    {
                        for (int i = 0; i < list.Count; i++)
                        {
                            var unit = list[i];
                            if (unit == null || unit.IsDead) continue;
                            
                            if ((unit.transform.position - position).sqrMagnitude <= sqrRadius)
                            {
                                results.Add(unit);
                            }
                        }
                    }
                }
            }
        }

        #endregion

        #region Minion Spawn & Revive

        /// <summary>
        /// [SRP] 해금된 미니언 데이터 풀을 최신화합니다. 세션 시작 시 GameManager에서 호출됩니다.
        /// </summary>
        public void UpdateUnlockedMinionPool()
        {
            Debug.Log("<color=yellow>[UnitManager]</color> Updating Dynamic Minion Pool...");
            if (unlockedMinionDatas == null) unlockedMinionDatas = new List<Necromancer.Data.MinionUnlockSO>();
            unlockedMinionDatas.Clear();

            var gm = GameManager.Instance;
            if (gm == null || gm.minionUnlockDataList == null) return;

            // 1. 기본 미니언(전사) 데이터 찾아서 추가 (ID: SkeletonWarrior)
            var warriorData = gm.minionUnlockDataList.Find(x => x.minionID == "SkeletonWarrior");
            if (warriorData != null) unlockedMinionDatas.Add(warriorData);

            if (gm.Resources == null || gm.SaveData == null || gm.SaveData.Data == null) return;

            // 2. 해금된 다른 미니언 데이터 추가
            foreach (var data in gm.minionUnlockDataList)
            {
                if (data == null || data.minionID == "SkeletonWarrior") continue;
                if (gm.Resources.IsMinionUnlocked(data.minionID))
                    unlockedMinionDatas.Add(data);
            }

            Debug.Log($"<color=green>[UnitManager]</color> Dynamic Pool Updated. Unlocked Types: {unlockedMinionDatas.Count}");
        }

        /// <summary>
        /// [SRP] 사망한 적을 확률에 따라 미니언으로 부활시킵니다.
        /// </summary>
        public void TryReviveAsMinion(Vector3 pos)
        {
            var gm = GameManager.Instance;
            float bonus = (gm != null && gm.Resources != null) ? gm.Resources.GetUpgradeValue(UpgradeStatType.Resurrection) : 0f;
            if (UnityEngine.Random.Range(0f, 100f) <= Mathf.Min(baseReviveChance + bonus, 90f))
            {
                if (gm != null && gm.poolManager != null)
                {
                    GameObject minionObj = gm.poolManager.Get(minionPoolTag, pos, Quaternion.identity);
                    if (minionObj != null && minionObj.TryGetComponent<MinionAI>(out var ai))
                    {
                        Necromancer.Data.MinionUnlockSO selectedData = null;
                        if (unlockedMinionDatas.Count > 0)
                            selectedData = unlockedMinionDatas[UnityEngine.Random.Range(0, unlockedMinionDatas.Count)];

                        ai.Initialize(selectedData);

                        if (gm.Sound != null) gm.Sound.PlaySFX(gm.Sound.sfxCreateMinion);
                        Debug.Log($"<color=green>[UnitManager]</color> Revived Minion: {(selectedData != null ? selectedData.minionName : "Basic")}");
                    }
                }
            }
        }

        /// <summary>
        /// [SRP] 현재 스테이지 ID에 따라 드랍할 미니언 해금 데이터를 반환합니다.
        /// 1-10: Minion_02, 11-20: Minion_03, 21-30: Minion_04, 31-40: Minion_05, 41+: Minion_06
        /// </summary>
        public Necromancer.Data.MinionUnlockSO GetMinionDataForCurrentStage()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.currentStage == null || gm.minionUnlockDataList == null || gm.minionUnlockDataList.Count == 0) return null;

            int stageID = gm.currentStage.stageID;
            int minionIndex = 0;

            if (stageID >= 1 && stageID <= 10) minionIndex = 1;
            else if (stageID >= 11 && stageID <= 20) minionIndex = 2;
            else if (stageID >= 21 && stageID <= 30) minionIndex = 3;
            else if (stageID >= 31 && stageID <= 40) minionIndex = 4;
            else if (stageID >= 41) minionIndex = 5;

            if (minionIndex >= 0 && minionIndex < gm.minionUnlockDataList.Count)
            {
                var data = gm.minionUnlockDataList[minionIndex];
                // 이미 해금된 미니언이라면 중복 드랍 방지
                if (data != null && gm.Resources != null && gm.Resources.IsMinionUnlocked(data.minionID))
                    return null;
                return data;
            }

            return null;
        }

        /// <summary>
        /// [SRP] 게임 시작 시 로비 업그레이드에 따른 초기 미니언을 소환합니다.
        /// </summary>
        public void SpawnInitialMinions()
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Resources == null || gm.poolManager == null || gm.playerTransform == null) return;

            int count = Mathf.FloorToInt(gm.Resources.GetUpgradeValue(UpgradeStatType.StartMinionCount));
            if (count <= 0) return;

            Debug.Log($"[UnitManager] Spawning {count} initial minions from lobby upgrade.");

            for (int i = 0; i < count; i++)
            {
                Vector3 spawnPos = gm.playerTransform.position + (Vector3)UnityEngine.Random.insideUnitCircle * 2f;
                GameObject minionObj = gm.poolManager.Get(minionPoolTag, spawnPos, Quaternion.identity);
                if (minionObj != null && minionObj.TryGetComponent<MinionAI>(out var ai))
                {
                    Necromancer.Data.MinionUnlockSO selectedData = null;
                    if (unlockedMinionDatas.Count > 0)
                        selectedData = unlockedMinionDatas[0]; // 보통 첫 번째가 워리어
                    ai.Initialize(selectedData);
                }
            }
        }

        #endregion
    }
}
