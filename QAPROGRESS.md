# ✅ 포이즌 (Poison) — QA 진행 상황 (런타임 오류 점검)

> **목표:** 431개 스크립트를 하나씩 점검하며 런타임 오류를 잡아냅니다.
>
> **진행 방식:** 테스트 씬별로 시스템 격리 → Play 테스트 → 오류 발견 → 수정 → 기록
>
> **최종 갱신:** 2026-09-06

---

## 2026-09-06: 애니 10차 — 컨트롤러 모션 참조 전량 깨짐 교체 ✅ (판정 대기)

**DD4 결정 증거:** `state=Walk/Run`인데 `clip=NONE` — 상태 전환·normT 진행은 되는데 **재생 클립이 아예 없음**(LFArmRotΔ도 회전 오염분 제거 시 0.00°). 컨트롤러 파일 검증: 4개 컨트롤러(Player+병사3)의 **모든 m_Motion이 `fileID: -203655887218126122, type: 3`** — .anim 네이티브 참조는 `fileID: 7400000, type: 2`여야 함. guid는 전부 올바른데 참조 형식만 깨짐 → 추정: 09-03 팩 교체 때 YAML guid 문자열만 교체하고 구 참조 형식 잔존. **09-03 이후 모든 애니 테스트가 빈 상태로 재생되고 있었음** (8차 메타·9차 위계는 실존 버그였으나 이것이 최후 고리).

**수리:** Tools > Anim > Build Mixamo Controllers 재실행 → **Player_AC_AC 중복 생성 재발**(구 컨트롤러 삭제 실패, 09-04a 수정에도) → 파일 레벨 교체: 구 Player_AC 삭제, 검증된 Player_AC_AC(7400000/type2 ×7 + FBX 서브에셋 type3 ×2)를 Player_AC로 승격. 병사 3개는 정상 재빌드 확인. (커밋 3cbf372b)

**판정 대기 (Play):** DD4 `clip=OneHand_Up_Walk_B(human=True)` + `LULegRotΔ>10°`.

---

## 2026-09-06: 지형 구멍 수리 — 와인딩 삼각형 단위 전수 반전 ✅ (판정 대기)

**증상:** 일정 지형에서 점프/구르기 실패 — JumpProbe `grounded=False` + DiagP1 "전방지면 아래에 콜라이더 없음" 16건.

**원인:** Phase B 재표본 후 와인딩 반전을 **첫 삼각형 1개 법선만** 검사 → 원본 메시 와인딩 혼재로 아래향 삼각형 일부 잔존 → 백페이스(레이캐스트 미히트+렌더링 컬링) → 지형 구멍=허공 → 그 구역 grounded=False.

**수리:** 삼각형 단위 전수 검사로 교체(각 삼각형 Dot(n,up)<0만 개별 반전, 이중반전 방지) + 전방 5지점 probe 요약 로그 추가(구멍 분포 가시화). 정점 재표본·콜라이더 재베이크·SyncTransforms 유지. (code agent, 중괄호 144/144 검증)

**판정 대기 (Play):** `[DiagP1] 재표본+와인딩: flip=N/M` 로그 + 전방5지점 전부 hit + 구멍 구역에서 점프/구르기 성공.

---

## 2026-09-06: 지형 T1 — 청크 기반 런타임 지형 생성 (커버리지 확장) ✅ (판정 대기)

**측정으로 확정된 구조 문제:** 지형이 `Terrain_초원_100x100` 단 1장(2000×2000m, ±1000m, 20m/쿼드)인데 영지 링 반경은 Ring1=**1450m** → **Ring1 20개 영지 전체+Ring2 경계가 지형 밖(회색 허공)** — "지형적 문제로 애니가 안 나오는 지역"의 정체(높이 수식은 전역 계산이라 캐릭터는 허공 위 부유). 부가: 20m/쿼드 조밀도 부족으로 메시-수식 편차가 접지 판정을 지역별로 깨뜨림.

