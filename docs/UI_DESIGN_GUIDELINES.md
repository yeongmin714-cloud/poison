# 🎨 Poison UI 디자인 가이드라인 (Medieval Fantasy × Duckov Cleanish)

> **적용 대상**: 게임 내 **모든 UI** (인벤토리/장비/창고/전리품/스테이터스/미니맵/핫바/체력바/HUD 등 전 기존 창 + 앞으로 새로 만드는 모든 UI).
> **원칙**: ① 기존 **레이아웃 규칙은 유지**(인벤토리=왼쪽, 창고=오른쪽, 화면 3분할 등). ② **디자인(색/폰트/간격/스타일)만** 중세 판타지 × Duckov 클린으로 교체. ③ 모든 신규 UI는 이 문서를 참조.
> **기술**: Unity IMGUI(OnGUI). 외부 에셋 의존 최소화, 절차(코드) 기반이라 프로젝트 내 `UIStyleManager.cs`(전역 스킨) 중심으로 토크나이즈.

---

## 1. Design System & 토큰 (Design Token)

모든 색/폰트/간격은 **중앙 토큰**(`UIStyleManager` + `UIFont`)으로 정의하고, 창별 OnGUI는 토큰만 참조한다. 로컬 매직넘버 금지(스케일 규칙과 충돌).

### 1.1 색 팔레트
| 토큰 | HEX | 설명 | 용도 |
|:--|:--|:--|:--|
| `BgPanel` | `#1C1C1C E0` (딥 차콜 레더) | 패널 배경, 반투명 매트 | 캔버스/백플레이트 |
| `BgPanelDark` | `#141414 E8` (다크 슬레이트) | 심층 구조/내부 영역 | 슬롯 배경·호버 베이스 |
| `BorderBronze` | `#8C6B3F` (브론즈) | 1-2px 날카로운 경계선 | 창 테두리·구분선 |
| `BorderGold` | `#C9A227` (앤틱 골드) | 강조 테두리 | 선택/마법·희귀 |
| `IronLine` | `#3A3A3A` (다크 아이언) | 미세 구분 | 슬롯 그리드 라인 |
| `TextPrimary` | `#F5EFE0` (양피지 화이트) | 1차 텍스트 | 제목·수치 |
| `TextSecondary` | `#B9B3A6` (뮤트 웜 그레이) | 2차 텍스트 | 설명·부가정보 |
| `AccentMagic` | `#4A7BD0` (마법 블루) | 강조 통계 | 마나/마법 속성 |
| `AccentRare` | `#E7B73A` (희귀 골드) | 희귀 아이템 | 등급 강조 |
| `HealthRed` | `#C83838` (헬스 레드) | 핵심 스탯 | 체력바/위험 |
| `GuildGreen` | `#5E8C4A` (가문 초록) | 성공/양수 | HP/성공 |
| `HoverGold` | `#D9B45B` | 호버 강조 | 인터랙션 피드백 |

> 기존 `UIStyleManager`의 미드나이트 블루(`BgColor 0.063,0.086,0.133`) → 위 **치콜/브론즈 파레트**로 교체. 앤틱골드 트림 + 양피지 화이트 타이포로 전환.

### 1.2 폰트 & 타이포그래피 계층 (Crucial)
- **타이포 계층**: `UIFont.Display(제목) > Title(섹션) > Heading(아이템명) > Body(설명) > Stats(수치·숫자) > Badge(등급/수량)`.
- **가독성 절대 우선**: 텍스트는 항상 배경 대비를 보장(텍스트 주변 패딩 4~8px 고정, 진해진 폰트). 숫자는 **별도 Stats 스타일**(굵게+고정폭 계열)로 항상 도드라지게.
- **스케일·해상도 무결성**:
  - 모든 fontSize/상수는 `UIFont`(Display60/Title38/Body24/Caption17/Badge13)를 쓰고, 창 레이아웃 비례는 `_uiScale = sqrt((W/1920)*(H/1080))` 단일 산식.
  - 비16:9/해상도 변경에서 **폰트 왜곡·확대 비균일·레이아웃 파열 방지** — 레이아웃 배수는 `_uiScale`, 폰트는 `UIFont` 스케일로만. 로컬 px 하드코딩 금지.
  - 해상도 변경 감지 시 `_stylesInitialized=false` 재생성(UIFont/UIStyleManager 관례 유지).

