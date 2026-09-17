# 🏹 화살 액션 V2 계획 (테스트 5 실측 기반 — 2026-09-17)

> **입력**: 화살 테스트 5 영상 프레임 시트 실측 + 사용자 리포트("여전히 화살이 이상함" / "선택 오라를 earth trail VFX로").
> **성격**: 실행 전 계획서 — 승인("진행") 후 Phase A→E 순차 실행.

---

## 📐 뿌리 진단 (테스트 5 프레임 시트 실측)

| # | 증상 | 뿌리 추정 |
|:--|:-----|:----------|
| 1 | **화살 모델이 진행 방향과 어긋남**(대각선 비행 중 화살이 수직으로 서 있음) | 스폰 시 LookRotation 회전은 1회 설정 — 그 후 **Rigidbody 회전이 물리에 의해 풀림**(freezeRotation 없음) + 플레이어 콜라이더와 스폰 겹침 충돌로 텀블링 |
| 2 | 비행은 1.2s로 정상(사거리 개선 확인) | — |
| 3 | **트레일 끊김**(일정하지 않게 짧아짐) | 프레임 드랍 시 TrailRenderer 정점 간격 벌어짐(minVertexDistance 0.08·rb interpolation 미설정) |
| 4 | **명중 스틱이 이펙트에 가려짐** | PlayHitFX 풀 이펙트가 스틱 지점 커버 + 스틱 후 떨어짐 전환이 거칠음 |
| 5 | 드로 중 발사 예비감 부족 | 활 시위/장전 화살 시각화 없음 + 조준 프리뷰 없음 |
| 6 | 선택 표시 품질 | 보유 자산 `Vefects/Trails VFX URP/VFX_Trail_Earth`(TrailRenderer 리본×2 + Distortion — 클래스 96·스크립트 0·URP 재질 3종 실측) — **이동 중 잔상을 그리는 trail형이라 선택 병사 이동 표시에 적합** |

---

## Phase A — 화살 정렬/물리 안정화 (뿌리 수리) ⭐ 최우선
| # | 작업 | 내용 |
|:--|:-----|:-----|
| A1 | 회전 고정 | Spawn에서 `rb.freezeRotation = true` + `rb.interpolation = Interpolate` — 발사 방향 고정, 텀블링 방지 |
| A2 | 매 프레임 진행방향 정렬 | Update에서 `rb.rotation = LookRotation(velocity)`(속도 접선 정렬 — 아크 중에도 화살이 항상 진행 방향) |
| A3 | 스폰 겹침 제거 | 스폰 콜라이더에 플레이어 무시(발사 후 0.1s 무적 or 레이어 오버라이드) — 콜라이더 겹침 충돌로 튕기는 것 방지 |
| A4 | 트레일 끊김 | `minVertexDistance 0.08→0.05` + rb interpolation — 프레임 드랍 시에도 연속 궤적 |

## Phase B — 명중 연출 정리
| # | 작업 | 내용 |
|:--|:-----|:-----|
| B1 | 화살 전용 명중 이펙트 축소 | PlayHitFX 화살 경로에서 이펙트 스케일 축소(스틱 가림 해소) — 근접 풀 이펙트 유지 |
| B2 | 스틱→낙하 전환 부드럽게 | 스틱 중 kinematic 유지 → 낙하 시 물리 전환 + 0.5s 페이드(툭 끊김 제거) |

## Phase C — 화살 액션감 업그레이드
| # | 작업 | 내용 |
|:--|:-----|:-----|
| C1 | 발사 카메라 킥 | 드로 릴리즈 시 소형 카메라 킥(CombatCameraEffects 재사용 — 근접 규약) |
| C2 | 명중 히트스톱 | 명중 순간 0.05s 타임스케일(근접 명중과 동일 규약) |
| C3 | 명중 데미지 숫자 | 화살 경로에도 ShowDamageNumber(활 타입 골드색) |
| C4 | 드로 중 시위+장전 화살 시각화 | 활 시위 라인 렌더(드로 파워에 따라 당겨짐) + 드로 중 화살 프리뷰를 활에 부착 |
| C5 | 조준 프리뷰 | 드로 중 얇은 사거리 궤적 프리뷰(파워에 따라 길이 변화) |
| C6 | 파워 풀 보너스 | 파워 풀(1.0) 명중 시 슬로모션 0.1s(기존 근접 슬로모션 재사용) |

## Phase D — 선택 표시: Earth Trail 전환
| # | 작업 | 내용 |
|:--|:-----|:-----|
| D1 | 자산 복사 | `VFX_Trail_Earth.prefab`(+meta) → `Resources/FX/Selection/EarthTrail.prefab` — TrailRenderer 리본×2+Distortion(스크립트 0, URP 재질 GUID 참조 유지 → 안전 복사) |
| D2 | GuardSelectionManager 전환 | Buff/MagicCircle2 → EarthTrail 부착 방식으로 변경 — **TrailRenderer는 이동 시 잔상을 그리므로 선택 병사 "이동 트레일" 표시**(정지 시 페이드 — 자연스러움), 부모화 후 이동 추종 |
| D3 | 후보 관리 | Earth가 마음에 안 들면 동일 팩의 Cosmos/Fire/Ice/Lightning 등 9종 즉시 교체 가능(같은 구조) |

## Phase E — 검증
- 배치컴파일 error CS=0 + 서브 QA(변경 파일 diff 전수)
- 화살 테스트 6 영상 판정: ①화살이 진행 방향 정렬(아크 접선) ②트레일 끊김 0 ③스틱 가시화 ④발사 킥/히트스톱 체감 ⑤선택 병사 이동 시 earth trail ⑥기존 회귀 없음

---

## 📌 실행 순서 및 예상
- **순서**: A(뿌리) → B → C → D → E — Phase A만으로 "이상함"의 대부분 해소, C는 체감 액션감, D는 표시 전환.
- **파일**: ArrowProjectile.cs(중심), PlayerCombat.cs(C1/C3/C5), ArrowManager.cs(C1 일부), GuardSelectionManager.cs(D2), 자산 복사(D1).
- **리스크**: C4(시위 시각화)는 활 GLB 구조 확인 필요 — 문제 시 C5만 선적용.

**승인 후("진행") Phase A→E 순차 실행합니다.**