**수리 (신규 RuntimeTerrainChunkManager.cs + GameSetup 훅):** ±1600m를 400m 청크 8×8=64장으로 런타임 생성(100×100 정점/청크 = **4m/쿼드, 5배 해상도**), y=GetHeightAt+1 규약 준수, (a,c,b)/(b,c,d) +Y 위향+안전반전, UV는 Ground_Inner affine 역산으로 월드 연속(폴백 worldXZ/2000+0.5는 실제 규약과 일치 — QA 확인), 머티리얼 1회 복제 공유, 플레이어 청크 우선 코루틴(청크당 1프레임), 플레이어 청크 완료 즉시 Ground_Inner의 **Renderer+Collider만** 비활성(z-fighting/이중 물리 방지, TerrainTextureApplier 보존). 병행: DiagP1 5지점 probe를 Phase B 후로 이동(VOID×5 오판 방지), _Custom_Color 타입 불일치 SetColor 경고 스팸 수리(폴백+1회 경고).

**QA PASS:** 위향 수학/청크 경계 봉합(−1200 일치)/GetHeightAt 시그니처/UV 폴백=실규약 일치/null 가드/균형 검증. 훅이 try-catch 격리 밖이던 것을 QA가 직접 격리 수정. (커밋 7b614ec7, c86d4f9a)

**판정 대기 (Play):** ① `[TerrainChunks] 완료: 64청크` 로그 ② `Ground_Inner 렌더러/콜라이더 비활성` 로그 ③ 스폰→Ring1 방향 1450m 직진 중 회색 허공 소멸 + grounded=True 연속 + 점프/구르기 성공 ④ Ring1 영지 스크린샷 ⑤ 애니 지표 유지(clip= 팩 클립, LULegRotΔ>10°). **T1 Play 검증 완료(26일 21시대): 64청크 생성 확인(37.0초, 정점 640k).**

---

## 2026-09-06: 지형 T2/T3 — 데코 전체 확장 + 머티리얼 전파 + 월드 경계 ✅ (판정 대기)

**T2:** ① IdyllicDecoPlacer `BOUND_MAX 950→1550` — 데코(트리/바위/덤불/꽃/초지)가 청크 지형 전체(±1550m)에 배치, 캡(트리 1900/국가)이 자연 스로틀 → 총 오브젝트 ~9.9k→~14k 예상(컬링 150/200m 유지) ② RuntimeTerrainChunkManager 머티리얼을 `Object.Instantiate` 복제 → **원본 sharedMaterial 직접 공유**로 변경 — NationTerrainController의 국가별 mainTexture 교체가 64청크 전체에 전파. 원본 파괴/스왑 없음 확인(NationTerrainController 171행은 null 분기 한정).
**T3:** PlayerMovement `WorldBound=1590` + `ClampToWorldBounds()` 헬퍼 — MovePlayer/HandleRoll의 Move 직후 호출(위치 XZ만 클램프, 상태 무변경; 이후 ClampToGroundByHeight가 최종 XZ 기준 Y 정합 — 순서 정합).

**QA:** 자체 라인별 검증 완료(위 3파일), QA 에이전트 타임아웃으로 컴팩트 자체 검증으로 대체 — 머티리얼 파괴/스왑 부재·클램프 순서·균형 확인.

**판정 대기 (Play):** ① 국경(Ring2↔Ring1 방위) 넘어갈 때 청크 텍스처 변화 눈확인 ② 지형 밖(±1600m) 이동 차단(걷기로 못 나감) ③ 배치 수 로그(트리 총합 ~7k 수준) ④ 프레임 저하 없음.

---

## 2026-09-06: 애니 끊김 2차 + 지형 B1 드라마 강화 ✅ (판정 대기)

**A. 애니 끊김 2차(의도 기반 속도):** Walk↔Run 진동 31/29회 — 측정 속도가 경사에서 4.0~4.5로 떨어지며 임계(4.5/4.0)를 오감. HumanoidClipDriver를 **의도 기반**으로 교체: 이동 중 목표=IsDashing?5.0:2.5(기존 공개 프로퍼티 재사용 — 중복 정의 회피), 정지 감지는 raw+0.25초 홀드 유지, 스무딩은 목표로 수렴. 컨트롤러 YAML 무수정(2.5→Walk/5.0→Run 호환). raw는 DD 로그에 유지.

**C. 잔디 미부착 원인 확정+복구:** **IdyllicGrassCover.Configure() 호출부가 코드에 부재**(시스템만 존재, 프리팹 Resources/IdyllicPrefabs/Grass 정상) → GameSetup.Start에 try-catch 격리로 AddComponent+Configure(player) 복원. Configure만으로 자체 기동 확인(프리팹 자동 로드+부모 생성+Update 가드).

