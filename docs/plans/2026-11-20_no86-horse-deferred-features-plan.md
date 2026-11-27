# no86 — 말 보류 기능 구현 계획 (2026-11-20 — 사용자 "보류도 승인" 승인 반영, 즉시 실행)

## 0. 범위 판정 (실측 근거)

| 보류 항목 | 판정 | 근거 |
|---|---|---|
| 세이브 연동 | ✅ 착수 | SaveData v5 구조 + VillageDrugState/LaborMarketSystem Export·Import 선례(SaveManager L127/205, L133/828) |
| 앉기 자세 애니(승마 포즈) | ✅ 착수 | Animations/MeshyUser에 Sit 계열 3종(Sit_Lie_Bed·Stand_to_Sit_Transition_M·Sit_to_Stand_Transition_M) + HumanoidClipDriver 트리거 API |
| 말 전투(발길질) | ✅ 착수 | AttackSystem.Update 좌클릭 경로(L59~) + Rear 클립(85-P6 재용) |
| 황마 | ⛔ 에셋 블로크 | GLB만 존재, 애니 FBX 0종 |
| 갈색말 | ⛔ 에셋 블로크 | 애니 FBX 4종 존재, GLB 부재 |
| 사운드 | ⛔ 에셋 블로크 | Resources/Audio·Resources/Sounds 전수 스캔 — 마필 SFX 오디오 0종. 오디오 파일 제공 시 합류 |

## 1. Phase 분해

### P1 — 세이브 연동 (말 상태 저장/복원)
- `Assets/Scripts/Systems/SaveData.cs`: `HorseSaveEntry` DTO(breed/tamed/posX/posY/posZ) + `SaveData.horses` 리스트 필드 + saveVersion 5→6 승격 + 헤더 주석 갱신
- `Assets/Scripts/Systems/SaveManager.cs`: L188 버전 검증(>5→>6)·안내문 갱신, Save()에 `HorseMountSystem.ExportSaveData()` 훅, Load()에 `ImportSaveData` 훅(구세이브=빈 리스트 → 전부 야생 유지)
- `Assets/Scripts/Systems/HorseMountSystem.cs`: Export/ImportSaveData — 스폰 후 상태 복원(길들임·위치·접지). Instance 미성립 시 pending 버퍼 후 적용
- 검증: HorseMountSaveTests(라운드트립·구세이브 폴백·DTO 직렬화)

### P2 — 승마 자세 (탑승 중 플레이어 앉기)
- Player_AC 컨트롤러에 Sit 홀드 상태 추가 — Editor 배치 배선 스크립트(SpearThrustClipWiring 패턴). Sit 클립 3종 실측 후 착지형 홀드 선택(루프 off·exit 없음 = 마지막 프레임 홀드)
- `HumanoidClipDriver.cs`: `SetMounted(bool)` API — 탑승 시 Sit 진입, 하차 시 기존 Speed 게이트 복귀
- `HorseMountSystem.cs` Mount()/Dismount()에서 드라이버 호출 연동
- 검증: MountedPoseTests(마운트→포즈 진입 플래그, 하차→복귀). 포즈 높이/안장 정합은 Play 튜닝

### P3 — 말 전투 (발길질)
- `HorseMountSystem.cs`: `IsMounted` static + 탑승 중 좌클릭 → Rear 재생(anim.Play("Rear") 재용) + 전방 IDamageable 타격(반경 3m, 쿨다운 1.0s 결정론). 피해 = PlayerCombat 무기 데미지 재사용(부재 시 10 고정)
- `AttackSystem.cs` Update: IsMounted 시 좌클릭 무시(승마 중 검격 억제)
- 검증: MountKickTests(발길질 발화·피해 판정·하차 후 공격 경로 복원)

## 2. 계약
- 말 리그 절차애니 부착 금지(클립 재생 고정) · Random 금지(결정론) · .meta 수기 금지 · 카메라 팔로우 무수정
- MountUI/MountSystem legacy 무접촉 · [무번호 86-*] 마커 주석 · 커밋·Play 금지(별도 게이트)
- QA는 구현과 분리 위임(AGENT_DELEGATION_POLICY) — 컴파일 error CS 0 + 전수 EditMode baseline(2581/2571/10) 1:1 + 신규 3종 집중
