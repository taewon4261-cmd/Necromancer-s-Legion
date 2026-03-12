using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Necromancer;

namespace Necromancer.UI
{
    /// <summary>
    /// 스테이지 리스트 스크롤 뷰 내의 개별 스테이지 버튼 항목입니다.
    /// </summary>
    public class StageItemUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TextMeshProUGUI stageNumberText;
        public TextMeshProUGUI stageNameText;
        public Button itemButton;

        private StageDataSO stageData;
        private StageSelectUI parentUI;

        public void Setup(StageDataSO data, StageSelectUI ui)
        {
            stageData = data;
            parentUI = ui;

            if (stageNumberText != null) stageNumberText.text = data.stageID.ToString("D2");
            if (stageNameText != null) stageNameText.text = data.stageName;

            if (itemButton != null)
            {
                itemButton.onClick.RemoveAllListeners();
                itemButton.onClick.AddListener(OnItemClicked);
            }
        }

        private void OnItemClicked()
        {
            if (parentUI != null)
            {
                parentUI.SelectStage(stageData);
            }
        }
    }
}
