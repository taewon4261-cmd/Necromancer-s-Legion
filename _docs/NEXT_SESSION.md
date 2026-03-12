# 🧛 Necromancer's Legion: Next Session Briefing

이 문서는 다음 작업(AI)이 프로젝트 현황을 1분 만에 파악하고 즉시 개발에 착수하기 위한 핸드오버 가이드입니다.

---

## ✅ 현재 완료된 작업 (Today's Progress)

### 1. 스테이지 시스템 고도화 및 대량 생성
*   **스테이지 50개 생성:** `StageDataGenerator.cs` 에디터 툴을 구현하여 50개의 스테이지 데이터(`StageDataSO`)를 난이도 보정치와 함께 자동 생성 완료.
*   **리소스 자동 로드:** 데이터 위치를 `Resources/Stages`로 이동하여 인게임 로딩 시 `Resources.LoadAll`로 50개 리스트를 자동 확보하도록 구현.
*   **네비게이션 UI:** 단일 스크롤 방식에서 **[이전]/[다음] 버튼**으로 스테이지를 하나씩 넘겨보는 직관적인 내비게이션 구조로 `StageSelectUI.cs` 전면 개편.
*   **잠금(Lock) 시스템:** `ResourceManager.cs`와 연동하여 이전 스테이지를 클리어해야 다음 스테이지가 열리는 진입 제한 로직 기초 구현 완료.

### 2. 비주얼 에셋 강화
*   **스테이지 썸네일:** 12가지 서로 다른 환경 테마(성, 숲, 지옥, 늪지 등)를 담은 프리미엄 썸네일 그리드 제작 및 프로젝트 배치 완료 (`StageThumbnails_V2.png`).
*   **UI 버튼 자동화:** `Btn_Back` 이라는 이름만 붙이면 코드가 알아서 "뒤로가기" 기능을 연결하도록 `TitleUIController.cs` 시스템 고도화.

---

## 🚀 다음 세션 작업 로드맵 (Immediate Action)

### 1단계: 유니티 에디터 UI 조립 (필수)
*   **작업:** `unity_setup_guide.md`의 4번 단계를 보며 `Panel_StageSelect` 내부에 **Prev/Next 버튼**과 **Lock_Overlay** 오브젝트를 배치하고 `StageSelectUI` 인스펙터에 할당.
*   **썸네일 슬라이싱:** `StageThumbnails_V2.png`를 12개로 슬라이싱하여 각 스테이지 SO의 `Stage Thumbnail` 슬롯에 5개 스테이지 단위로 테마별 배치.

### 2단계: 인게임 스테이지 해금 연동
*   **작업:** 실제 전투 승리 시 `GameManager.Instance.Resources.UnlockLevel(nextId)`를 호출하여 다음 스테이지가 타이틀 화면에서 실시간으로 열리도록 결과창 스크립트 연결.

### 3단계: 로비 업그레이드 UI 구현 (데이터 연동)
*   로비에서 획득한 골드를 소모하여 능력치를 올리는 UI 세밀화 및 저장 시스템(`PlayerPrefs`) 연동.

---

## ⚠️ 주의사항
*   스테이지 데이터가 `Resources/Stages`에 있으므로, 이동이나 삭제 시 코드 에러 발생 주의.
*   `TitleUIController`의 버튼 애니메이션은 반드시 제자리(Scale/Fade) 방식을 유지할 것 (위치 이동 금지).
