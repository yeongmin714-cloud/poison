# 계획서: VFX 잔여 마무리 (무번호 30 — D-4 디졸브 + S4 선택 링 지속 펄스)

**선행**: 무번호 29(D/M2/S2/P2) 전량 착지(qa42 2345/2335/10) — D-4의 착지 조건("D-1~3 착지 후 판단") 충족.
**범위 제외(보류 유지)**: V3 수차·VFX Graph(결정론/프로브 재현성 리스크), S1-4 지면 틴트(지형 타입 조회 API 부재 — 전수 재검증 0건), P4 화이트 프레임(Play 판정 항목).

---

## 1. D-4 디졸브 — 사망/소멸 연출

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| D-4a 디졸브 셰이더 | 절차 해시 노이즈(텍스처 불필요 — 셰이더 내 결정론) + 임계 밴드 엣지 하이라이트, `_DissolveAmount` 0→1 램프 | 신규 Assets/Shaders/FxDissolve.shader | URP Transparent, SRP Batcher(CBuffer), Random 0건 |
| D-4b 사망 디졸브 훅 | StartDeathSequence(눕힘+sink 창 0.6~1.2s)에 바디 렌더러 디졸브 램프(≈0.9s) — 머티리얼 인스턴스 교체(_BaseMap/_BaseColor 승계) 후 파괴(누출 금지) | AnimalAI + 신규 FxDissolveUtility.cs | 기존 눕힘/sink/파괴 시퀀스 무손상, EA2 유한 수명 계약 유지, 셰이더 부재=스킵 폴백(기존 사망 연출만), EditMode 가드 |

## 2. S4 선택 링 지속 펄스

| 항목 | 내용 | 파일 | 계약 주의 |
|---|---|---|---|
| S4 선택 호흡 | 선택 유지 중 링 머티리얼 인스턴스의 `_TeamColor` 알파를 SpawnPulse로 미세 호흡(0.82~1.0×) — 19-C1 1회성 선택 펄스와 별개 지속 계층 | SelectionRingController | 84차 잠금(5파일 AreEqual) 무손상 — ①생성 상수/프로퍼티 무수정 ②인스턴스 전용 기록 ③비활성 시 원복 ④기존 19-C1 블록 무수정 |

## 3. 검증
1. 계약테스트(FxDissolveS4Tests 6케이스) + rg 사전검사
2. 컴파일 error CS 0 → EditMode 전수 + baseline 10건 1:1
3. Play 통합 판정(사용자): 디졸브 연출 + 선택 링 호흡 체감 — D-4 실패 시 셰이더 로그 확인

## 4. 순서
D-4a → D-4b → S4 → QA. 커밋은 별도 승인 대기.