**B1. 지형 드라마 강화(예시2~13 컨셉):** 조사 결과 고급 인프라(ridged 절벽/도메인워핑/테라스/계곡/방위 크로스페이드)는 존재하나 진폭이 "구릉" 수준. ① NationParams 방위별 격차 확대: East amp13/절벽8(구릉 초원), West amp14/절벽14(협곡), South amp7/절벽4(평탄 적토), North amp16/절벽16(험준 설산), Empire amp2.5(평탄) ② **메사(대지) 레이어 신규**: 140m 셀 15% 확률, 6m 융기+급경사 가장자리(예시2 단차), cliffSuppression 곱셈으로 스폰/성/호수/경계 보호, ApplyLakeBasins가 나중이라 호수와 무충돌. 구릉 최대경사 amp×freq×2π≈31°(CC slopeLimit 45° 이하). GetHeightAt만 확장 → 64청크 자동 반영.

**판정 대기 (Play):** ① 걷기/달리기 끊김 소멸 + [State] Walk↔Run 진동 잔존 여부 ② 방위별 이동 시 지형 성격 차이(북 험준/남 평탄/서 협곡) 스크린샷 ③ 메사(평탄 대지+절벽 가장자리) 발견 여부 ④ 잔디 밀집 커버(스폰 주변) 스크린샷 ⑤ 성/영지 위치가 새 지형 위에 정상 부착되는지.

---

## 2026-09-06: 입력 신뢰성 + 방위색 고정 + 지형 밀도 상향 ✅ (판정 대기)

**A. 입력(W키 드랍) 수리:** PlayerMovement가 `Keyboard.current`를 1회 캐시 — 디바이스 재열거 시 stale 참조로 isPressed false 고정(간헐 W 드랍 → 이동 정지 → Idle 전환 = "애니 끊김"으로 보임). 수리: 캐시 제거 → 매 사용 `CurrentKeyboard` 즉시 조회 + null 시 InputSystem.devices 재열거 1회. 사용부 10곳 교체. **DD5 입력 프로브** 추가(Play 30초, WASD 에지 로그 + 요약) — W 홀드 중 드랍을 숫자로 확정.

**B. 방위색 고정 표시(북=흰색):** NationTerrainController의 `UpdateForCurrentNation`(플레이어 현재 국가 기준 결합 맵 통째로 재생성)이 방위 고정색을 파괴 → **Start에서 호출 제거**(함수 보존). 결합 텍스처를 **±1600m 1:1 매핑**으로 변경(구 _textureTiling=200 반복 타일 폐기), 북쪽 설원 백색 강화(_northTint 0.93/0.95/0.98 + 북방위 색 고정 강화). 청크 UV도 Ground_Inner 구 uv(±1000 기준) 역산 경로 폐기 → **worldXZ/3200+0.5 고정 매핑**(외곽 래핑 오색 방지). Awake ApplyNationTerrainTexture는 유지(부팅 시 결합 텍스처 적용).

**C. 밀도/다양성 상향:** 메사 셀 15%→25%(예시2 단차 증빈) / 꽃밭 커버리지 14%→22%(FLOWER_LO 0.78→0.70) / **숲 군락화**(forestMask Fbm>0.55 밀집·0.40~0.55 정상·<0.40 개활지 70% 스킵 — 숲 덩어리/개활지 분리) / 호수 배치 bound ±1000→±1500(LAKE_COUNT는 AA2에서 이미 14).

**QA:** 파일별 균형 검증(6파일 braces 0), UpdateForCurrentNation 외부 호출부 0건 확인, ApplyNationTerrainTexture(Awake) 유지 확인, UV 고정 매핑 정합 검증. QA 에이전트 1회 타임아웃 → 컴팩트 자체 검증 대체(머티리얼 파괴/스왑·호출부·스코프 직접 확인).

**판정 대기 (Play):** ① 걷기/달리기 끊김 소멸([InputProbe] 요약 + [State] 진동) ② 4방위 이동 시 방위색 고정(특히 북=흰색) ③ 숲 군락/꽃밭/메사/호수 스크린샷 ④ 성/영지/데코 신지형 부착 확인.

---

## 2026-09-06: A-2 애니 최종 + 흙길 네트워크 + 밀도 최종 상향 ✅ (판정 대기)

