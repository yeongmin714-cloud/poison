# 부대 지정(슬롯/드래그) 후 우클릭 이동 — 분석 + 계획

> 2026-09-27 사용자 요청: "병사 부대 지정 후 우클릭 이동 가능하게. 슬롯 부대 지정 or 드래그로 병사 지정됐을 때 우클릭 → 지정 위치로 이동. 계획해줘"
> **결론: 핵심 인프라 모두 이미 구현돼 있음(P26 외)** — 남은 작업은 UX 갭 확인/보완이다. 실행은 "진행/실행해줘" 확인 후.

## 1. 현 상태 (전부 구현 확인, 2026-09-27 코드 실측)

| 기능 | 구현 위치 | 상태 |
|:--|:--|:--|
| 부대 슬롯 등록 | `GuardSquadHotbar.HandleCtrlAssignKeys()` F+1~8 (그룹 덮어쓰기) · 눌패드 1~8 두 번 연속 | ✅ |
| 부대 모드 토글 | `GuardSquadHotbar.HandleTabKey()` — Tab, `IsSquadMode` static | ✅ |
| 부대 슬롯 선택 | `GuardSquadHotbar.SelectSlotGroup(i)` → `GuardSelectionManager.SelectGroup(alive)` (파랑 원 오라) | ✅ |
| 드래그 다중 선택 | `GuardSelectionManager` — Ctrl+좌클릭 드래그 박스 → `SelectGuardsInRect` → `SelectedGuards` | ✅ |
| 우클릭 이동/공격 | `RTSCommandSystem.IssueRightClickCommand(mouse, ctrl)` — 선택 기반, 지형=이동/적=공격, Ctrl+우클릭=일괄 | ✅ |
| 이동 지점 표시 | `CommandMarker.Spawn(pos, color, owner)` — 지속형 지면 골드 링(MoveTargetRing 베이크) | ✅ |
| 이동 명령 | `GuardPlaceholder.SetCommandTarget(v3, attack)`/`HasCommand`/`ClearCommand` + 이동 루프 | ✅ |
| 부대 핫바 표시 | `GuardSquadHotbarUTK` (표시/해제만) — ⚠ U8 주석 "HotbarUIUTK로 흡수, 독립 바 미표시" | ⚠ 확인 필요 |

**기존 플로우 (원하던 것과 동일)**: F+1~8 부대 등록 → Tab 부대모드 → 1~8 선택(파랑 원) → **우클릭 = 선택 부대가 지형으로 이동** + 지면 링. 드래그 후 우클릭 이동도 동일 경로(`GuardSelectionManager` 게이트).

## 2. 관찰된 잠재 갭 (실행 시 확인해야 할 것)

1. **부대 핫바 시각**: `GuardSquadHotbarUTK.Bootstrap`가 U8 은퇴로 "독립 바 미표시" → **부대 슬롯이 화면에 안 보일 수 있음**. 실제로 `HotbarUIUTK`(숫자아이템 핫바) 내로 흡수됐는지, 부대 모드 시 슬롯이 뜨는지 Play 확인 필요.
2. **드래그 진입 조건**: `GuardSelectionManager` 드래그는 **Ctrl 홀드** 필요 + 부대 모드(`squadModeActive`) 상호작용. "그냥 드래그해서 병사 지정"이 항상 되는지 확인.
3. **우클릭 이동 트리거**: 드래그/슬롯 선택이 끝난 뒤 우클릭이 `IssueRightClickCommand`로 정상 잡히는지(ContextCommandRouter L208 / GuardSelectionManager L281 두 진입점).

## 3. 계획 (UX 갭 확인 → 필요 시 소폭 보완)

| Phase | 작업 | 방식 |
|:--|:--|:--|
| P1 | **부대 핫바 표시 복원/확인** | `GuardSquadHotbarUTK` 미표시 원인 확인. HotbarUIUTK에 부대 슬롯 흡수 안 됐으면 U8 은퇴 되돌려 독립 바 재표시(Tab 시). |
| P2 | **드래그 선택 UX 보완** | "그냥 드래그" 경로 — Ctrl 없이(또는 아이템모드) 드래그 선택이 부대 모드 전환 부담 없이 되도록. `squadModeActive`/`ctrlActiveForDrag` 조건 재검토. |
| P3 | **선택 후 우클릭 이동→링** 검증 | 드래그/슬롯 선택 직후 우클릭 → `IssueRightClickCommand` → 이동 + `CommandMarker` 링. RTSCommandSystem 선택 병사 0일 때 안내. |
| P4 | 마무리 | 컴파일0 + EditMode + QAPROGRESS/ROADMAP/git 3종. |

## 4. 판단 안내
핵심 로직(등록/선택/이동/링)은 이미 완성이므로 **신규 대규모 구현 없음**. 실제 Play에서 "슬롯이 안 보인다 / 드래그가 안 된다 / 우클릭 이동이 안 된다" 중 무엇이 문제인지 확인하면 해당 소폭만 고치면 된다. Play 스샷(16.PNG 순번) 주시면 즉시 해당 갭 수리합니다. — **"진행/실행해줘" 시 P1부터 실행.**