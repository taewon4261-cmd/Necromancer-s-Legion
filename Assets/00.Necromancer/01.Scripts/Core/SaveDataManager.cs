using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Firebase.Firestore;
using Firebase.Extensions;

namespace Necromancer.Core
{
    [Serializable]
    public class UpgradeSaveData
    {
        public string key;
        public int level;
    }

    [Serializable]
    public class EssenceEntry
    {
        public string enemyID;
        public int count;
    }


    /// <summary>
    /// 게임 내 모든 영속성 데이터를 JSON 형식으로 관리하는 매니저입니다.
    /// </summary>
    [Serializable]
    public class GameSaveData : ISerializationCallbackReceiver
    {
        [Header("Audio Settings")]
        public float masterVolume = 1f;
        public float bgmVolume = 0.6f;
        public float sfxVolume = 0.8f;

        [Header("Resources")]
        public int currentSoul = 0;
        public int unlockedStageLevel = 1;
        public List<string> unlockedMinionIDs = new List<string>() { "Minion_01" };
        public string lastLoginMethod = "None"; // [AUTH] "None", "Guest", "Google"
        public bool hasSeenTutorial = false;   // [TUTORIAL] 최초 실행 가이드 노출 여부

        [Header("Stamina (피로도)")]
        public int currentStamina = 10;
        public long lastStaminaUpdateTimeTicks = 0;
        public int staminaAdsWatchedToday = 0;
        public string lastStaminaAdDate = "";
        
        // [SAVE] 정수 획득 현황 딕셔너리 직렬화를 위한 리스트
        [SerializeField] private List<EssenceEntry> essenceList = new List<EssenceEntry>();
        public Dictionary<string, int> minionEssences = new Dictionary<string, int>();


        [Header("Upgrades")]
        [SerializeField] private List<UpgradeSaveData> upgradeLevels = new List<UpgradeSaveData>();

        // [OPTIMIZATION] 런타임 빠른 조회를 위한 딕셔너리 (JsonUtility는 직렬화 불가하므로 콜백으로 관리)
        public Dictionary<string, int> upgradeDict = new Dictionary<string, int>();

        public void OnBeforeSerialize()
        {
            // Dictionary -> List 동기화 (저장 전)
            upgradeLevels.Clear();
            foreach (var kvp in upgradeDict)
            {
                upgradeLevels.Add(new UpgradeSaveData { key = kvp.Key, level = kvp.Value });
            }

            // [SAVE] Essence Dictionary -> List 동기화
            essenceList.Clear();
            foreach (var kvp in minionEssences)
            {
                essenceList.Add(new EssenceEntry { enemyID = kvp.Key, count = kvp.Value });
            }
        }

        public void OnAfterDeserialize()
        {
            // List -> Dictionary 동기화 (로드 후)
            upgradeDict.Clear();
            foreach (var item in upgradeLevels)
            {
                upgradeDict[item.key] = item.level;
            }

            // [SAVE] Essence List -> Dictionary 동기화
            minionEssences.Clear();
            foreach (var entry in essenceList)
            {
                minionEssences[entry.enemyID] = entry.count;
            }

        }
    }

    public class SaveDataManager : MonoBehaviour
    {
        // AES 암호화 키/IV (32바이트 키 = AES-256)
        private const string AES_KEY = "NecroLegion!Key#2024$Secure@Pass";  // 32자
        private const string LEGACY_AES_IV = "NecroLegion!IV##";           // [MIGRATION] 구버전 고정 IV

        private string savePath;
        private bool isInitialized = false;

        // [CLOUD] 현재 로그인된 Firebase UID (로그인 시 AuthManager가 설정)
        private string _cloudUid = null;
        private bool _isLoadingFromCloud = false; // [FIX] 클라우드 로딩 중 저장 방지용

        public string CurrentUid => _cloudUid;

        private GameSaveData currentData;

        public GameSaveData Data => currentData;

        private void Awake()
        {
            // [STABILITY] 초기화되지 않은 상태에서 Save가 호출되는 것을 방지하기 위해 Awake에서 즉시 경로를 설정합니다.
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            }
        }

