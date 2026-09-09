# 🛠️ 실내 크래프팅(장비·요리·물약) + 창고 시스템 통합 계획

> **요구 (사장님):** 실내씬에 크래프팅 박스들(장비/요리/물약) 배치 + 창고 배치 후, 크래프팅 시스템과 창고 시스템을 구현.
> 작성일: 2026-09-09
> 상태: 📋 계획 (실행 전) — "진행" 시 Phase 순차 실행

---

## 0. 조사 결과 (검증 완료)

### ✅ 이미 구현·완성된 것 (재사용, 중복 금지)

| 시스템 | 파일 | 상태 |
|:--|:--|:--:|
| 장비 제작 UI | `UI/CraftingUI.cs` | 완성 (재료 2슬롯+레시피+성공/실패+XP+발견) |
| 요리 UI | `UI/CookingUI.cs` | 완성 (고기+약초 → 요리) |
| 물약(연금) UI | `UI/AlchemyUI.cs` | 완성 (레시피+성공률+XP) |
| 장비 스테이션 | `UI/CraftingStation.cs` | 완성 (E→CraftingUI, `craft_equipment` 모델) |
| 요리 스테이션 | `UI/CookingStation.cs` | 완성 (E→CookingUI, `craft_cook` 모델) |
| 물약 스테이션 | `UI/AlchemyStation.cs` | 완성 (E→AlchemyUI, `craft_blend` 모델) |
| 영지 작업대 | `UI/TerritoryCraftingStation.cs` | 완성 (Configure/CanUse/레벨제한) |
| 창고 시스템 | `Systems/WarehouseSystem.cs` | 완성 (영지별 20슬롯+SaveData+Transfer) |
| 창고 상호작용 | `UI/TerritoryWarehouse.cs` | 완성 |
| 가구 배치 헬퍼 | `Systems/IndoorFurniturePlacer.cs` | 완성 (CreateTable/Counter/Shelf) |
| 모델 GLb | `Resources/Models/UserProvided/craft_equipment|cook|blend.glb` | 3종 존재 |

### ⚠️ 실제 갭 (구현 필요)

1. **요리(CookingStation) / 물약(AlchemyStation) 스테이션이 어떤 실내씬에도 배치 안 됨** — 장비만 배치됨.
   - `PlayerCastleInteriorBuilder.cs` : 장비 작업대(TerritoryCraftingStation)만 부착. 요리/물약 없음.
   - `CraftHouseInteriorBuilder.cs` : `CraftingStation` 1개만 부착. 요리/물약 없음.
2. **요리/물약용 시각 가구(테이블/카운터) 미배치** — 모델은 있으나 스테이션 기준 GameObject 부재.
3. **창고 시각 배치는 플레이어 성에만 존재** — 저장고(선반 상호작용)+무기고. 크래프트하우스/기타 실내엔 창고 미배치.
4. **3종 스테이션 + 창고의 통합 배치/상호작용 검증 없음** — F8 실내 진입으로 점검 필요.

---

## 설계 원칙

- **중복 금지**: UI/시스템/모델은 이미 완성 → 새로 만들지 말고 **기존 코드를 재사용해 배치(부착)만** 한다.
- **부착 패턴 일관성**: 실내씬 빌더들이 이미 쓰는 방식 그대로.
  - `Systems asmdef`는 `UI asmdef`를 참조 못 함(순환참조) → `PlayerCastleInteriorBuilder`는 **리플렉션 `AttachUiComponent(...)`** 사용.
  - `CraftHouseInteriorBuilder`는 같은 asmdef(UI) → **직접 `AddComponent<T>()`** 사용 가능.
- **결정론**: 레이아웃 변형(variant) 무관하게 3종 스테이션+창고가 항상 존재해야 함(이름 기반 조회/앵커 유지).
- **모델 폴백**: 모든 스테이션은 GLb 없으면 큐브 플레이스홀더 생성 → 배치 실패해도 파괴되지 않음.

---

## Phase 1 — 플레이어 성: 요리/물약 스테이션 추가 배치

**파일:** `Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs` (ADAPT — 직접 참조 사용 가능? Systems asmdef이므로 리플렉션 사용 확인 필요)

**목표:** `BuildPlayerCastleInterior(nation, variant)`에 요리 테이블 + 물약 테이블 + 3종 스테이션 배치.