**A-2 애니 최종 수리:** 직전 의도 기반(걷기 2.5)이 오판이었음 — 실측 확정: **일반 이동=5.0m/s / 스프린트=15.0m/s**(2.5는 존재하지 않음) → 5m/s 이동 중 Walk 클립 1배속 = 발 미끄러짐("어색한 애니"). 수리: ① 속도 추적으로 복귀(raw 추적+0.25s 홀드 유지) ② **Run→Walk 임계 4.0→2.0**(일반 이동 경사 딥 3.5~4.8이 임계와 겹쳐 진동 — 실제 정지만 Walk 복귀) ③ **Run 클립 재생속도 스케일링**: `run.speedParameter="Speed"` + `speedParameterActive=true` + `speed=0.2f` → 재생속도=Speed×0.2(5m/s서 1배속, 15m/s서 3배속 — 발 매칭). **적용 조건: Tools > Anim > Build Mixamo Controllers 재실행 필수(빌더 변경이므로)**. InputProbe 30→120초.

**B3-1 흙길 네트워크 (신규):** NationTerrainController.GenerateCombinedTexture에 흙길 페인트 패스 — **53 세그먼트**(4방위 방사 스포크 r100→1500 + Ring3(550m)/Ring2(1000m) 링 도로 15° 폴리라인 + 스폰(728,-529)→East 스포크 수선 연결). 픽셀↔월드 매핑 동일 수식, AABB 사전 필터, 중심선 85%→가장자리 0% 스무스 블렌드(흙색 0.52/0.40/0.28, 폭 5m). 높이 평탄화 없음(시각 우선 — 2단계 과제).

**B3-2/3/4 밀도 최종:** 숲 게이트 0.55→0.48·개활지 스킵 70%→50%(숲 커버리지 상향) / 꽃밭 FLOWER_FREQ 0.012→0.009(패치 83m→110m) / 호수 bound ±1500 + **대형 호수 2개 수동 배치**((400,300) r180, (-560,-520) r150 — 기존 규약 waterLevel 계산 재사용, 시뮬레이션으로 무충돌 검증, 총 17개).

**판정 대기 (Play):** ① **빌더 재실행 후** 컨트롤러 재생성 확인(Player_AC_AC 중복 시 파일 교체 — 이전 절차) ② 달리기/스프린트 클립 끊김·미끄러짐 소멸([State] 진동 + [InputProbe] 120s 요약) ③ **흙길이 스폰→Ring1 경로에 표시**(스크린샷) ④ 숲 군락/대형 호수 2개/꽃밭 110m/메사 25% 스크린샷 ⑤ 성/영지/데코 부착 확인.

---

## 2026-09-07: Phase 1~3 — 로코모션 믹사모 전환 + 검 부착 + 잔디 전체 커버 + 가시화 ✅ (판정 대기)

**Phase 1 (클립 자연스러움):** ① 빌더 Create()를 **파일 레벨 스왑**으로 수리(DeleteAsset 의존 제거 — File.Delete+ImportAsset, Player_AC_AC 재발 원천 차단) ② **로코모션 믹사모 전환**(유저안 채택): Idle=`Idle.fbx`, Walk=`Walking.fbx`, Run=`Running.fbx`, Jump=`Standing Jump.fbx` — Attack/AttackCombo/Hit는 팩 OneHand 유지, Roll/Death 믹사모 유지. 병사는 검 든 유닛이라 팩 OneHand 유지 ③ Run 재생속도 0.2→**0.28**(믹사모 Running 자연 페이스 ~3.5m/s — 5m/s서 1.4배속) ④ **검 부착**: steel_sword.glb → RightHand 본(스케일 0.9m 정규화, try-catch) — 팩 공격 클립이 검을 전제하므로 공격이 자연스러워짐.

**Phase 2 (잔디 전체 커버):** RuntimeTerrainChunkManager에 **정적 병합 잔디** 추가 — 청크당 2500터프(4m 그리드, 교차 쿼드, 결정론 시드), 호수 반경 1.05r 내부 제외, 청크당 1드로우콜(20k 정점), 전체 약 128만 정점/16만 터프. 동적 밀집(45m, 예산 50→**100**)은 근접 디테일로 역할 분리.

**Phase 3 (가시화/다양성):** 텍스처 **256→1024**(12.5→3.1m/px — 흙길 5m 가시화; 씬 직렬화 값도 1024로 수정 — 코드 기본값만으론 미적용 문제), 메사 25→**35%**, 꽃밭 패치 110→**140m**(FLOWER_FREQ 0.007).

