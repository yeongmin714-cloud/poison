# 105차 계획 — 플레이어 장비 착용 상태 수리 (바디 측정 오염 + 1.8m 스케일 미적용 폴백 상수)

> 발단: 사용자 리포트 — 마을씬 테스트 14 영상에서 플레이어 장비 착용 상태가 비정상.
> 프레임 실측(ffmpeg fps=0.5 → 5×2 타일 + 전신 크롭): ①투구가 머리 위 공중 부유 ②갑옷이 골반/다리 쪽으로 처져 과대 배치 ③방패 옆구리 근처 부유. (장착 예시.PNG / 장착 예시 2.PNG = 나무 세트 정상 착용 참조)

## 1. 근거 (Editor.log — 2026-10-20 세션, 줄 번호)

```
[TerritoryBuilder] 병사 크기 정규화 — PlayerModel 높이 1.16m → 1.80m (scale ×1.56)   (L4259)
[ArmorVisual] 피팅 Helmet: part=(1.45, 1.46, 1.21) scale보정=x2.52 center=(-389.97, 4.59, -74.59)  (L5499·L6190)
[ArmorVisual] ✅ Helmet ... (스케일 x0.18, anchor=본 기준, 본↔중심 0.81m)            (L6210)
[ArmorVisual] ⚠️ Helmet 비주얼 중심이 본에서 0.35m 이상 벗어남 — 포즈 튜닝 필요       (L6229)
[ArmorVisual] 피팅 Armor: part=(1.65, 0.64, 0.97) scale보정=x3.00 center=(-390.02, 3.02, -74.43)   (L5606·L6297)
[Weapon] 그립 정렬(DATA): bone=RightHand, type=Spear, gripT=0.67 ... (정상 정렬)      (L5287)
```

## 2. 근원 분석 (감독자 판정 — 두 갈래)

### 뿌리 1 — 무기 GLB 정점이 바디 측정을 오염 (주 원인)
- `ArmorVisualAttachSystem.PlayerBodyMeasure.TryMeasurePlayerBody` (L716~868)는 Player 태그 하위 **모든** 렌더러 정점을 샘플링해 부위 bounds를 만든다. 제외 목록(`excludeRoots`)은 `_slotVisuals`(갑옷 비주얼)뿐 — **WeaponEquipManager가 부착한 무기 GLB(wood_spear, bounds z=1.27m)는 제외 대상이 아니다.**
- Head 밴드 임계(headBone.y−0.05) 위로 창끝이 올라오면(들어올린 창) head part가 skull+창으로 성립 → 실측 head part (1.45, 1.46, 1.21) = 두개골(≈0.62m) + 창 돌출. 투구 배치 target = partCenter + up×8%×partSize.y → 창 오염분만큼 **머리 위로 부유**(본↔중심 0.81m — 두 fit 수치 교차검증으로 Head 밴드 하단 ≈ 지면+0.8m 확정).
- Torso 밴드에 창대가 가로질러 torso part x=1.65m(실제 어깨폭 대비 ~3배) → `scale=(pSize.x×1.25)/b.size.x` = ×3.00(클램프 상한) → **갑옷 1.86m 폭 과대 + 처짐**.
- 69차(2026-09-16) 당시 검증은 무기 미장착/단검 상태라 오염이 노출되지 않았다.

### 뿌리 2 — 폴백 상수가 1.16m 자연 키 기준 절대값 (51차→54차 1.8m 정규화 미동행)
- 플레이어 리그는 TestPlayerAnimatorBoot가 Heat.fbx(자연 1.16m)로 교체 후 ×1.56 → 1.8m(L4259 실측).
- character-height-contract.md 경고대로, bodyFitted 실패 시 사용하는 절대 상수군이 ×1.56 미적용:
  - `_targetSize`(Helmet 0.34 등 7슬롯), worldAnchor 오프셋(투구 +0.26m 등), `footRadius 0.28`/`handRadius 0.25`, 헬멧 치수 캡 0.6, 본↔중심 경고 임계 0.35.
- 참고: `_poseTable.LocalPos`·`_centerOffset`은 bone-local(TransformPoint)이라 자동 스케일 — 변경 대상 아님.

## 3. 수정 항목 (구현 서브에이전트 지시)

