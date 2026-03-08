# 🧛 Necromancer's Legion: Next Session Briefing

이 문서는 다음 작업(AI)이 프로젝트 현황을 1분 만에 파악하고 즉시 개발에 착수하기 위한 핸드오버 가이드입니다.

---

## ✅ 현재 완료된 작업 (Ground Truth)

### 1. 그래픽 에셋 파이프라인 안정화
*   **파일:** [SpriteAutoProcessor.cs](file:///c:/Users/USER/Documents/GitHub/Necromancer-s-Legion/Assets/Necromancer/Editor/SpriteAutoProcessor.cs)
*   **핵심 기능:** 
    *   **자동 슬라이싱:** 파일명 카테고리(`Move`, `Attack`, `Die`) 기반 1x4 슬라이싱 자동 수행.
    *   **PPU 규격화:** 이미지 해상도에 상관없이 인게임 캐릭터 높이를 **2.5 Unit**으로 강제 통일 (공격 시 캐릭터 커짐 방지).
    *   **아이콘 보존:** `Icons` 키워드는 자동 슬라이싱에서 제외하여 개별 고화질 에셋 유지.

### 2. 에셋 생성 현황 (`04.Sprites/`)
*   **Characters:** 플레이어, 미니언 6종, 적 몬스터 10종 애니메이션 시트 확보.
*   **Skill Icons:** 20종 스킬에 대한 **개별 아이콘** 생성 및 `SkillIcons/` 폴더 배치 완료.
*   **UI/VFX:** 조이스틱(분리형), 투사체(뼈, 마법), 스킬 카드 베이스 확보.

### 3. 데이터 구조 (`02.Data/`)
*   **SkillData (SO):** 20종의 스킬 정보를 담은 ScriptableObject 에셋 생성 완료.

---

## 🚀 다음 세션 작업 로드맵 (Immediate Action)

### 1단계: 스킬 데이터 마감 (Data Binding)
*   `04.Sprites/SkillIcons/`에 있는 개별 이미지들을 `02.Data/SkillSO/`의 각 에셋(`Skill Icon` 필드)에 수동 또는 자동 드래그 앤 드롭으로 연결.

### 2단계: 타이틀 화면 연출 (Title Scene Polishing)
*   **Scene:** `TitleScene`
*   **Task:** [TitleUIController.cs](file:///c:/Users/USER/Documents/GitHub/Necromancer-s-Legion/Assets/Necromancer/01.Scripts/UI/TitleUIController.cs)를 사용해 로고 부유, 버튼 슬라이드 인 애니메이션 구현 (DOTween 필수).

### 3단계: 캐릭터 애니메이터 설정
*   슬라이싱된 1x4 에셋들을 이용해 각 유닛의 `Animator Controller` 생성 및 `Move`, `Attack`, `Die` 상태 연결.

---

## ⚠️ 주의사항
*   모든 캐릭터는 **PPU 자동 보정 스크립트**의 영향을 받으므로, 크기가 안 맞으면 스크립트의 `TARGET_UNIT_HEIGHT` 상수를 수정하세요.
*   DOTween이 설치되어 있어야 UI 연출이 가능합니다.
