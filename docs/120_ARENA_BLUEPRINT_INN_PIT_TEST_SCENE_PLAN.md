# 120차 계획 — 아레나 설계도 재구축(로비/투기장) + 아레나 테스트씬

## 발단
사용자가 Screenshots/에 아레나 탑다운 설계도 2장 제공:
- **아레나 실내.png** = "The Rusty Dragon Inn" — 여관형 베팅 로비 평면도 (무대/개인 부스/창고/계단/주방/바/중앙 벽난로/탁자석/정문, 가로 1860 × 세로 2500 단위)
- **아레나 외부.png** = "COLOSSEUM LUDUS MAGNUS: ARENA FLOOR" — 타원형 투기장 바닥 설계도 (모래 경기장 + 6게이트 + 글래디에이터 배치구역 + 장애물 범례, 가로 1860 × 세로 1950 단위)
요구: 설계도대로 각 실내(로비/투기장) 재구축 + 아레나 테스트씬 신설.

## 스케일 계약
- 설계도 단위 → 미터: **100 단위 = 1m (authored)** → 성내부빌더 규약 ×2.45 = 최종 좌표.
- 로비: 18.6×25.0 authored → **최종 45.6(x) × 61.3(z)**, 높이 8m (기존 실내 표준).
- 투기장: 외곽 타원 18.6×19.5 authored → **최종 외곽 타원 45.6×47.8** (반축 22.8×23.9), 모래 타원 ≈ 반축 17.8×18.6, 게이트 폭 ≈ 5.4m, 배치원 r≈4.9m.
- 구현은 **최종 좌표 직접 배치**(2-패스 스케일 없음 — 상수에 ×2.45 반영).

## P1 — ArenaInteriorBuilder 재설계 (Rusty Dragon Inn)
- 파일: Assets/Scripts/Systems/ArenaInteriorBuilder.cs (단독 소유)
- 유지 계약: room.name="ArenaRoom", ArenaBettingDesk/ArenaNpc/ArenaPit_Trigger/ArenaSouthEntry_Left·Right 이름, GLB 규약(arena_<nation>.glb), 로그 마커 "[ArenaInteriorBuilder]", 품질(캡≥5, 마모 6장, 블롭 총 12 — 배치는 신설계에 맞게), **천장 없음**(탑다운 원칙).
- 신설계(설계도 구역, 최종 좌표): 목재 판자 바닥 + 돌벽·목재 기둥 + 벽 횃불
  1. **STAGE(북서)** — 반원형 알코브 돌출 + 무대 판자 + 악기 장식
  2. **PRIVATE BOOTHS(북벽)** — 2인용 부스 탁자+벤치+격벽 일렬
  3. **STORAGE(북동)** —.enclosed 방(벽+문) + 오크통 적립
  4. **STAIRS(북동)** — 직선 나무 계단(창고 옆 동벽)
  5. **KITCHEN(남서)** — 조리대/개수대/화로/통
  6. **BAR(남중앙)** — L자 카운터 + 뒤 선반(베팅 데스크/ArenaNpc를 이쪽에 재배치 — ArenaMenu 흐름 유지)
  7. **FIREPLACE(중앙)** — 원형 돌 굴뚝(r≈2.7 최종) + 주황 러그
  8. **탁자석(남동 홀)** — 원형 2 + 사각 3 탁자 + 의자
  9. **MAIN ENTRANCE(남벽 동측)** — 개구부(ArenaSouthEntry_* 이름 유지) + 외투걸이
  10. 투기장 출입문(ArenaPit_Trigger) — 북벽 중앙 유지

## P2 — ArenaPitInteriorBuilder 재설계 (Colosseum Ludus Magnus)
- 파일: Assets/Scripts/Systems/ArenaPitInteriorBuilder.cs (단독 소유)
- 유지 계약: room.name="ArenaPitRoom", ArenaPit_Stage(모래 타원 바닥으로 용도 전환)/ArenaPit_ExitDoor(+BuildingTrigger "Exit"), GLB 규약(arena_pit_<nation>.glb), 로그 마커, 품질(캡≥4 — 벽 세그먼트 캡, 마모 4+4), **오픈에어 — 천장/돔 없음**.
- 신설계: **타원형** — CreateRoom(사각) 폐기, 절차 타원 구축
  1. 모래 바닥 = 스케일드 Cylinder(반축 17.8×18.6, y≈0.02) + MeshCollider(보행) — name "ArenaPit_Stage"
  2. 외곽 벽 링 = 타원 따라 48 세그먼트 박스(높이 8m) 각 MeshCollider + 상단 캡
  3. **6게이트**: 서/동 대형 + 북×2 + 남×2 — 게이트 각도 세그먼트 생략 + 아치(기둥2+인방)
  4. 기둥: 외벽 내면 12~16개
  5. **배치구역**: 서/동 점선 원(r≈4.9) — 평면 링 데칼(비충돌)
  6. 장애물: 바구니/단지/돌 5~8개(작은 콜라이더)
  7. 중앙 마커: 출발선·결투 원(평면 링)
  8. 모래 텍스처: IndoorTextureGenerator 절차 생성(모래색 0.76,0.68,0.52 톤) 또는 기존 gen 틴트
  9. ExitDoor — 남측 중앙 게이트(기존 남벽 유지 관례)

## 부모 작업
1. Test_18_Arena.unity 신설 — InteriorSystemsTest.unity(3.5KB, 카메라+라이트만) 복사(메타 생성은 Unity 위임 — 씬 이름으로 부트스트랩)
2. ArenaTestSetup.cs 신설 — InteriorSystemsTestSetup 패턴([RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + 씬 이름 게이트): 지면+UI+카메라+플레이어+Arena 트리거( Western)+안내 로그
3. 테스트 갱신: ArenaInteriorTests/ArenaQualityLayersTests — 위치 의존 어서션(블롭 이름 등) 신설계 좌표로
4. 컴파일 + EditMode 전수 + QAPROGRESS/메모리

## 검증
- 컴파일 error CS 0 / EditMode baseline 11건 1:1 + 비baseline 0
- Play(별도 승인): Test_18 진입 → E키 로비(설계도 구역 가시) → 투기장 문 → 타원 투기장(게이트 6/배치원/장애물) → Exit 복귀