### [F1] 측정 오염 차단 — PlayerBodyMeasure 제외 목록 확장
- `WeaponEquipManager`에 `public static GameObject CurrentVisual => _current;` 노출(주석: [105차] 바디 측정 오염 제외용).
- `InstantiateAttached`의 excludeRoots 구성(L253~257)에 무기 비주얼 루트 추가: `WeaponEquipManager.CurrentVisual != null → excludeRoots.Add(...)`. 누군가 재장착해도 매 부착마다 재조회되므로 멱등.
- **동일 프레임 파괴 누수 수리**: `DestroySlotVisuals`는 `Destroy`(프레임 말 지연) 후 `list.Clear()`로 제외 목록에서 즉시 빠져, 같은 프레임 측정에 파괴 예정 비주얼(잘못 부착된 이전 투구 등)이 여전히 기여한다 → `DestroySlotVisuals`는 파괴한 transform을 `_pendingExcluded`(인스턴스 List<Transform>)로 옮겨두고, `TryMeasurePlayerBody` excludeRoots에 합친 뒤 다음 측정에서 해제하는 1프레임 유지 정책으로 수리.
- 예방: 측정 루프에서 렌더러의 루트가 제외 목록 어느 조상 아래도 아닌데 **bone 계층 밖 오브젝트**(예: Player 루트 직속 비골격 파티클)는 건드리지 않는다 — 범위 최소화(무기+파괴 예정분만).

### [F2] 부위 측정 정상성 게이트 + 비율 밴드 폴백
- 측정 루프에 **전신 bounds(`fullBody`)도 함께 누적**(분류 이전 샘플 전부). H = fullBody.size.y (H < 0.5m이면 측정 전체 무효 처리).
- 순수 정적 판정 함수 신설(테스트 가능):
  `static bool IsPartPlausible(EquipmentSlot slot, Bounds part, Bounds fullBody)`
  - Helmet/Mask: part.size.y ≤ 0.55×H AND part.center.y ≥ fullBody.min.y + 0.45×H
  - Armor/Bag: part.size.x ≤ 0.75×H AND part.size.y ≤ 0.55×H AND part.center.y ≥ fullBody.min.y + 0.25×H
  - Shoes: part.size.y ≤ 0.25×H AND part.center.y ≤ fullBody.min.y + 0.20×H
  - Gloves: 최대축 ≤ 0.40×H
  - (근거: 치비 리그 두개골 하단 = 키의 57% — 69차 실측 Head 본 로컬 0.57)
- `FitVisualToBodyPart` 호출부에서 part가 게이트 실패 시 bodyFitted=false로 간주(로그 1회: `[ArmorVisual] 부위 측정 게이트 실패 {slot} part=... H=... — 폴백 상수 사용`). 기존 파트로 fit 진행 금지.
- 본 정상성 교차검증(진단용): Head/Spine/LeftFoot 본 월드 y와 fullBody 대비 비율이 계약 밴드(Head: 0.35~0.85×H, Spine: 0.15~0.60×H, Foot: ≤0.15×H)를 벗어나면 `[ArmorVisual] 본 매핑 의심` 경고 1회 + 그 본 기준 밴드 임계를 비율 밴드(headThreshold = fullBody.min.y + 0.57×H, torsoLow = fullBody.min.y + 0.20×H)로 대체.

### [F3] 폴백 절대 상수 ×1.8 정규화 동행
- `const float NaturalBodyHeight = 1.16f;` 신설 + `static float BodyScale => TerritoryBuilder.SoldierTargetHeight / NaturalBodyHeight;` (근거 주석: Editor.log "1.16m → 1.80m ×1.56").
- 아래 상수는 **코드에서 69차 원값 × BodyScale로 산정**(하드코딩 결과값 금지 — 배율 근거 보존):
  - `_targetSize`: Helmet 0.34 / Armor 0.62 / Shoes 0.36 / Gloves 0.24 / Back 0.85 / Mask 0.30 / Bag 0.45 → 전 슬롯 ×BodyScale.
  - worldAnchor 오프셋: Helmet up 0.26 / Mask fwd 0.06·up 0.19 / Armor up 0.15 / Bag up 0.12·back 0.09 / Gloves down 0.02 / Shoes down 0.05 / Back lateral 0.10·up 0.02 → 전부 ×BodyScale.
  - `footRadius` 0.28 → ×BodyScale, `handRadius` 0.25 → ×BodyScale (const 폐기 → static readonly 프로퍼티 또는 TryMeasurePlayerBody에서 산정).
  - Helmet 치수 캡 0.6 → ×BodyScale, 본↔중심 경고 임계 0.35 → ×BodyScale.
