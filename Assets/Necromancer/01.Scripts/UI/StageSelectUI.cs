using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using Necromancer;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// 스테이지 선택 패널의 UI와 선택 로직을 담당합니다.
    /// </summary>
    public class StageSelectUI : MonoBehaviour
    {
        [Header("Stage Info Display")]
        public TextMeshProUGUI stageNameText;
        public TextMeshProUGUI stageDescText;
        public Image stageThumbnail;
        public Button startButton;

        [Header("Navigations")]
        public Button prevButton;
        public Button nextButton;
        public GameObject lockOverlay; // 잠긴 스테이지 가림용
        public TextMeshProUGUI lockMessage;

        [Header("List System (Hidden)")]
        public Transform listContainer;    
        public GameObject itemPrefab;      
        public List<StageDataSO> stageList = new List<StageDataSO>(); 

        [Header("Current Selection")]
        public StageDataSO selectedStage;
        private int currentIndex = 0;

        private void Start()
        {
            if (startButton != null) startButton.onClick.AddListener(OnStartButtonClicked);
            if (prevButton != null) prevButton.onClick.AddListener(MovePrev);
            if (nextButton != null) nextButton.onClick.AddListener(MoveNext);
            
            InitStageList();

            if (stageList != null && stageList.Count > 0)
            {
                currentIndex = 0;
                SelectStage(stageList[currentIndex]);
            }
        }

        public void MoveNext()
        {
            if (currentIndex < stageList.Count - 1)
            {
                currentIndex++;
                SelectStage(stageList[currentIndex]);
            }
        }

        public void MovePrev()
        {
            if (currentIndex > 0)
            {
                currentIndex--;
                SelectStage(stageList[currentIndex]);
            }
        }

        private void InitStageList()
        {
            // 리스트 데이터를 가져옵니다. (스크롤 뷰가 없더라도 데이터를 로드해야 하므로 유지)
            if (stageList == null || stageList.Count == 0)
            {
                var loadedStages = Resources.LoadAll<StageDataSO>("Stages");
                if (loadedStages != null && loadedStages.Length > 0)
                {
                    stageList = new List<StageDataSO>(loadedStages);
                    stageList.Sort((a, b) => a.stageID.CompareTo(b.stageID));
                }
            }
        }

        public void SelectStage(StageDataSO stage)
        {
            selectedStage = stage;
            UpdateStageDisplay(stage);
            UpdateNavButtons();
        }

        private void UpdateNavButtons()
        {
            if (prevButton != null) prevButton.interactable = currentIndex > 0;
            if (nextButton != null) nextButton.interactable = currentIndex < stageList.Count - 1;
        }

        private void UpdateStageDisplay(StageDataSO stage)
        {
            if (stage == null) return;

            // 1. 잠금 여부 확인
            bool isUnlocked = GameManager.Instance.Resources.IsStageUnlocked(stage.stageID);

            // 2. 기본 정보 표시
            if (stageNameText != null) stageNameText.text = isUnlocked ? stage.stageName : "???";
            if (stageDescText != null) stageDescText.text = isUnlocked ? stage.stageDescription : "이전 스테이지를 클리어해야 합니다.";
            if (stageThumbnail != null) 
            {
                stageThumbnail.sprite = stage.stageThumbnail;
                stageThumbnail.color = isUnlocked ? Color.white : Color.black; // 잠기면 검게
            }
            
            // 3. 잠금 UI 처리
            if (lockOverlay != null) lockOverlay.SetActive(!isUnlocked);
            if (startButton != null) 
            {
                startButton.interactable = isUnlocked;
                // 버튼 텍스트나 알파값 등으로 시각적 피드백 추가 가능
            }
        }

        private void OnStartButtonClicked()
        {
            if (selectedStage == null) return;

            Debug.Log($"<color=cyan>[StageSelectUI]</color> Starting Game with Stage: {selectedStage.stageName}");
            
            // GameManager를 통해 게임 시작 및 난이도 설정
            GameManager.Instance.Combat.SetupStageModifiers(selectedStage);
            GameManager.Instance.StartGame(selectedStage);
        }
    }
}
