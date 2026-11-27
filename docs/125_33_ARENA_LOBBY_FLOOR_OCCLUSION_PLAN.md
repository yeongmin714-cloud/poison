# [125차 후속33 계획] 로비 바닥 미변경 원인 수리 — 스테이징 Ground가 실내 바닥을 덮는 가림(텍스처 배선은 정상)

> 상태: **계획 수립 — 구현은 사용자 승인 후.** 스크린샷(아레나 실내2) 판독 + Editor.log + 소스 실측 기반.

---

## 진단 결과 (수치/증거)

| # | 확인 항목 | 실측 | 판정 |
|---|---|---|---|
| 1 | tavern 텍스처 로드 | Editor.log: `tavern[floor=True floorN=True plaster=True plasterN=True timber=True timberN=True]` | **로드 정상** |
| 2 | 주점 머티리얼 적용 | Editor.log: `[125차 후속32] 주점 머티리얼 적용 완료 — Floor(오크 판자)+Wall(회반죽 투톤)` | **적용 실행 정상** |
| 3 | 텍스처 파일 자체 | tavern_floor_plank.png 판독 — 다크 브라운 오크 판자(옹이/결/못자국), 심리스 정합 | **파일 정상 — 주황색 아님** |
| 4 | 스크린샷 판독 | 로비 맞음(가이드 "진입 즉시 로비(여관)") — 바닥=**단색 주황/갈색, 판자 결 0**, 벽/가구 단색 | 바닥만 이상 |
| 5 | **가림원 — 스테이징 Ground** | ArenaTestSetup.SetupGround: `Ground` Plane 60×60m, color (0.35,0.32,0.28) — **파괴/비활성 코드 전무**(IndoorSceneTransition Ground 참조 0건) → 로비 전환 후에도 원점(y=0)에 생존 | **원인 후보 확정** |
| 6 | 높이 경합 | IndoorBuilder.CreateRoom — Floor **y=0 정확히**(L30 주석 "바닥(XZ 평면, y=0)") vs Ground y=0 — **동평면 경합(z-fight/뎊스 동률)**, Ground가 60×60으로 룸 전체를 덮음 | 메커니즘 정합 |
| 7 | 색상 정합 | Ground (0.35,0.32,0.28) 갈색-회색 + HQ 웜 포인트라이트(촛불광) 조사 → **주황색처럼 보임** — 스크린샷 단색 주황과 정합 | 색상 근거 정합 |
| 8 | 교차 검증 — 투기장은 정상 | 외부 투기장 모래 톱 y=0.15 > Ground y=0 → 모래가 Ground 위 → 영상14에서 투기장 지형 정상 가시 실측 | 같은 Ground가 투기장은 안 가림 — 로비(Floor y=0)만 가림 = **가설 강화** |
| 9 | 왜 이제야 눈에 보였나 | 로비 바닥은 후속24 B1 이전부터 Ground에 가려져 있었음(B1은 로그만 실측 — 시각 미검증) | 배선 이전부터 존재한 가림 결함 |

**결론: tavern 배선(로더/팩토리/분기)은 전부 정상 동작. 로비 바닥은 적용됐으나 스테이징 Ground(60×60, y=0)가 동평면에서 덮어 가림 — 텍스처 문제가 아니라 테스트씬 인프라의 가림 결함.**

---

## 수리 항목 (판정 기준 사전 정의)

### N1. 스테이징 전용 리소스(Ground+StagingDirectionalLight) 라이프사이클 정리 — 근본 수리
- 소유 원칙: Ground/StagingDirectionalLight는 ArenaTestSetup이 만든 **스테이징 전용** 리소스 — 실내(로비/투기장)에서는 꺼져야 한다.
- 구현: ArenaTestRunner.Update에 룸 상태 폴링(0.5s 간격, 캐시) — `ArenaRoom` 또는 `ArenaPitRoom` 존재 → Ground+StagingDirectionalLight `SetActive(false)` / 둘 다 부재(스테이징 복귀) → `SetActive(true)`.
  - 출구 E키로 스테이징 복귀 시 Ground 자동 복원(회귀 방지 — 기존 복귀 흐름 무변경).
  - 폴링은 테스트씬 전용 저비용(Find 0.5s 간격) — 프로덕션 경로 무영향.
- StagingDirectionalLight(intensity 1.0, 50°)도 같이 끈다 — 실내 촛불 웜광 분위기 보존(주점 톤 훼손 방지 — 동일 소유 리소스).
- **판정**: 재Play 로비에서 오크 판자 바닥 가시 + 스테이징 복귀 시 Ground 재가시.

### N2. (기각 대안 — 기록용) 실내 Floor y 오프셋
- IndoorBuilder.CreateRoom Floor y=0→+0.02로 모든 실내 바닥을 Ground 위로 — **기각**: 성 내부 등 검증 완료 씬 전체에 영향(가구/접지 2cm 시각 변화) — 범위 과다. N1(테스트씬 소유 정리)이 수술적.

### N3. 예방 점검 — 동일 유형 씬
- 실내 빌드+원점 Ground 병존 가능 테스트씬(Test_PlayerCastleInterior 등)에서 동일 가림 여부 확인 — 이번 재Play 로그/다음 Play에서 확인(별도 수리는 증상 확인 시).

---

## 검증 절차 (재Play 1회)
1. 로비: 바닥=다크 오크 판자(판자 결/옹이 가시, 2.4m 타일링) + 벽 회반죽 투톤 + 촛불 웜광 분위기
2. 스테이징 복귀(출구 E키): Ground 재가시(갈색 지면) — 회귀 없음
3. 투기장: 기존과 동일(모래/벽 정상)
4. 성 내부: 기존 HQ 무변화
5. 로그: 예외 0 / baseline 1:1

## 비고
- tavern 배선(후속32) 코드는 무결 — 수리 대상 아님.
- 전체 워킹트리 미커밋 유지. Play/커밋은 별도 승인 게이트.
