using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using Cysharp.Threading.Tasks;

namespace Necromancer.Systems
{
    /// <summary>
    /// Addressables 원격 번들 다운로드를 담당합니다.
    /// GameManager를 통해 접근합니다 (GameManager.Instance.Download).
    /// </summary>
    public class DownloadManager : MonoBehaviour
    {
        // 다운로드할 Addressables 레이블 (Addressables Groups 창에서 에셋에 붙인 레이블과 동일하게 설정)
        [SerializeField] private string _remoteLabel = "remote";

        public static event Action<float> OnDownloadProgress;  // 0 ~ 1
        public static event Action        OnDownloadComplete;
        public static event Action<string> OnDownloadError;

        public void Init()
        {
            Debug.Log("<color=cyan>[DownloadManager]</color> Initialized.");
        }

        /// <summary>
        /// 원격 카탈로그 업데이트를 확인하고 적용합니다.
        /// 네트워크 오류 시 false를 반환합니다.
        /// </summary>
        public async UniTask<bool> UpdateCatalogsAsync()
        {
            // [BUILD FIX] 빌드 기기에서 암시적 초기화가 지연될 수 있으므로 명시적으로 보장
            var initHandle = Addressables.InitializeAsync();
            while (!initHandle.IsDone)
                await UniTask.Yield();

            if (initHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[DownloadManager] Addressables.InitializeAsync failed.");
                Addressables.Release(initHandle);
                return false;
            }
            Addressables.Release(initHandle);

            var checkHandle = Addressables.CheckForCatalogUpdates(false);
            while (!checkHandle.IsDone)
                await UniTask.Yield();

            if (checkHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogWarning("[DownloadManager] CheckForCatalogUpdates failed. Offline mode?");
                Addressables.Release(checkHandle);
                return false;
            }

            List<string> toUpdate = checkHandle.Result;
            Addressables.Release(checkHandle);

            if (toUpdate == null || toUpdate.Count == 0)
            {
                Debug.Log("[DownloadManager] No catalog updates found.");
                return true;
            }

            Debug.Log($"[DownloadManager] Updating {toUpdate.Count} catalog(s)...");
            var updateHandle = Addressables.UpdateCatalogs(toUpdate, false);
            while (!updateHandle.IsDone)
                await UniTask.Yield();

            bool success = updateHandle.Status == AsyncOperationStatus.Succeeded;
            Addressables.Release(updateHandle);

            if (!success)
                Debug.LogError("[DownloadManager] UpdateCatalogs failed.");

            return success;
        }

        /// <summary>
        /// remoteLabel에 해당하는 에셋의 미다운로드 용량(bytes)을 반환합니다.
        /// 0이면 이미 최신, -1이면 오류입니다.
        /// </summary>
        public async UniTask<long> GetDownloadSizeAsync()
        {
            var sizeHandle = Addressables.GetDownloadSizeAsync(_remoteLabel);
            while (!sizeHandle.IsDone)
                await UniTask.Yield();

            if (sizeHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError("[DownloadManager] GetDownloadSizeAsync failed.");
                Addressables.Release(sizeHandle);
                return -1L;
            }

            long bytes = sizeHandle.Result;
            Addressables.Release(sizeHandle);
            Debug.Log($"[DownloadManager] Download size: {bytes / 1024f / 1024f:F2} MB");
            return bytes;
        }

        /// <summary>
        /// 다운로드를 실행합니다. onProgress 콜백으로 0~1 진행률을 전달합니다.
        /// </summary>
        public async UniTask<bool> DownloadAsync(Action<float> onProgress = null)
        {
            var handle = Addressables.DownloadDependenciesAsync(_remoteLabel, false);

            while (!handle.IsDone)
            {
                float pct = handle.PercentComplete;
                onProgress?.Invoke(pct);
                OnDownloadProgress?.Invoke(pct);
                await UniTask.Yield();
            }

            bool success = handle.Status == AsyncOperationStatus.Succeeded;

            if (success)
            {
                onProgress?.Invoke(1f);
                OnDownloadProgress?.Invoke(1f);
                OnDownloadComplete?.Invoke();
                Debug.Log("<color=green>[DownloadManager]</color> Download complete!");
            }
            else
            {
                string err = handle.OperationException?.Message ?? "Unknown error";
                OnDownloadError?.Invoke(err);
                Debug.LogError($"[DownloadManager] Download failed: {err}");
            }

            Addressables.Release(handle);
            return success;
        }

        public string RemoteLabel => _remoteLabel;
    }
}