### 1.3 간격·라디우스
- **그리드 패딩**: 4px/8px 배수. 창 외곽 패딩 8px, 슬롯 사이 4px, 섹션 사이 8px.
- **모서리**: 한정적 둥근 모서리(슬롯 2px) 또는 깔끔한 직각을 **창 타입별 하나로 통일**.
- **구조선**: 브론즈 1-2px 날카로운 라인으로 그리드 정렬. 과한 그림자/글로우 지양(클린).

---

## 2. 레이아웃 규칙 (기존 유지 — 변경 금지)

> ⚠️ 이 절은 **기존 규칙 그대로 유지**한다. 아래는 참고용 정의.

| UI | 위치 | 규칙 |
|:--|:--|:--|
| 인벤토리 | **왼쪽** | 1구획(x=6), 그리드 5×6 |
| 장비칸(Equipment) | 우측 임베디드 | `EquipmentWindow.TryRenderEmbedded(2S/3+6)`, ContextMode.None 시만 |
| 창고(Warehouse) | **오른쪽** | 3분할 우측 구획 |
| 전리품(Loot) | 화면 우측 | `2S/3+6`, 높이 Screen-180 |
| 스테이터스(Status) | HUD 우측 | 스탯 패널 |
| 미니맵(Minimap) | 화면 우측 상단 | _uiScale 전파 |
| 핫바(Hotbar) | 하단 중앙 | 캔버스 스케일 |
| 체력바(HUD) | 상단 좌측 | 하트 1칸=20HP, 하트 아이콘 절차 마스크 |

> 디자인 변경 시 이 위치/크기 규칙은 **건드리지 않는다**. 색/폰트/간격/재질만 교체.

---

## 3. Unity IMGUI 기술 표준

- **응답형 레이아웃**: `_uiScale` 단일 산식 + `UIFont` 폰트 스케일로 전 창 비례 (Flexbox 대체 — IMGUI 한계 내 동일 목적).
- **계층·성능**: 정적 배경(백플레이트/테두리 텍스처)은 **static 캐시**(`UIStyleManager.MakeTexture` 1회)로, 동적 요소(슬롯/체력바)만 매 프레임 렌더. `OnGUI` 에서 `new GUIStyle/Texture` 금지(GC/재생성 방지).
- **인터랙션 피드백**: 각 컴포넌트에 Hover/Pressed/Disabled 상태 명시 — 브론즈→골드 호버, 프레스 감쇠, 비활성 회색. 무드 있는 택타일 피드백.
- **캔버스 재생성 최소화**: 씬 진입/해상도 변경 시에만 스타일 재생성, 매 프레임 재계산 금지.

---

## 4. UI 타입별 디자인 명세 (변경 대상)

| 컴포넌트 | 현재 | 변경 목표 |
|:--|:--|:--|
| 인벤토리 창 | 미드나이트 블루 패널 | 딥 차콜 레더 패널 + 브론즈 테두리 + 양피지 화이트 아이템명 |
| 장비칸 | 파란 강조 | 골드 장착 강조, 슬롯 배지(힐멧/갑옷 아이콘) 브론즈 라인 |
| 창고 창 | 파란 강조 | 브론즈 그리드 + 카테고리탭 골드 활성 |
| 전리품 창 | 파란 강조 | 오렌지→브론즈/골드, 획득 아이콘 양피지 |
| 스테이터스 창 | 파란 | 골드 타이틀 + 마법블루 스탯수치 |
| 미니맵 | 기존 | 브론즈 프레임 + 양피지 배경 톤, 마커 유지 |
| 핫바 | 기존 | 골드 슬롯 테두리 + 양피지 단축키 숫자 |
| 체력바/HUD | 기존 하트 | 하트 마스크 유지 + 레드/골드 그라데이션, 폰트 양피지 |
| 퀘스트/레시피/연금 | 파란 | 전술 다크 패널 + 골드 트림 |
| 상호작용 창(병사/크래프트 등) | 파란 | 중세 팔레트 통일(37차 병사창 선례 확장) |

---

## 5. 구현 시 규칙 (Execution Output Requirements)

