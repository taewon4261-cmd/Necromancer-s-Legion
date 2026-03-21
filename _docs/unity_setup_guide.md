# 🧛 유니티 에디터 설정 가이드 (Unity Setup Guide)

이 문서는 코드 구현 이후, 유니티 에디터 상에서 직접 오브젝트를 연결하고 에셋을 설정해야 하는 항목들을 정리한 체크리스트입니다.

---

## 1. 썸네일 스프라이트 슬라이싱 (Thumbnail Slicing)
전체 그리드 이미지인 `StageThumbnails_V2.png`를 개별 스테이지 아이콘으로 분리해야 합니다.

1.  **경로:** `Assets/Necromancer/04.Sprites/UI/StageThumbnails_V2.png` 선택
2.  **인스펙터 설정:**
    *   `Texture Type`: Sprite (2D and UI)
    *   `Sprite Mode`: **Multiple**
3.  **Sprite Editor 열기:**
    *   `Slice` 메뉴 클릭 -> `Type`: **Grid By Cell Count**
    *   `Column & Row`: **C: 4, R: 3** (총 12칸) 또는 적절한 비율로 슬라이싱
    *   `Apply` 클릭하여 저장

---

## 2. 타이틀 씬 UI 연결 (`TitleScene`)
스테이지 선택 창의 버튼과 잠금 표시를 연결합니다.

1.  `Panel_StageSelect` 오브젝트 선택 (또는 `StageSelectUI` 스크립트가 붙은 오브젝트)
2.  **StageSelectUI 컴포넌트** 인스펙터에 다음 오브젝트 드래그 앤 드롭:
    *   `Prev Button`: 스테이지 이전 버튼
    *   `Next Button`: 스테이지 다음 버튼
    *   `Lock Overlay`: 스테이지가 잠겼을 때 위를 덮는 이미지/수정용 오브젝트
    *   `Stage Thumbnail`: 중앙의 큰 썸네일 Image 컴포넌트

---

## 3. 인게임 결과창 연결 (`GameScene`)
승리/패배 시 나타날 결과창을 `UIManager`에 연결합니다.

1.  `GameManager` (또는 UI를 관리하는 `UIManager`) 오브젝트 선택
2.  **UIManager 컴포넌트** 인스펙터의 **Result Panel** 섹션에 연결:
    *   `Result Panel`: 승리/패배 시 켜질 부모 Panel 오브젝트
    *   `Result Title Text`: "STAGE CLEAR"가 표시될 TMP 텍스트
    *   `Result Stats Text`: 생존 시간, 골드 등이 표시될 TMP 텍스트
    *   `Back To Title Button`: 결과창의 "로비로 돌아가기" 버튼

---

## 4. 스테이지 데이터 완성 (`StageDataSO`)
각 스테이지 SO 파일에 썸네일과 웨이브 데이터를 할당합니다.

1.  **경로:** `Assets/Resources/Stages/` 폴더 내의 모든 `StageDataSO` 파일 확인
2.  각 파일마다:
    *   `Stage Thumbnail`: 위(1번)에서 슬라이싱한 개별 스프라이트 중 하나를 할당
    *   `Wave Database`: 해당 스테이지에서 사용할 **WaveDatabase** 에셋 할당 (없다면 새로 생성하여 할당)

---

## ✅ 확인 방법
1.  **스테이지 선택:** 타이틀에서 좌우 버튼으로 스테이지가 바뀌고, 썸네일이 정상적으로 나오는지 확인.
2.  **해금 테스트:** `ResourceManager`의 `unlockedStageLevel`을 1로 두고, 첫 스테이지를 클리어한 후 타이틀로 돌아갔을 때 두 번째 스테이지의 `Lock Overlay`가 사라지는지 확인.