**주의:** Player_AC_AC.controller 잔재 삭제 완료. 컴파일은 에디터 포커스 시 최종 확인.

**판정 대기 (Play):** ① 빌더 재실행 → Player_AC 단일 확인 ② 검 부착 + 믹사모 로코모션 자연스러움(스크린샷) ③ 잔디 전체 커버 + 프레임 수치 ④ 흙길 표시(1024 텍스처) ⑤ 메사 35%/꽃밭 140m 체감 ⑥ [State]/[InputProbe] 수치.

---

## 2026-09-06: 애니 끊김 수리 — 정지 스냅 홀드 타이머 ✅ (판정 대기)

**증상:** 전 지형에서 애니는 작동하나 걷기/달리기 중 애니가 "자꾸 끊겨서 재생".

**원인 (Editor.log [State] 전환 로그로 확정):** HumanoidClipDriver의 정지 스냅이 raw<0.05 **3프레임(50ms)**만 유지돼도 _smoothedSpeed를 즉시 0으로 만듦 → 걷는 중 미세 정체(청크 이음새/구릉 접촉/경사 순간 정체)마다 Run→Walk→Idle→Walk→Run 전체 사이클이 돌고, 전환마다 클립이 normT=0에서 재시작 → 끊김. 증거: `Walk → Idle (speed=4.89 rawSpd=5.00)` — 0.05초 전환 구간 동안 raw가 이미 5.0으로 복귀했는데 상태 전환이 확정된 패턴 반복.

**수리:** 스냅 조건을 3프레임 카운터 → **0.25초 연속 홀드 타이머**(_stallTime)로 교체. 미세 정체는 스무딩이 흡수하고, 진짜 정지(0.25초)만 즉시 Idle. (커밋 cb8a7069)

**판정 대기 (Play):** ① 걷기/달리기 중 클립 끊김 소멸(눈확인) ② [State] 전환 빈도 대폭 감소(로그) ③ 정지 시 Idle 전환은 ~0.3초 내 유지(응답성).

---

## 2026-09-05: 접지 구조 개선 — 물리 접촉 우선 + 수식 안전망 (데코/건물 위 자연 착지) ✅

**직전 버전의 구조적 결함:** `ClampToGroundByHeight`가 매 프레임 수식(GetHeightAt)으로 캐릭터를 지표면+0.02에 **항상 스냅** → ①데코/건물 콜라이더 위에 서 있어도 지면으로 끌어내려 관통 ②물리 `isGrounded`가 false로 남고 AA3의 `Move(down*0.02)`가 수식 스냅에 캔슬되는 **배타 구조**. (지형지물 위 안정 접지 시나리오에서 실제 문제 발생 가능)

**수리 (PlayerMovement.cs `ClampToGroundByHeight()` 교체, code agent + QA agent PASS):**
1. **물리 접촉 우선** — `!isGrounded && vv≤0 && !rolling`이면 `CC.Move(down*0.05)`로 지형/데코/건물 콜라이더와 실제 충돌 유도, 접지 즉시 `_isGrounded=true`·`return`(수식 개입 없음). 접지 상태에선 추가 Move 없음 → 이중 Move 아님.
2. **이탈/추락 안전망(수식)** — 지형 메시는 `TerrainTextureApplier`가 GetHeightAt으로 재표본되므로 GetHeightAt=지표면과 정확 일치. `feetY < formulaY−0.5`(지형 콜라이더 유실·낙하 위험)만 수식으로 복귀, `feetY < formulaY+0.02`(소량 파묻힘)만 표면 정렬. **`feetY ≥ formulaY+0.02`(데코 위·구릉 정상)= 개입 안 함 → CC가 자연 접지 유지.**

**QA PASS:** 점프(`_isJumping` early-return, vv≤0 확정으로 조기해제 없음)·구르기(MovePlayer early-return이라 clamp 미호출)·데코 위 접지(`feetY≥formulaY+0.02` 분기 미발동→0.05m nudge로 자연 충돌) 흐름 정상. 중괄호 127/127·괄호 339/339, `formulaY` 전역 규약(1f+GetHeightAt Plains42) 일치. **배치 컴파일 error CS=0, warning CS=0.** (관찰 권장: Plains/42 하드코딩 — 바이옴·시드 동적화 시 재검토; 구버전 `ClampToGround()` 죽은 코드 잔존)

