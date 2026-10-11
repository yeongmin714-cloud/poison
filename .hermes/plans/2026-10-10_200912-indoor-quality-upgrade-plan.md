# 실내씬 고품질화 계획 (Indoor Quality Upgrade Plan)

- **작성:** 2026-10-10 20:09
- **상태:** 계획만 — 실행은 사용자 승인 후("진행" 지시 시 Phase별 개별 진행)
- **스코프:** IndoorScene(아레나 로비/투기장) + 플레이어 성 내부 공통 품질 상승. 실내씬 예시.png(하프팀버·웜 로우키)가 무드 기준.

## 목표

현재 실내(회반죽 벽+팀버 골조+석재 바닥+AERO 포그+웜 포인트라이트)를 예시 이미지 수준의
"어둡고 따뜻한 중세 실내"로 끌어올린다. GPU 제약은 2026-10-09 사용자 확정(별도 데스크톱 실행)으로
해제됐으므로 품질 우선, 단 인텔 HD 530 백업 환경을 해치지 않는 보수적 기본값을 유지한다.

## 현재 상태 (실측 — 2026-10-10)

| 항목 | 현황 |
|---|---|
| 벽 | wall_plaster_upper 전면 + D3 팀버(세로 기둥 0.40+상단 보) — 노멀맵 없음 |
| 바닥 | floor_flagstone 1024²(1장=2.4m, 오늘 신규 교체) — 노멀맵 파일 존재(로드됨) |
| 하부 석재 | D1에서 폐지(예시는 "하단 석재 소량 노출" — 미반영) |
| 짚 데칼 | `floor_straw_decal.png` 로드만 되고 **미사용**(적용기 없음) |
| 조명 | 앰비언트 0.06(완전 암전)·화광 0.62×3·랜턴/작업등 — 플리커는 균등 사인(0.8~1.2) |
| 포스트 프로세싱 | **없음** (IndoorScene Volume 0건 — Bloom/Vignette/Color Grading 미적용) |
| 렌더러 | SSAO feature 부착 확인 + AERO Volumetric Fog(0.2 흰색) — MSAA 4 |

## Phase F1 — 텍스처 세트 고도화 (PNG 투입 대기형 + 코드 소량)

1. **노멀맵 확장 경로**: `wall_plaster_upper_n.png` / `wood_frame_n.png` 선택 로드 추가
   - 파일: `Assets/Scripts/Core/IndoorTextureLoader.cs`(+`IndoorMaterialFactory.cs` Apply 경로)
   - 파일 부재 시 자동 스킵(기존 floor_flagstone_n 선례) — **사용자 PNG 투입 즉시 반영**
2. **하부 석재 밴드 복귀 옵션**: `IndoorTextureLoader.IncludeWallStoneLower`(기본 false)
   - 벽 하부 ~1.1m에 wall_stone_lower 재적용(예시의 하단 석재 노출) — true 시 캡처 비교
3. **바닥 신규 텍스처 검증**: 오늘 교체한 floor_flagstone의 타일 체감 확인
   - 패턴이 크게/작게 느껴지면 `FloorCoverMeters`(2.4) 1줄 튜닝
4. **러프니스 튜닝**: 바닥 0.55/wood 0.18 현값 → 캡처 기반 미세 조정
5. **짚 데칼 적용기 신설**: 미사용 `floor_straw_decal.png`를 엔트리홀/부엌 바닥에 짚 매트 데칼
   (렌더러 전용 쿼드 — 무충돌, 예시의 짚단 포인트)
- **게이트**: 컴파일 error CS 0 + IndoorAeroFog/토폴로지 focused + 캡처 승인
- **사용자 투입 파일(선택)**: `wall_plaster_upper_n.png` 1024×512 / `wood_frame_n.png` 512×1024 / (하부 석재 복귀 시 별도 승인)

## Phase F2 — 조명 고도화 (암전 무드 완성)

1. **화광 라이트 쿠키**: 절차 생성 스팟 쿠키(불꽃 실루엣 블롭) — 화광 3개에 적용
   - 파일: `Assets/Scripts/Systems/IndoorLighting.cs`(쿠키 팩토리) + IndoorScene 라이트 배선
2. **플리커 고도화**: 균등 사인(0.8~1.2) → 다중 사인+노이즈 화재 플리커, 광원별 위상차
   - 파일: `IndoorLighting.cs`(FLICKER_* 상수 계열)
3. **Emissive 화로 코어**: 물리 브레이저 금지 계약 유지 — **렌더러 전용(무콜라이더) emissive 숯 메시**
   (입구 화광 아래 바닥면, HDR 색 ~1.5 — Bloom(F4)과 연동 시 은은한 잉글루)