        public void Init()
        {
            if (isInitialized) return;

            // [STABILITY] 플랫폼 독립적인 저장 경로 강제 할당 (Master's Directive)
            savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            
            // 저장 폴더가 존재하지 않으면 생성
            string directory = System.IO.Path.GetDirectoryName(savePath);
            if (!System.IO.Directory.Exists(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            Load();
            isInitialized = true;
            Debug.Log($"<color=green>[SaveDataManager]</color> Initialized at: {savePath}");
        }

        /// <summary>
        /// 파일로부터 데이터를 읽어옵니다. 없으면 기본값을 생성합니다.
        /// </summary>
        public void Load()
        {
            if (File.Exists(savePath))
            {
                try
                {
                    string encrypted = File.ReadAllText(savePath);
                    string json = "";

                    try
                    {
                        // 1. 신규 포맷 (랜덤 IV) 시도
                        json = Decrypt(encrypted);
                    }
                    catch
                    {
                        // 2. 실패 시 구 포맷 (고정 IV) 마이그레이션 시도
                        Debug.Log("<color=orange>[SaveDataManager]</color> New format decrypt failed. Trying legacy migration...");
                        json = DecryptLegacy(encrypted);
                        
                        // 마이그레이션 성공 시 즉시 신규 포맷으로 갱신 저장
                        currentData = JsonUtility.FromJson<GameSaveData>(json);
                        Save(); 
                        Debug.Log("<color=green>[SaveDataManager]</color> Migration from legacy format successful.");
                        return;
                    }

                    currentData = JsonUtility.FromJson<GameSaveData>(json);
                    Debug.Log($"<color=green>[SaveDataManager]</color> Data loaded from {savePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[SaveDataManager] Load failed: {e.Message}");
                    currentData = new GameSaveData();
                }
            }
            else
            {
                currentData = new GameSaveData();
                // [FIX] 초기화 시 즉시 저장하지 않음 (클라우드 로드 전 덮어쓰기 방지)
                // Save(); 
            }
        }

        /// <summary>
        /// 현재 메모리의 데이터를 파일에 저장합니다.
        /// 로그인 상태라면 Firestore에도 비동기로 업로드합니다.
        /// </summary>
        public void Save()
        {
            if (currentData == null) currentData = new GameSaveData();
            if (_isLoadingFromCloud)
            {
                Debug.LogWarning("[SaveDataManager] Save skipped: currently loading from cloud.");
                return;
            }

            // [STABILITY] 저장 전 경로 재검증 (NullReference 방어)
            if (string.IsNullOrEmpty(savePath))
            {
                savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            }

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                string encrypted = Encrypt(json);
                System.IO.File.WriteAllText(savePath, encrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] Save failed: {e.Message}");
            }

            // [CLOUD] 로그인 상태일 때 클라우드에 fire-and-forget 업로드
            if (!string.IsNullOrEmpty(_cloudUid))
            {
                _ = SaveToCloud(_cloudUid);
            }
        }

        // 파일에만 쓰고 클라우드 업로드는 건너뜀 (LoadFromCloud 후 이중 업로드 방지용)
        private void SaveLocalOnly()
        {
            if (currentData == null) currentData = new GameSaveData();
            if (string.IsNullOrEmpty(savePath))
                savePath = System.IO.Path.Combine(Application.persistentDataPath, "NecromancerSave.json");
            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                string encrypted = Encrypt(json);
                System.IO.File.WriteAllText(savePath, encrypted);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] SaveLocalOnly failed: {e.Message}");
            }
        }

        /// <summary>
        /// [CLOUD] 로그인된 UID를 설정합니다. AuthManager가 로그인 성공 후 호출합니다.
        /// </summary>
        public void SetCloudUser(string uid)
        {
            _cloudUid = uid;
            Debug.Log($"<color=cyan>[SaveDataManager]</color> Cloud user set: {uid}");
        }

        /// <summary>
        /// [CLOUD] 현재 데이터를 Firestore에 업로드합니다.
        /// </summary>
        public async Task SaveToCloud(string uid = null)
        {
            string targetUid = string.IsNullOrEmpty(uid) ? _cloudUid : uid;
            if (string.IsNullOrEmpty(targetUid))
            {
                Debug.LogWarning("[SaveDataManager] SaveToCloud skipped: no user UID.");
                return;
            }

            try
            {
                string json = JsonUtility.ToJson(currentData, true);
                string encrypted = Encrypt(json);

                var db = FirebaseFirestore.DefaultInstance;
                var data = new Dictionary<string, object>
                {
                    { "gameData", encrypted },
                    { "lastUpdate", FieldValue.ServerTimestamp }
                };

                await db.Collection("users").Document(targetUid).SetAsync(data, SetOptions.MergeAll);
                Debug.Log("<color=green>[SaveDataManager]</color> Cloud save successful.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] SaveToCloud failed: {e.Message}");
            }
        }

        /// <summary>
        /// [CLOUD] Firestore에서 데이터를 불러와 로컬에 덮어씁니다.
        /// 문서가 없으면 false, 성공하면 true를 반환합니다.
        /// </summary>
        public async Task<bool> LoadFromCloud(string uid)
        {
            if (string.IsNullOrEmpty(uid))
            {
                Debug.LogWarning("[SaveDataManager] LoadFromCloud skipped: uid is null/empty.");
                return false;
            }

            _isLoadingFromCloud = true;
            try
            {
                var db = FirebaseFirestore.DefaultInstance;
                var snapshot = await db.Collection("users").Document(uid).GetSnapshotAsync();

                if (!snapshot.Exists)
                {
                    Debug.Log("[SaveDataManager] No cloud save found. Using local data.");
                    return false;
                }

                if (!snapshot.TryGetValue("gameData", out string encrypted))
                {
                    Debug.LogWarning("[SaveDataManager] Cloud document has no 'gameData' field.");
                    return false;
                }

                string json = Decrypt(encrypted);
                var loaded = JsonUtility.FromJson<GameSaveData>(json);

                // 클라우드 데이터가 기본값이면 로컬 데이터 유지 (빈 클라우드 덮어쓰기 방지)
                bool isMeaningful = loaded.currentSoul > 0
                    || loaded.unlockedStageLevel > 1
                    || (loaded.upgradeDict != null && loaded.upgradeDict.Count > 0);

                if (!isMeaningful)
                {
                    Debug.Log("[SaveDataManager] Cloud data is empty/default. Keeping local data.");
                    return false;
                }

                currentData = loaded;
                SaveLocalOnly(); // 로컬 파일만 동기화 (클라우드 이중 업로드 방지)
                Debug.Log("<color=green>[SaveDataManager]</color> Cloud data loaded and synced to local.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveDataManager] LoadFromCloud failed: {e.Message}");
                throw; // 에러 발생 시 예외를 던져서 상위에서 '데이터 없음'과 '통신 에러'를 구분할 수 있게 함
            }
            finally
            {
                _isLoadingFromCloud = false;
            }
        }

        private void OnApplicationQuit()
        {
            Save();
            Debug.Log("<color=orange>[SaveDataManager]</color> Final save on ApplicationQuit executed.");
        }

        public void OnDisable()
        {
            // [ATOMIC] 에디터 중단이나 비활성화 시점에 데이터를 즉각 보호
            Save();
            Debug.Log("<color=orange>[SaveDataManager]</color> Final save on OnDisable executed.");
        }

        /// <summary>
        /// [DEBUG] 모든 저장 데이터와 로그인 기록을 초기화합니다. (인스펙터 우클릭 메뉴)
        /// </summary>
        [ContextMenu("Clear All Save Data")]
        public void ClearAllData()
        {
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
                Debug.Log("<color=red>[SaveDataManager]</color> 저장 파일이 삭제되었습니다: " + savePath);
            }
            
            currentData = new GameSaveData();
            Save();
            
            Debug.Log("<color=cyan>[SaveDataManager]</color> 모든 데이터가 초기화되었습니다. 재시작 시 로그인 패널이 다시 표시됩니다.");
        }

        /// <summary>
        /// 특정 액션을 수행한 뒤 자동 저장합니다.
        /// </summary>
        public void UpdateAndSave(Action<GameSaveData> updateAction)
        {
            updateAction?.Invoke(currentData);
            Save();
        }

        #region Encryption
        private string Encrypt(string plainText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(AES_KEY);
                aes.GenerateIV(); // [SECURITY] 매번 새로운 IV 생성
                
                using (var ms = new MemoryStream())
                {
                    // IV를 암호문 앞 16바이트에 포함
                    ms.Write(aes.IV, 0, 16);

                    using (var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        byte[] data = Encoding.UTF8.GetBytes(plainText);
                        cs.Write(data, 0, data.Length);
                        cs.FlushFinalBlock();
                    }
                    return Convert.ToBase64String(ms.ToArray());
                }
            }
        }

        private string Decrypt(string cipherText)
        {
            byte[] fullCipher = Convert.FromBase64String(cipherText);
            if (fullCipher.Length < 16) throw new Exception("Invalid cipher text");

            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(AES_KEY);
                
                // 앞 16바이트에서 IV 추출
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, 16);
                aes.IV = iv;

                // 나머지 부분 복호화
                using (var ms = new MemoryStream(fullCipher, 16, fullCipher.Length - 16))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }

        private string DecryptLegacy(string cipherText)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = Encoding.UTF8.GetBytes(AES_KEY);
                aes.IV  = Encoding.UTF8.GetBytes(LEGACY_AES_IV);
                using (var ms = new MemoryStream(Convert.FromBase64String(cipherText)))
                using (var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Read))
                using (var sr = new StreamReader(cs))
                {
                    return sr.ReadToEnd();
                }
            }
        }
        #endregion

        #region Upgrade Helpers
        public int GetUpgradeLevel(string key)
        {
            if (string.IsNullOrEmpty(key)) return 0;
            return currentData.upgradeDict.TryGetValue(key, out int lv) ? lv : 0;
        }

        public void SetUpgradeLevel(string key, int level)
        {
            if (string.IsNullOrEmpty(key)) return;
            currentData.upgradeDict[key] = level;
            Save();
        }
        #endregion
    }
}
