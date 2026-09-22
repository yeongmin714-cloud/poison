# 🎬 보행 자연스러움 2차 튜닝 계획 (P-ANIM2, 2026-09-22)

> **기반**: 애니메이션 테스트 4.mp4 프레임 분석 — 글리치(꺾임/뒤틀림)는 소멸, 이제 **경직/기계적**이 남음.
> **상태**: 계획 확정 — "진행" 시 Phase 순차 실행

## 진단 (영상 분석 종합)
1. **체중이동 없음** — 골반 회전/좌우 흔들림 0 → 다리가 앞뒤로만 움직이는 뻣뻣한 보행
2. **발 미끄러짐 가능성** — 보폭과 이동속도 미스매치
3. **2차 모션 부재** — 척추 S자 물결 약함/상체 통째 이동, 머리 정면 고정(생동감 0)
4. **접지감 부재** — 그림자 없음 → 부유감
5. **테스트 방해** — 근접 시 TextMesh 라벨이 화면 가림

## Phase A — 체중이동/보행 코어 (가장 큰 체감)
- **골반 흔들림**: 루트/골반 본에 y 진폭(sin, 보행 주기 2배) + 좌우 롤 소폭 — 체중이동 흉내
- **대각 교차 검증**: gait 위상이 LF+RH / RF+LH 교차인지 로그+프레임 확인
- **스트라이드-속도 동기**: phaseSpeed = speed/stepLength 기존식 점검, 종별 stepLength 재튜닝 → 발 미끄러짐 제거
- 완료 기준: 걷는 척이 아닌 "디디는" 보행

## Phase B — 2차 모션 (생동감)
- **머리 바운스**: HeadLook을 진행방향 고정 + 보행 주기 미세 상하 진동으로 변경
- **체간 롤**: 상체 roll sin 소폭(골반과 역위상) — S자 물결 강화
- **NeckStabilization 확인**: QuadrupedProceduralLocomotion.ApplyNeckStabilization 작동 여부 점검

## Phase C — 접지감
- **블롭 섀도우**: 플레이어용 BlobShadow 컴포넌트 쇼케이스 캐릭터 전원 부착(부유감 제거, 가장 싸고 효과 큼)
- **발 스윙 굽힘**: 스윙 중 발 끝 살짝 들기(Toe-off 흉내) — 선택

## Phase D — 테스트 방해 제거
- TextMesh 라벨: 카메라와의 거리 < N일 때 축소/페이드 (근접 시 화면 가림 방지)

## Phase E — 검증 (사용자 확인 로그 목록)
| 로그 | 의미 |
|:--|:--|
| `[ProceduralBoneUtility] 토폴로지 매핑: ... spine=bone_N, head=bone_M` | 매핑 성공(spine/head=없음이면 증상 보고) |
| `[ShowcaseMonitor] ⚠️ 폴백 호흡 구동` | **뜨면 안 됨** — 뜨면 해당 몬스터 절차 애니 죽음(몬스터 이름과 함께 보고) |
| `[QuadrupedLocomotion] Gait changed: ...` | 보행 등급 전환 정상 작동 중 |
| `[HumanoidClipDriver] UV 미스매치` | 영주 틴트 폴백 정상 동작 확인용 |
| `[ShowcaseCameraZoom] 휠 입력 수신 (...)` | 카메라 줌 채널 동작 확인 |
| 에디터 Stats 창 FPS | 렉 여부(렉이 있으면 애니 끊김은 성능 문제) |

## 진행 규칙
- Phase별 커밋 · CS=0 + EditMode · QAPROGRESS 기록 · 완료 후 텔레그램
- Phase A/B는 메인 씬 몬스터에도 영향 — 컴파일+회귀 점검 포함