- NormalizeVisualScale 클램프(0.12~5.0)·FitVisualToBodyPart 클램프(0.3~3.0)는 유지.

### [F4] 진단 로그 (다음 Play 판정용)
- TryMeasurePlayerBody 종료 시 슬롯 첫 부착에 1회(WarnOnce 키 `measure-diag:{scene}`): placeAnim 이름/avatar null 여부, Head/Spine/LeftFoot/RightHand 본 월드 y(지면 대비 m), fullBody bounds, part bounds 요약, 제외한 렌더러 수. 정상이면 정보성 1줄.

### [F5] 테스트 (신규 1 파일)
- `Assets/Scripts/Tests/EditMode/ArmorVisualFitGuardTests.cs` 신규. 기존 계약테스트 관행(source-string Does.Contain + 순수 수학 테스트) 따름:
  1. `IsPartPlausible` 순수 테스트: Helmet 오염 bounds(1.45,1.46,1.21 @ H=1.8) → false / 정상 bounds(0.5,0.62,0.5, center 상단 43%) → true. Armor 폭 1.65 → false. Shoes 하단 밴드 → true.
  2. 소스 계약: ArmorVisualAttachSystem에 `CurrentVisual` 제외 합침 존재 / `NaturalBodyHeight` 1.16 / `_targetSize`가 ×BodyScale 산정식 사용 / worldAnchor 0.26 원값×BodyScale 형태.
  3. WeaponEquipManager에 `public static GameObject CurrentVisual` 존재.
- 기존 ArmorVisual 관련 테스트가 있다면 회귀 확인(검색 후 있으면 대조).

## 4. 범위 파일 (화이트리스트)
- `Assets/Scripts/Systems/ArmorVisualAttachSystem.cs` (신규 수정)
- `Assets/Scripts/Systems/WeaponEquipManager.cs` (⚠️이미 더티 — 다차 미커밋 진실상태 위에 최소 1프로퍼티 추가만. 다른 더티 변경은 절대 건드리지 않음)
- `Assets/Scripts/Tests/EditMode/ArmorVisualFitGuardTests.cs` (+.meta 신규)
- 그 외 일체 금지. 추가 필요 시 즉시 보고 후 승인.

## 5. 완료 기준
- 컴파일 `error CS=0`.
- 신규 테스트 전부 GREEN + 기존 EditMode 전수에서 신규 회귀 0건(기존 baseline 실패 목록과 1:1 동일성).
- 착용 관련 로직상 계약 유지: Weapon 슬롯은 본 시스템이 건드리지 않음 / Watchdog·리컨실레이션 동작 불변.

## 6. Play 검증 항목 (사용자 승인 대기 — 별도 게이트)
1. 나무 세트 전체 착용 시: 투구=머리 정수리 감쌈(부유 0) / 갑옷=가슴~허리 밀착(다리 내려앉음 0) / 부츠=발 접지 / 장갑=양손 / 방패=왼팔 바깥.
2. 창 장착 상태에서 갑옷/투구 재장착 → 위치가 창 존재와 무관하게 동일(오염 차단 증명).
3. 장착 해제→재장착 반복 3회 → 위치/크기 불변(멱등).
4. Editor.log에 [F4] 진단 1줄 + 게이트 실패 0건(정상 측정 시).

## 7. 참조
- `~/.hermes/skills/game-dev/poison-unity-game-systems/references/character-height-contract.md` (51차 1.8m 계약·갑옷 캘리브레이션 감사 항목)
- 장비 정규 치수(69차): 투구 0.27h / 갑옷 0.62w·0.34h / 방패 0.80×0.40 / 가면 0.24×0.21 / 가방 0.39×0.26 / 부츠 0.19×0.36 / 장갑 0.15×0.08 (자연 1.16m 기준)