**작업 1-1.** 요리 코너(작업대 근처)에 `CookingStation` 부착 — 테이블(IndoorFurniturePlacer) + `craft_cook` 모델 기준 GameObject.
- `AttachUiComponent(table, CookingStation 타입명)` — 영지 키 전달 없음(전역 레시피).

**작업 1-2.** 물약(연금) 코너에 `AlchemyStation` 부착 — `craft_blend` 모델 기준.
- 벽/코너 배치로 variant별 좌표 2~3개 테이블(기존 `GetLayoutVariantParams` + `CreateCounter` 재사용).

**작업 1-3.** Nameplate 추가: `🍳 요리 테이블` / `🧪 연금술 테이블` (기존 `AddNameplate` 패턴).

**작업 1-4.** 컴파일 검증 (error CS = 0) + 변형(variant 0~7) 재방문 시 3종 스테이션 항상 존재 확인(주석/가드).

---

## Phase 2 — 크래프트하우스: 3종 스테이션 + 창고 배치

**파일:** `Assets/Scripts/UI/CraftHouseInteriorBuilder.cs` (직접 AddComponent 가능)

**목표:** 크래프트하우스를 "만능 제작소"로 — 장비/요리/물약 + 창고.

**작업 2-1.** 이미 있는 `craftStation`(장비) 유지. 추가로 요리 스테이션(CookingStation) + 물약 스테이션(AlchemyStation) GameObject 생성·배치.
- `craft_cook`, `craft_blend` 시각 부여(모델 또는 플레이스홀더 + 테이블).

**작업 2-2.** 창고 배치 — `TerritoryWarehouse` 부착 선반(또는 상자) 추가. (`AddComponent<TerritoryWarehouse>` + `Configure(영지키)`).
- 창고 레이아웃: 한쪽 벽 선반 추가, 기존 workbench와 겹치지 않게.

**작업 2-3.** Nameplate 추가 (`🏺 창고` / 요리·물약 라벨).

**작업 2-4.** 컴파일 검증.

---

## Phase 3 — 창고 시스템 시각 확장 (플레이어 성)

**파일:** `Assets/Scripts/Systems/PlayerCastleInteriorBuilder.cs`

**목표:** 창고가 더 명확히 보이도록 시각/라벨 개선 + 무기고/저장고 스테이션 정상 회동 확인.

**작업 3-1.** 저장고/무기고 `TerritoryWarehouse`가 정상 부착되어 E키→WarehouseUI 여는지 확인(코드 추적+주석 점검).
- `TerritoryWarehouse.Configure` 파라미터(슬롯 수, 이름) 확인·보강.

**작업 3-2.** 창고 Nameplate/라벨 일관화: `🎒 저장고` / `⚔️ 무기고` (기존 유지, overlap 점검).

**작업 3-3.** 컴파일 검증.

---

## Phase 4 — 통합 검증 (F8 실내 진입)

**파일:** (수정 없음 — 테스트)

**목표:** 실제 런타임에서 3종 크래프트 + 창고가 동작하는지 눈검증.

**작업 4-1.** Play → F8(실내 진입, IndoorDebugEnterExit) → 플레이어 성.

**작업 4-2.** 장비 스테이션: E → CraftingUI 열림 → 재료 조합 → 제작/성공/XP.

**작업 4-3.** 요리/물약 스테이션: E → CookingUI/AlchemyUI 열림 → 조합 → 획득/XP.

**작업 4-4.** 창고(저장고/무기고): E → WarehouseUI → 아이템 입출고/Transfer → 인벤 반영 확인.

**작업 4-5.** F8 재입력 → 월드 복귀 정상.

**결과 기록:** QAPROGRESS + ROADMAP 체크 → git commit/push → 영구메모리.

---

## 실행 순서 요약

1. Phase 1 → 컴파일(CS=0)
2. Phase 2 → 컴파일(CS=0)
3. Phase 3 → 컴파일(CS=0)
4. Phase 4 → Play 눈검증 (F8)
5. QAPROGRESS + ROADMAP + git push + 메모리 (3중 저장)

> ⚠️ 각 Phase: 코드/QA는 `delegate_task` 위임. 배치컴파일 error CS=0 선행 → 성공 시에만 DLL 재생성(에디터 실행 중이면 에디터 자동재컴파일 + DLL strings grep 판정).