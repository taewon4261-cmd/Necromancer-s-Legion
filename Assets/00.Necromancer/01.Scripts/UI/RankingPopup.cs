using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections.Generic;
using Firebase.Firestore;
using Firebase.Extensions;
using Necromancer.Core;

namespace Necromancer.UI
{
    /// <summary>
    /// Firestore에서 랭킹 데이터를 실시간으로 가져와 순위표를 렌더링하는 팝업 컨트롤러입니다.
    /// </summary>
    public class RankingPopup : MonoBehaviour
    {
        [System.Serializable]
        public class RankingSlot
        {
            public GameObject root;
            public TextMeshProUGUI tmpRank;
            public TextMeshProUGUI tmpNickname;
            public TextMeshProUGUI tmpStage;
        }

        [Header("UI References - List")]
        [SerializeField] private RectTransform contentParent;
        [SerializeField] private GameObject rankingItemPrefab;
        [SerializeField] private GameObject loadingOverlay;

        [Header("UI References - My Rank")]
        [SerializeField] private TextMeshProUGUI tmpMyRankText;

        [Header("UI References - Actions")]
        [SerializeField] private Button btnClose;

        private readonly List<GameObject> activeItems = new List<GameObject>();

        private bool isInitialized = false;

        private void InitializeIfNeeded()
        {
            if (isInitialized) return;
            isInitialized = true;

            // 1. [UI CLEANUP] 필요 없는 설정 UI 요소 숨기기
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                string childName = child.name;
                if (childName != "Setting Image" && !childName.Contains("Close") && !childName.Contains("Back"))
                {
                    child.gameObject.SetActive(false);
                }
            }

            // 2. 타이틀 텍스트를 "랭 킹"으로 수정
            var titleText = transform.Find("Setting Image/Text_Title_Setting")?.GetComponent<TextMeshProUGUI>();
            if (titleText != null)
            {
                titleText.text = "랭 킹";
            }

            // 3. [DYNAMIC SCROLLVIEW] 랭킹 리스트 출력을 위한 스크롤뷰 동적 생성
            if (contentParent == null)
            {
                var scrollGo = DefaultControls.CreateScrollView(new DefaultControls.Resources());
                scrollGo.name = "ScrollView_Ranking";
                scrollGo.transform.SetParent(this.transform, false);

                var rect = scrollGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(600, 600); // 팝업 크기에 맞춰 600 크기로 설정
                rect.anchoredPosition = new Vector2(0, 50);

                var scrollRect = scrollGo.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    scrollRect.horizontal = false;
                    if (scrollRect.horizontalScrollbar != null)
                        scrollRect.horizontalScrollbar.gameObject.SetActive(false);
                    contentParent = scrollRect.content;
                    
                    // 세로 자동 레이아웃 정렬 컴포넌트 장착
                    var vlg = contentParent.gameObject.AddComponent<VerticalLayoutGroup>();
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childControlHeight = true;
                    vlg.childControlWidth = true;
                    vlg.childForceExpandHeight = false;
                    vlg.childForceExpandWidth = true;
                    vlg.spacing = 10;

                    var csf = contentParent.gameObject.AddComponent<ContentSizeFitter>();
                    csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
            }

            // 4. [AUTO BINDING] 닫기 버튼 연동
            if (btnClose == null)
            {
                var buttons = GetComponentsInChildren<Button>(true);
                // 1순위: 정확한 고유 이름 매칭
                foreach (var btn in buttons)
                {
                    if (btn.name == "Btn_Close")
                    {
                        btnClose = btn;
                        btn.gameObject.SetActive(true);
                        break;
                    }
                }
                
                // 2순위: 단어 포함 Fallback
                if (btnClose == null)
                {
                    foreach (var btn in buttons)
                    {
                        string btnNameLower = btn.name.ToLower();
                        if ((btnNameLower.Contains("close") || btnNameLower.Contains("back")) && 
                            !btn.name.Contains("BackToMain") && !btn.name.Contains("MainMenu"))
                        {
                            btnClose = btn;
                            btn.gameObject.SetActive(true);
                            break;
                        }
                    }
                }
            }