**판정 대기 (Play):** ① 나무/바위/건물 위에 올라섰을 때 지면으로 끌려내려가지 않고 그 콜라이더 위에 안정 착지 ② 경사 내려가는 중 캐릭터가 지면을 따라 붙음(허버 없음) ③ 발밑 접지 그림자가 지면에 상시 밀착. 에디터 Play 후 새 스크린샷 판정.

---

## 2026-09-05: 지형 접지감 수리 + 프로젝트 컴파일 블로커 해제 ✅

**문제:** 지형지물(나무/바위/잔디)+플레이어가 땅에서 떠보임. y 수학은 전 시스템이 공통 기준(`GROUND_BASE=1f + TerrainGenerator.GetHeightAt(x,z,Plains,42)`)으로 정확히 일치했으므로 재정렬이 아니라 **접지감(접촉 그림자 + 실제 물리 접지) 부재**가 근본 원인. CollisionDebugger 로그로 확정: 플레이어 `pos=(752, 3.97, -515) isGrounded=False` 지속 + `_verticalVelocity` 누적(중력이 물리적으로 해소 안 됨) — ClampToGroundByHeight가 매 프레임 위치를 텔레포트로 고정해 CC가 실 접촉을 하지 못한 구조.

**수리 (PlayerMovement.cs, code agent):**
1. **플레이어 동적 접지 그림자(BlobShadow) 부착** — Start()에서 `GetOrAdd<BlobShadow>()` (GetOrAdd 중복 방지, try/catch 실패 시 경고 후 계속). BlobShadow는 LateUpdate에서 `GetHeightAt+GROUND_BASE+0.05`로 발밑 고정 그림자(r=0.8/α=0.35).
2. **물리 접지 복구** — ApplyGravity에서 `_controller.isGrounded==false && _isGrounded && vv<0 && !_isRolling`이면 `CC.Move(down*0.02)` 1회로 실 충돌 유도해 isGrounded=true(접지 시 vv=-2 규약 유지). 점프 상승·구르기는 가드로 미개입.
3. **ClampToGroundByHeight 개조** — 텔레포트 대신 CC.Move(up/down)로 물리 접지 유지 + 0.5m 초과 파묻힘/이탈 시 최후 하드 스냅. 파묻힘 0.5m 이내·공중 0.5m 이내는 무개입(gravity가 자연 착지).

**추가 수리 (HeatAvatarMappingFix.cs, 24건 컴파일 에러 → 0):**
애니 8차 도구가 **무존재 타입 `HumanDescriptionBone`**과 **`HumanLimit.value/length/modified`**(존재 X)를 사용해 프로젝트 전체를 미컴파일 상태로 만들고 있었음 → 모든 Play 판정이 구(스테일) 어셈블리로 돈 셈. 교정: `HumanBone[]`(boneName/humanName/limit) + `HumanLimit{useDefaultValues,min,max}`. **교훈: 8차 "QA PASS 4/4"는 문법 검증일 뿐 실제 API 타입/필드 오류를 놓침 — 배치모드 컴파일로 error CS=0 검증이 선행돼야 함.**

**QA PASS:** code agent(PlayerMovement) + code agent(Heat 교정) + QA agent 리뷰(양 파일 PASS, 메서드 단일·중괄호 균형·점프/구르기 흐름 무결·`1<<9`=Ground 검증). **배치모드 컴파일 error CS=0, warning CS=0.**

**판정 대기 (Play):** ① 플레이어 발밑 접지 그림자 상시 확인 ② 걷기/경사에서 머무르다 멈출 때 캐릭터가 지면에 붙는(허버 없음) ③ 지형지물 밑동이 지면에 닿아 보임. 에디터 Play 후 새 스크린샷으로 판정.

---

## 2026-09-06: 애니 9차 — FBX 골격 위계 수리 (매핑 시프트의 근본 원인) ✅

**문제:** 8차 매핑 주입 후에도 동결 지속. DD3-1이 결정 증거 포착 — 아바타 매핑이 `Spine→Hips, Chest→Spine, UpperChest→Chest, Chest2→UpperChest`로 **강제 시프트**되고 정작 Hips 본 탈락. Blender hier_probe로 파일 진단 → **다리(Left/RightUpperLeg)가 Hips가 아닌 Spine의 자식** + 목/어깨/breast가 비표준 Chest2 아래. Unity 휴머노이드 위상규칙(다리=Hips 직계) 위반 → Unity가 어떤 메타 매핑이든 임포트 시 강제 재배치 → 근육값이 어긋난 뼈에 기록 → 동결. (8차까지의 "매핑 비어있음/자동매핑" 가설은 모두 이 위계 문제의 하위 현상이었음)

