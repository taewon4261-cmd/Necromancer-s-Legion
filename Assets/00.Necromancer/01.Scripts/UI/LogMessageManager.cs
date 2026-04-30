using System.Collections.Generic;
using UnityEngine;

namespace Necromancer.UI
{
    public class LogMessageManager : MonoBehaviour
    {
        [SerializeField] private GameObject _slotPrefab;
        [SerializeField] private int _initialPoolSize = 10;

        private Transform _contentsParent;
        private readonly Queue<LogMessageSlot> _pool = new Queue<LogMessageSlot>();

        /// <summary>
        /// UIManager.Init()에서 InGameHUD의 logContents를 전달받아 초기화합니다.
        /// </summary>
        public void Init(Transform contentsParent)
        {
            _contentsParent = contentsParent;
            _pool.Clear();

            for (int i = 0; i < _initialPoolSize; i++)
                SpawnSlot();
        }

        public void AddLog(string message)
        {
            if (_contentsParent == null)
            {
                Debug.LogWarning("[LogMessageManager] contentsParent is null. Call Init() first.");
                return;
            }

            LogMessageSlot slot = _pool.Count > 0 ? _pool.Dequeue() : SpawnSlot();

            if (slot == null)
            {
                Debug.LogError("[LogMessageManager] slot is null! Check: 1) _slotPrefab이 Inspector에 연결됐는지, 2) 프리팹에 LogMessageSlot 컴포넌트가 붙어있는지 확인하세요.");
                return;
            }

            slot.transform.SetAsLastSibling();
            slot.gameObject.SetActive(true);
            slot.Setup(message, ReturnToPool);
        }

        private LogMessageSlot SpawnSlot()
        {
            if (_slotPrefab == null)
            {
                Debug.LogError("[LogMessageManager] _slotPrefab이 null입니다. GameManager Inspector에서 LogMessageManager의 SlotPrefab을 연결하세요.");
                return null;
            }

            var obj = Instantiate(_slotPrefab, _contentsParent);
            var slot = obj.GetComponent<LogMessageSlot>();

            if (slot == null)
            {
                Debug.LogError($"[LogMessageManager] 프리팹 '{_slotPrefab.name}'에 LogMessageSlot 컴포넌트가 없습니다. 프리팹에 LogMessageSlot.cs를 추가하세요.");
                Destroy(obj);
                return null;
            }

            obj.SetActive(false);
            _pool.Enqueue(slot);
            return slot;
        }

        private void ReturnToPool(LogMessageSlot slot)
        {
            _pool.Enqueue(slot);
        }
    }
}