1. **모듈식 구조**: 설계는 **전역 토큰(UIStyleManager)·폰트(UIFont)·컴포넌트 헬퍼**로 분리. 창은 자기 OnGUI만 표시.
2. **가독성·스케일 명시**: 각 창 코드 주석에 "어떻게 시각 계층/타이포 스케일을 강제하는지"(예: `이 제목은 Title38+브론즈 라인, 숫자는 Stats+마법블루`) 명시.
3. **새 UI 작성 시**: 이 문서 §1(토큰)·§2(레이아웃)·§3(IMGUI 표준) 준수 — 색은 `UIStyleManager` 토큰, 폰트는 `UIFont`, 크기는 `_uiScale`만 사용.
4. **회귀 금지**: 위치/레이아웃 상수는 §2 유지. 디자인 토큰만 교체.

---

## 6. 수리 계획 반영 (TEST21_FOLLOWUP_PLAN 연계)

- **Phase G — UI 디자인 전면 교체 (New)**: 위 가이드라인 적용.
  - G-1: `UIStyleManager` 파레트 전환(딥 차콜/브론즈/골드/양피지) + `UIFont` 타이포 계층 확정.
  - G-2: 왼쪽=인벤토리 / 오른쪽=창고·전리품 / 상단=체력·미니맵 / 하단=핫바 위치 **유지**하며 디자인만 교체.
  - G-3: 전 기존 창(UIStyleManager 참조 창 전수) + 신규 창 생성 시 §1~§3 준수.
  - G-4: 배치컴파일 error CS=0 + Play 시각 검증(가독성/스케일/테두리).

---

## UTK (UI Toolkit) 표준 — Phase 68 (2026-09-18)

> 모든 신규 UI는 UI Toolkit(UXML/USS) — IMGUI 신규 금지. 계획서: docs/UI_TOOLKIT_MIGRATION.md

### 토큰 이식 (Theme.uss `:root` 변수)
| 기존 토큰 | USS 변수 |
|:--|:--|
| BgPanel #1C1C1C E0 | `--c-bg-panel` |
| BgPanelDark #141414 E8 | `--c-bg-panel-dark` |
| BorderBronze #8C6B3F | `--c-border-bronze` |
| BorderGold #C9A227 | `--c-border-gold` |
| TextPrimary #F5EFE0 | `--c-text-primary` |
| TextSecondary #B9B3A6 | `--c-text-secondary` |
| AccentMagic #4A7BD0 | `--c-accent-magic` |
| AccentRare #E7B73A | `--c-accent-rare` |
| HoverGold #D9B45B | `--c-hover-gold` |
| 등급 6종 | `--c-rank-common`~`--c-rank-unique` |

### 폰트 5단 (기존 UIFont 계승)
`--fs-xl: 60px` / `--fs-lg: 38px` / `--fs-md: 24px` / `--fs-sm: 17px` / `--fs-xs: 13px`
실제 폰트는 `UTKWindowBase.ApplyUIToolkitFont(VisualElement)`로 적용.

### 레이아웃 관례 (유지)
좌: 인벤 / 우: 창고·장비 · 전리품=우측 2S/3+6(높이 Screen-180) · 월드맵=양피지+정규화 좌표

### 엔진 규약 (마이그레이션 중 확립)
1. IStyle은 셔스루햇 없음 — margin/padding/borderColor/borderWidth 반드시 Top/Bottom/Left/Right 4면 개별
2. 폴링 = `element.schedule.Execute(Action).Every(ms)` → `IVisualElementScheduledItem`, 정지=`Pause()`
3. static Toggle = `if (_instance != null && _instance.IsOpen) { _instance.Close(); return; } Ensure();`
4. DropdownField는 3-arg `(label, choices, defaultIndex)`만 안전
5. 보간문자열(`$"..."`) 내 중첩 따옴표 금지 — `+` 문자열 연결로 재구성
6. 구조체 반환 API(GetDefinition 등) null 비교 불가 — 필드 빈값 체크(IsNullOrEmpty)
7. Systems→UI 순환참조 회피: 정적 이벤트(OnXxxRequestedUTK) + UTKWireUp 구독 브리지, 폴백 내장
8. 호버/프레스 상태: USS `:hover` + UTKButton.Variant(Primary/Secondary/Danger)
