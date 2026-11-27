# 계획서: 행동 인터페이스 VFX 고도화 (무번호 74 제안 — F1 낚시 라인·릴 시즌 / G1 벌목 쓰러짐·채널 링 / H1 수확 파티클)

**상태**: 계획 제안 — 승인 후 착지. Play·커밋 별도 승인 유지.

---

## 0. 중복 감사 (전수 실측 — 이대로 없으면 재구현)

**이미 있는 것 (수리·재작성 대상 아님)**:
- 타격 파티클 6종 프리펩 전부 착지: `Resources/VFX/Gather/{GatherVFX_WoodChips,StoneFragments,LeafPuff,SoilBurst,FishRipple,WaterDroplets}` — GatherFeedbackSystem(133·153차)이 SFX+파티클+셰이크 일괄 발화
- 벌목 중 나무 스웨이: `ResourceNode.ApplyWoodSway` (진행률 0.7→3.0° 사인파)
- 완료 시: 마지막 타격+셰이크+히트스톱(0.06s)+곡갱이질 애니(156차 P5)+AcquisitionCinema
- 농경: Soil(굉이질/파종/정리)·WaterPour 발화점 전부 배선(153차)
- 낚시: 캐스팅/입질 리플 2점만(FishingSystem 315·352·428행)

**공백 (본 계획 대상 — 실측 근거)**:
1. **낚싯줄·낚싯대 비주얼 자체가 0건** — FishingSystem에 LineRenderer/Rod 비주얼 전무(인벤 요구 `"fishing_rod"` 1줄만). 캐스팅이 "보이지 않는 라인"으로 이뤄짐
2. **릴 게임(진행/긴장) VFX 전무** — `_reelProgress`·`_reelTension`이 0..1 수치로만 존재, 물리적 표현 0
3. **벌목 완료 시 쓰러짐 0건** — StopSway 후 `_visual` 숨김만("나무 대신 그루터기 잔존" 계약은 유지), 통나무 넘어가는 연출 없음
4. **채널 중 타격 확산 감이 없음** — 파랑 진행바는 UI 전용, 월드 공간에서는 스웨이 소수각뿐
5. **수확 완료(OnHarvested) 시점 파티클 0건** — 수확은 결과 창으로만

## F계 — 낚시 (규모 대·체감 최대)

| 항목 | 내용 | 선례 재사용 |
|---|---|---|
| F1 낚싯대·낚싯줄·찌 | 캐스팅 방향 라인(LineRenderer — 그림자암살자 스트릭 인프라 패턴)+찌 원판(1회성 FX 리소스 경량) + 투척 포물선 라인 갱신 | ParryDeflectFan 라인 패턴 |
| F2 릴 시즌 긴장 시각화 | `_reelTension` 0..1 → 라인 굵기(0.03→0.09m)+색(파랑→적 경고 틴트)+라인 쪽 꺾임 진폭, `_reelProgress` → 물속 라인 방향 이동+잔물선 패치(진행률 지점 갱신) | 링발광캡(68)·기존 색 계약(왕자 청록 등 무관 — 라인 전용 팔레트) |
| F3 낚아 올리기/실패 | 성공: 지점→플레이어 물기둥 스트릭+물고기 실루엣 포물선 점프(절차 본 이징 — FXMotion 재사용) / 실패: 라인 스냅(전단 분절)+물튀김 소형 입자 | FxDeathRelax 절차 본 회전 · 스트릭 파편(Fragment) 패턴 |

## G계 — 벌목/채광 (규모 중)

| 항목 | 내용 | 선례 재사용 |
|---|---|---|
| G1 채널 리듬 확산 링 | 채널 타격마다 타격점에 소형 확산 링(반경 0.35m, 0.4s) — 기존 파티클 위 '타격 파장' 가시화. **기존 링 예산(MaxConcurrentRings 24) 공유 — X2 계약 내에서만**, 리듬 간격 쿨다운 | ShockwaveRingFX.Spawn |
| G2 벌목 완료 쓰러짐 시퀀스 | 그루터기 잔존 계약 유지 + 통나무가 본체(숨김 전)에서 절차 쓰러짐(가도 방향 축 회전 0.9s·가속 이징) + 지면 먼지 기둥 1회 + 수목 통과음. 쓰러짐 완료 후 기존 숨김 경로 그대로 | FxDeathRelax 절차 회전 · FxEnvReaction 스윙 패턴 |
| G3 채광 완료 파편 폭산 | 완료 시점 StoneFragments 대신 1회 강화 버스트(수 1.6배·드리프트 확대 — 기존 프리펩 파라미터 클론, 에셋 무변경 계약) | 기존 파티클 인프라 |

