# 🎨 전체 UI Fluent/GitHub-dark 통일 — 실행 계획 (2026-09-28)

> 지시: 준수 판정(P6~P9 포함) 전 창을 피그마/플루언트 디자인 기준으로 재작업.
> 레이아웃·분위기 무조건 Figma 기준, 게임 로직은 Figma에 맞춰 조정.
> 기준: 기존 18종 Figma 창이 쓰는 GitHubDark 팔레트(플루언트 다크) + 카드/그리드/게이지 패턴.

## 디자인 시스템 (Figma = 단일 기준)
- 배경 #0B0E14 / 패널 #161B22 / 보조 #21262D / 액센트 #58A6FF / 골드 #E3B341
- 텍스트 #F0F6FC / 보조텍스트 #8B949E / 스트로크 #2E343D
- danger #F85149 / success #3FB950 / warn #D29922
- 반경 8(메인)/6(서브)/4(배지), 폰트 Roboto + Geist Mono(수치)

## 전략
1. **UTKTheme 공용 팔레트 클래스 신설** — GitHubDark 토큰 중앙화.
   기존 18창의 인라인 GitHubDark는 무수정(회귀 0). 미통일 창만 UTKTheme 참조하도록.
2. 창별 `ApplyGitHubDarkStyle()` 인라인 오버라이드(생성자 1회, 스킬 정석).
3. 게임 로직 = Figma 구조에 맞춰 매칭(데이터 API 소비·표시 재배열), 로직 보존.

## Phase (Feature별 커밋 분리 + 배치컴파일 CS=0 + EditMode + QAPROGRESS/ROADMAP/git)
- P0: UTKTheme 공용 팔레트 신설 + 컴파일 검증
- P1: HUD·상시 묶음 (HUD/Tab/Minimap/Clock/Nameplate/PlayerFlag/BowAim/GasSpray/WarNotify/QuickSlot)
- P2: 공용 프레임워크 (UTKWindowBase/UTKControls/UTKCircularGauge/UTKThreeColumn 등) — 공용 안건드리되 리스타일만 입힐 수 있는지 판별
- P3: 인벤·창고·전리품·아이템 (Warehouse/Loot/Inventory/ItemDesc — 이미 토큰인 것 제외)
- P4: 영지·경제·정치·행사 (TerritoryInfo/Lord/Revenge/Envoy/Spy/Festival/Construction/DynamicEvent/Route/FastTravel/AutoMove/ReadDoc)
- P5: 크래프트·생존·사냥 (CraftBench/WeaponForge/Cooking/Alchemy/Repair/Fishing/Lockpick/Encyclopedia/Arena/Church/Sleep/MonsterSkill)
- P6: 병사·NPC·대화 (NPCDialogue/NPCDaily/QuestChoice/GuardSquadHotbar/Equipment)
- P7: 메뉴·시스템 (MainMenu/Esc/Load/Save/Settings/Options/Death/Loading/EndingCredits/Achievement/GameStats/Tutorial)
- P8: 전수 검증 + EditMode + 커밋 · 통합 Play 검증 대기 보고

## 검증
- 변경 .cs + .meta 동반 git add (git add -A 금지)
- 배치컴파일 error CS=0 / EditMode 재실행 / QAPROGRESS·ROADMAP 갱신