**수리 (Blender 5.1 headless, roll_make/fix_hierarchy_v2.py):** edit 모드에서 `use_connect=False` + parent 재할당만 (행렬 복원 코드는 오히려 본을 밈 — 제거): 다리 2개→Hips, Neck/LeftShoulder/RightShoulder/breast.L/R→UpperChest, pelvis.L/R은 Spine 아래 유지. **검증: 전 본 head/tail 무손상(identical), 버텍스그룹 불변, roundtrip 재임포트로 위계/본수 27 확인.** 백업: roll_make/Player_Rigged_Heat_backup_before_reparent.fbx. 신규 지식: **원본부터 Hips 본은 메시 가중치가 없음(26/27) — rootBone 앵커 역할만 하므로 무해(스파인/다리 가중치로 움직임 전달).** 8차 메타 매핑(22개)은 이제 위상검증 통과 — Unity가 더 이상 시프트하지 않아야 함.

**판정 대기 (Play):** ① DD3-1 매핑에 `Hips→Hips` 포함 + 시프트 소멸(Spine→Spine, Chest→Chest...) ② 직선 이동 중 LFootΔ/LHandΔ가 회전 설명치 초과(주의: DD3-2 상대Δ는 루트 회전에 오염됨 — 직선 보행 기준 판정) ③ 보행 스윙 눈확인(56.PNG와 다른 포즈) ④ `[JumpProbe] grounded=` 값.

---

## 2026-09-05: 애니 8차 — 아바타 매핑 명시 주입 (RPG팩·믹사모 동결의 근본 수리) ✅

**문제 확정:** RPG팩/믹사모 무관하게 몸이 "걷기 한 프레임" 자세에서 동결(56.PNG). Animator 상태 전환·normT 진행·SMR·아바타 isHuman 전부 정상으로 보였으나 **Heat 메타의 `humanDescription.human`이 `[]` (매핑 0개)**. 7차에서 "자동매핑 유도" 목적으로 비운 것이 원인 — 사지(Limb)가 미매핑되면 Run/Walk 클립의 근육값이 행선지가 없어 몸은 임포트 시점 자세(bake_anim=False → export 시 자세)에 영원히 동결.

**오판 유발 요인 2개 (교훈):** ① `isHuman=True`는 매핑 0개여도 True ② `GetBoneTransform(Hips)`는 Hips만 매핑돼도 non-null — hipsΔ는 보행 판정 지표로 무용(월드 이동이 지배). 사지 상대Δ를 봤어야 했음.

**수리:** ① 메타에 22개 표준 Humanoid 매핑 명시 기록(boneName=humanName, canonical 순서, soldier 참조 서식, +177/−1) ② 복구 도구 `Assets/Editor/HeatAvatarMappingFix.cs` — Tools/Anim에 매핑 적용(ModelImporter API + SaveAndReimport)·덤프 메뉴 ③ DD3 진단기(HumanoidClipDriver): 매핑 덤프+실질매핑 n/55 스캔, 사지 상대Δ(LHandΔ/LFootΔ), SMR 외부골격 검사 ④ PlayerMovement [JumpProbe]: Space 입력 시 grounded/rolling/mount/vv 스냅샷.

**QA PASS (4/4):** 메타 YAML 파싱·22본 대조(rerig_report bones_final)·API 검증·균형 검사 통과. 누락된 HeatAvatarMappingFix.cs.meta는 QA가 생성.

**판정 대기 (Play):** ① DD3-1 `매핑 본수=22` + `실질매핑≥17` ② 이동 중 `LHandΔ/LFootΔ > 0` ③ 보행 스윙 눈확인 ④ Space → `[JumpProbe]` 로그의 grounded 값. **JumpProbe 로그가 아예 안 찍히면** 구르기(_isRolling) 잔존 또는 탑승(MountSystem) 잔존이 점프 원인(probe가 그 검사보다 뒤에 있음).

---

## 2026-09-05: 애니 7차 — Heat meta 재작성 + 틴트 타입검사 수리 + hips 가드 ✅