4. **광원 균형 미세조정**: 암전(0.06) 기준 풀 강도/범위 캡처 피드백 튜닝
- **게이트**: 컴파일 0 + CollisionAndKitchen(입구 ≤0.5 계약) + 캡처 승인

## Phase F3 — 천장·소품 지오메트리

1. **천장 목재 들보**: 상단 보와 T자로 연결되는 천장 들보(병합 메시, 방 전체 격자) — 하프팀버 원칙 유지(**대각 없음**)
   - 파일: `PlayerCastleInteriorBuilder.cs`(AddTimberWallFrame 패턴 연장)
2. **벽걸이 깃발 곡면**: 현재 평면 BannerCloth → 옅은 S커브 정적 버텍스 웨이브(베이크, 애니 없음)
3. **촛대 소품**: 렌더러 전용 소형 촛대+Emissive 불꽃 점(F2 연동) — 책상/선반 위
- **게이트**: 컴파일 0 + 토폴로지/충돌 focused + 캡처 승인
- **오픈 퀘스천**: 창문+외부 광 스트림은 실내 셸 특성상 창밖 노출(하늘/스카이박스) 문제 — 원하면 "틈새 빛 슬릿+커튼" 형태로 별도 논의

## Phase F4 — 포스트 프로세싱 (실내 전용 Volume 게이트)

1. **Global Volume + Profile 신설**: `Assets/Settings/IndoorVolumeProfile.asset`
   - **Bloom**(화광 글로우 — threshold ~0.9, intensity 보수적 0.2~0.35)
   - **Vignette**(암전 몰입 — intensity ~0.35)
   - **Color Adjustments**(웜 틴트 미세 + 콘트라스트 소폭)
2. **게이트 배선**: `IndoorAeroFogController`와 동일 게이트(활성 씬==IndoorScene/Interior) — 실내 진입 시 Volume weight 1, 퇴장 시 0
   - 파일: `Assets/Scripts/Systems/IndoorAeroFogController.cs` 확장(이름 유지 or IndoorPostProcessController 분리)
3. **성능 보수 기준**: Bloom은 FullScreen pass 1회 추가 — AERO와 중복 시 카메라 스택 단일 확인
- **게이트**: 컴파일 0 + IndoorAeroFogTests 계약 갱신 + 캡처 승인
- **위험**: 톤이 예시보다 화사해지지 않게 intensity 상한 계약 테스트 신설

## Phase F5 — 가구 GLB 규약 확장 (선택 — 사용자 에셋 준비 시)

- 아레나 선례 패턴: `Assets/Resources/Models/UserProvided/castle_<slot>.glb` — `RuntimeModelLoader.TryGetModel`로 자동 로드, 없으면 현재 절차 박스 폴백
- 후보 우선순위: 침대(20개 배럭) → 보관함/선반 → 작업대 (다중 인스턴스 비용 고려 — 침대가 체감 최대)
- **오픈 퀘스천**: 어떤 가구부터 GLB화할지 + GLB 규약 파일명 확정은 사용자와 협의

## 실행 순서 제안

F1(텍스처) → F4(포스트) → F2(조명) → F3(지오메트리) → F5(GLB)
- 근거: F1+F4가 노력 대비 체감 최대(질감+무드), F2/F3는 지오메트리 리스크가 커서 후순위.
- 전 Phase 공통 게이트: fresh compile `error CS` 0 + 관련 focused 테스트 + **사용자 캡처 승인**. 커밋은 Phase 단위, push는 지시 시.

## 리스크/트레이드오프

- **VRAM**: 텍스처 세트 증가(노멀 2종) — 별도 데스크톱 실행(10-09 승인) 전제, 1024급 유지로 흡수.
- **톤 이탈**: Bloom/Color Grading이 예시보다 밝아 보이면 계약 상한 테스트로 제동.
- **무충돌 계약**: emissive/데칼/들보 전부 렌더러 전용 — CollisionAndKitchenTests 계약 유지.
- **암전과 포스트 상호작용**: Vignette+암전이 과하게 어두워지면 Vignette intensity 하향(캡처 조정).

## 검증 방법 (Phase 공통)

- `Unity.exe -batchmode -quit -projectPath C:\Unity\code -logFile TestOutput/<phase>_compile.log` → `error CS` 0
- focused EditMode(XML `TestOutput/<phase>_focused.xml`) → 신규 회귀 0(기존 baseline 7건 불변)
- Play 재실행 캡처 → 사용자 승인(예시 이미지와 나란히 비교)