## H계 — 농경/획득 (규모 소) — **착지 판정: 보류**

| 항목 | 내용 |
|---|---|
| H1 수확 파티클 | **중복 판정(기각)** — `HerbPickup.OnChannelImpact`(133차)이 채널 리듬마다 이미 잎 파티클을 발화 중. 완료 시점 추가는 이중 발화 |
| H2 획득 빛 이온 | 보류 — AcquisitionCinema 오케스트레이션과의 소유권 검토 후 별도 라운드 |

## 착지 기록 (2026-11-18)

- **착지**: F1~F3(`FishingLineVisual.cs` 신규 407행 + FishingSystem 훅 6곳) / G1~G3(`FellTreeSequence.cs` 신규 + ResourceNode 채널 링·쓰러짐 리팩터링 + GatherFeedbackSystem intensity 파라미터)
- **판정 2건**(구현 서브 제안, 부모 승인): BeginCast owner에 플레이어 폴백 적용(싱글턴 원점 문제 — ResolveCastPoint 선례 준용) / 쓰러짐 완료 시 베이스 회전 원복(리스폰 직립 보존)
- **수리 1건**(부모): FishingLineVisual `BeginPhase` 미정의 CS0103 ×5 → 페이즈 전이 메서드 신설
- **테스트**: FxFishingLineP74Tests(11) + FxGatherActionP74Tests(5) — 16/16 GREEN
- **QA**: 컴파일 error CS 0 ×2(수리 전후) + 전수 EditMode **2548/2538/10** — baseline(qa_73) 실패집합 1:1

## 잠금 무손상 (핵심)

- **73-E2 FxSwayRunner vs ResourceNode.ApplyWoodSway vs G2 쓰러짐 — 같은 나무 회전 3원 경합**: 우선순위 계약 신설 **쓰러짐(G2) > 채널 스웨이 > E2 스윙** — E2 쿼리에서 스웨이 중 노드(HerbPickup이 아닌 ResourceNode 대상은 원래 제외지만 풀 노드가 노드+채집대상 겹침) 재발화 가드 확장

## 충돌·계약 자가감사 (핵심)

- **73-E2 FxSwayRunner vs ResourceNode.ApplyWoodSway vs G2 쓰러짐 — 같은 나무 회전 3원 경합**: 우선순위 계약 신설 **쓰러짐(G2) > 채널 스웨이 > E2 스윙** — E2 쿼리에서 스웨이 중 노드(HerbPickup이 아닌 ResourceNode 대상은 원래 제외지만 풀 노드가 노드+채집대상 겹침) 재발화 가드 확장
- **G1 링은 X2 예산 공유** — 리듬 타격 인터벳(기본 2채널/타격 주기)과 쿨다운 명시, 스트레스 배치에서 예산 침범 금지 테스트 포함
- **E3 RainSplash 재사용 금지** — 낚시 물튀김은 Rain 게이트 없는 별도 스폰 경로(파라미터 확장 아님 — 단일 재생 경로 계약 유지)
- F1 장비 비주얼은 장비덩치(equipment-bulk-fitting) 계약과 접촉 가능 — 손 GLB 장착 경로 확인 후 착지
- 기존 소리 프리픽스 매칭·농경 8상태·낚시 UTK 패널(163:4) 계약 전부 무손상

## 파일 (예상)

- 신규: `Assets/Scripts/Systems/FishingLineVisual.cs` (F1~F3 — FishingSystem 훅 위임) / `Assets/Scripts/Systems/FellTreeSequence.cs` (G2)
- 수정: `Assets/Scripts/Systems/FishingSystem.cs` (F계 훅), `ResourceNode.cs` (G1·G2 훅), `GatherFeedbackSystem.cs` (G3 강화 파라미터), `FarmPlot.cs`/`HerbPickup.cs` (H1 훅) — 최소 diff
- 신규 테스트: `FxActionInterfaceP74Tests.cs` — 소스 계약(마커·게이트·예산 공유·우선순위 가드·결정론)

## 제안 착지 순서 (체감 ROI)

**P1 F1 낚시 라인** → **P2 F2 릴 시즌** → **P3 G2 벌목 쓰러짐** → **P4 G1 링+G3 파편** → **P5 F3+H1**

---
**QA 게이트**: 컴파일 error CS 0 + 전수 EditMode baseline(qa_73: 2532/2522/10) 실패집합 1:1 + P74 테스트 GREEN. 배치 background+폴링.