**문제1:** 6차에서 손으로 만든 meta의 스키마 오류(animations 블록 안 animationType, human 엔트리 1개, 중복 키)로 Unity가 Heat FBX를 **Generic 아바타로 임포트** → `InvalidOperationException: Avatar is not of type humanoid` (GetBoneTransform).
**수리:** meta 전면 재작성 — human: [] / skeleton: [] 빈 리스트 + animationType: 3 + avatarSetup: 1 → Unity가 히트 본 이름(Hips/Spine/Head/LeftUpperLeg...)을 **자동 매핑** (guid 유지 b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91).

**문제2:** 틴트 경고 스팸 재발 — FindPropertyIndex==-1인데 기본값 Color로 SetColor하는 빈틈(936행). **수리:** idx<0이면 _Custom_Color 세팅을 건너뛰고 _Color→_BaseColor 폴백.

**문제3:** DD2 진단기가 non-humanoid 아바타에서 GetBoneTransform 예외. **수리:** 측정 전 avatar.isValid && isHuman 가드, 아니면 `hips=N/A(nonhumanoid)`.

**QA PASS:** 3파일(+36/−65), 중복키 0, 괄호 균형 완벽. 커밋 48a3a885.

**다음 Play 판정:** ① 에디터 포커스 → Heat FBX 재임포트(자동 아바타 생성) ② 이동 시 몸 동작 + `Idle→Walk→Run` ③ DD2 `avatar isHuman=True` + 예외 소멸 ④ 틴트 경고 스팸 소멸. 여전히 정지면 rigImportWarnings 로그 제출 → Configure 매핑표 수리.

---

## 2026-09-05: 애니 6차(A안 실행) — Blender 리그 변환 Player_Rigged_Heat.fbx 통합 ✅

**근본 원인 최종 확정:** 원본 Player_Rigged의 리그 = Blender Rigify 커스텀(27본: Root/pelvis.L·R 분리/spine.001~.005/head·neck 없음). Unity Humanoid 필수 본(Hips/Spine/Head/좌우 사지 히트명) 미충족 → 아바타 isValid=True에도 **리타겟 대상 뼈가 없어 시각 변화 0**. (DD2 최종판: Neural=0 Hybrid=0에도 뼈 정지 — 재생 시스템 전부 정상, 모델 리그가 원인. DD1 로그 hipsΔ 0.4~7.9m은 Animator 재생 입증)

**A안 실행 (Blender 3.6 headless, roll_make/rerig_heat.py):**
- 본 리네임 매핑 23개: Root→Hips, spine→Spine, spine.001→Chest, spine.002→UpperChest, spine.004→Neck, **spine.005→Head(목-머리 부재 보정)**, shoulder.L/R→Left/RightShoulder, upper_arm→UpperArm, forearm→LowerArm, hand→Hand, thigh→UpperLeg, shin→LowerLeg, foot→Foot, toe→Toes
- breast.L/R, pelvis.L/R은 무매핑 유지(Humanoid 필수 아님)
- 메시 버텍스 그룹 자동 리네임(뼈 이름 추적)
- export: bake_anim=False(모델 전용 — 클립은 Player_AC 담당)
- **검증: 필수 17본 전부 존재, 메시 required_hit 17/17**

**산출물:** `roll_make/Player_Rigged_Heat.fbx` → `Assets/Resources/Models/UserProvided/fbx/` 복사 + Humanoid meta(guid b7c3d9e2f1a64c5e8d0b2a7c4e6f8a91, animationType 3, avatarSetup 1) + GameSetup.cs 로드 경로 `Player_Rigged`→`Player_Rigged_Heat` (머티리얼 원본은 GLB 그대로)

**주의:** .gitignore:43 `models/` 룰이 Assets/Resources/Models/를 무시 → **git add -f로 강제 트래킹** (기존 Player_Rigged.fbx도 미트래킹 상태였음 — 인지할 것)

**검증 수치:** missing_required=[] / mesh vertex_groups=27 hit=17/17 / bones_final 27개

**판정 대기 (Play):** 이동 시 몸이 직접 Idle→Walk→Run. 리타겟은 근육공간이라 팩 클립이 Heat 아바타로 재생됨(본 이름 무관).

---

## 2026-09-05: 애니 5차 수리(정책 반영) — Neural/Hybrid 자동부착 완전 제거, Player_AC 단일 경로 확정 ✅