            // 5. [DYNAMIC TEXT] 내 랭킹 정보 텍스트 생성
            if (tmpMyRankText == null)
            {
                var txtGo = new GameObject("Text_MyRankInfo", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                txtGo.transform.SetParent(this.transform, false);

                var rect = txtGo.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(600, 80);
                rect.anchoredPosition = new Vector2(0, -310); // 스크롤뷰 하단 영역

                tmpMyRankText = txtGo.GetComponent<TextMeshProUGUI>();
                tmpMyRankText.fontSize = 26;
                tmpMyRankText.alignment = TextAlignmentOptions.Center;
                tmpMyRankText.color = Color.white;
                if (titleText != null) tmpMyRankText.font = titleText.font;
            }

            // 6. [DYNAMIC ITEM PREFAB FALLBACK] 만약 rankingItemPrefab이 없으면 코드로 기본 텍스트 슬롯 형태를 만듦
            if (rankingItemPrefab == null)
            {
                var fallbackPrefab = new GameObject("RankingItemFallback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rect = fallbackPrefab.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(580, 80);
                
                var img = fallbackPrefab.GetComponent<Image>();
                img.color = new Color(0.15f, 0.15f, 0.2f, 0.9f); // 어두운 랭킹 슬롯 배경

                // Rank
                var rGo = new GameObject("Text_Rank", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                rGo.transform.SetParent(fallbackPrefab.transform, false);
                var rRect = rGo.GetComponent<RectTransform>();
                rRect.anchorMin = new Vector2(0, 0); rRect.anchorMax = new Vector2(0.2f, 1); rRect.sizeDelta = Vector2.zero;
                var rTxt = rGo.GetComponent<TextMeshProUGUI>();
                rTxt.fontSize = 28; rTxt.alignment = TextAlignmentOptions.Center; rTxt.font = titleText?.font;

                // Nickname
                var nGo = new GameObject("Text_Nickname", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                nGo.transform.SetParent(fallbackPrefab.transform, false);
                var nRect = nGo.GetComponent<RectTransform>();
                nRect.anchorMin = new Vector2(0.2f, 0); nRect.anchorMax = new Vector2(0.7f, 1); nRect.sizeDelta = Vector2.zero;
                var nTxt = nGo.GetComponent<TextMeshProUGUI>();
                nTxt.fontSize = 28; nTxt.alignment = TextAlignmentOptions.Left; nTxt.font = titleText?.font;

                // Stage
                var sGo = new GameObject("Text_Stage", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
                sGo.transform.SetParent(fallbackPrefab.transform, false);
                var sRect = sGo.GetComponent<RectTransform>();
                sRect.anchorMin = new Vector2(0.7f, 0); sRect.anchorMax = new Vector2(1, 1); sRect.sizeDelta = Vector2.zero;
                var sTxt = sGo.GetComponent<TextMeshProUGUI>();
                sTxt.fontSize = 28; sTxt.alignment = TextAlignmentOptions.Center; sTxt.font = titleText?.font;

                // 비활성 템플릿으로 저장해두고 instantiate
                fallbackPrefab.transform.SetParent(this.transform, false);
                fallbackPrefab.SetActive(false);
                rankingItemPrefab = fallbackPrefab;
            }
        }

        private void OnEnable()
        {
            InitializeIfNeeded();
            FetchRankingData();
        }

        private void OnDisable()
        {
            ClearList();
        }

        public void Open()
        {
            gameObject.SetActive(true);
        }

        public void Close()
        {
            PlayButtonSound();
            gameObject.SetActive(false);
        }

        private void ClearList()
        {
            foreach (var item in activeItems)
            {
                if (item != null) Destroy(item);
            }
            activeItems.Clear();
        }

        private void FetchRankingData()
        {
            ClearList();
            if (loadingOverlay != null) loadingOverlay.SetActive(true);

            var auth = GameManager.Instance?.Auth;
            if (auth == null || !auth.IsFirebaseReady)
            {
                OnFetchFailed("Firebase가 아직 준비되지 않았습니다. 잠시 후 다시 시도해 주세요.");
                return;
            }

            string myUid = GameManager.Instance.SaveData?.CurrentUid;
            if (string.IsNullOrEmpty(myUid))
            {
                OnFetchFailed("로그인이 필요합니다.");
                return;
            }

            var db = FirebaseFirestore.DefaultInstance;
            
            db.Collection("users")
                .OrderByDescending("unlockedStageLevel")
                .OrderBy("stageClearTime")
                .Limit(20)
                .GetSnapshotAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (loadingOverlay != null) loadingOverlay.SetActive(false);

                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError($"[RankingPopup] Firestore query failed: {task.Exception}");
                        OnFetchFailed("랭킹 정보를 불러오는 데 실패했습니다.\n(인덱스가 아직 생성되지 않았을 수 있습니다.)");
                        return;
                    }

                    var snapshot = task.Result;
                    if (snapshot == null)
                    {
                        OnFetchFailed("불러올 데이터가 없습니다.");
                        return;
                    }

                    RenderRanking(snapshot, myUid);
                });
        }

        private void RenderRanking(QuerySnapshot snapshot, string myUid)
        {
            int rankCounter = 1;
            bool myRankFound = false;

            var saveData = GameManager.Instance.SaveData;
            string myNickname = saveData.Data.nickname;
            int myMaxStage = saveData.Data.unlockedStageLevel;

            foreach (var doc in snapshot.Documents)
            {
                string uid = doc.Id;
                
                doc.TryGetValue("nickname", out string nickname);
                doc.TryGetValue("unlockedStageLevel", out int unlockedStageLevel);

                if (string.IsNullOrEmpty(nickname)) nickname = "Unknown Necromancer";
                if (unlockedStageLevel <= 0) unlockedStageLevel = 1;

                CreateRankingItem(rankCounter, nickname, unlockedStageLevel, uid == myUid);

                if (uid == myUid)
                {
                    SetMyRankText(rankCounter, myNickname, myMaxStage);
                    myRankFound = true;
                }

                rankCounter++;
            }

            if (!myRankFound)
            {
                SetMyRankText(-1, myNickname, myMaxStage);
            }
        }

        private void CreateRankingItem(int rank, string nickname, int stageLevel, bool isMe)
        {
            if (rankingItemPrefab == null || contentParent == null) return;

            var go = Instantiate(rankingItemPrefab, contentParent);
            go.SetActive(true);
            activeItems.Add(go);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
            
            foreach (var txt in texts)
            {
                string txtName = txt.name.ToLower();
                if (txtName.Contains("rank"))
                {
                    txt.text = $"{rank}위";
                    if (isMe) txt.color = Color.yellow;
                }
                else if (txtName.Contains("nickname") || txtName.Contains("name"))
                {
                    txt.text = nickname;
                    if (isMe) txt.text += " <color=yellow>(나)</color>";
                }
                else if (txtName.Contains("stage") || txtName.Contains("score"))
                {
                    txt.text = $"Stage {stageLevel}";
                    if (isMe) txt.color = Color.yellow;
                }
            }

            if (isMe)
            {
                var bgImage = go.GetComponent<Image>();
                if (bgImage != null)
                {
                    bgImage.color = new Color(0.2f, 0.4f, 0.2f, 0.8f);
                }
            }
        }

        private void SetMyRankText(int rank, string nickname, int stageLevel)
        {
            if (tmpMyRankText == null) return;

            string rankStr = rank > 0 ? $"{rank}위" : "순위 밖";
            tmpMyRankText.text = $"내 순위: <color=yellow>{rankStr}</color>  |  {nickname}  |  도달 스테이지: <color=cyan>Stage {stageLevel}</color>";
        }

        private void OnFetchFailed(string message)
        {
            if (loadingOverlay != null) loadingOverlay.SetActive(false);
            SetMyRankText(-1, GameManager.Instance?.SaveData?.Data?.nickname ?? "Unknown", GameManager.Instance?.SaveData?.Data?.unlockedStageLevel ?? 1);
            
            if (GameManager.Instance != null && GameManager.Instance.Popup != null)
            {
                GameManager.Instance.Popup.ShowMessagePopup(message);
            }
        }

        private void PlayButtonSound()
        {
            if (GameManager.Instance != null && GameManager.Instance.Sound != null)
            {
                GameManager.Instance.Sound.PlaySFX(GameManager.Instance.Sound.sfxSelectBtn, SfxPriority.Critical);
            }
        }
    }
}
