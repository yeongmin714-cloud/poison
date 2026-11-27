# [무번호 27] 장비 부착 정합성 수리 — 이중 정규화 봉합 + [F4] 측정 오염 차단

## 1. 근거 (로그 실측 — Editor-prev.log, 11-12 12:04)

### 1-1. 이중 정규화(=서로 다른 오브젝트 2회 정규화) — 스택 실측
| 로그 | 호출부 | 대상 | 결과 |
|---|---|---|---|
| L2854 | PlayerPlaceholder.cs:150 (TryLoadGLBModel → Start) | GLB 인스턴스 playerInstance ("PlayerModel") | 1.16m → 1.80m (scale ×1.56) |
| L3014 | TestPlayerAnimatorBoot.cs:94 (Boot 코루틴) | Heat FBX heat ("PlayerModel" 이름 계승) | 1.16m → 1.80m (scale ×1.56) |
| L2867 | PlayerPlaceholder.cs:145 | 플레이어 GLB 로드 성공 — Player_Rigged 부착 | — |

→ **같은 오브젝트를 이중 스케일링하는 것은 아님.** GLB가 Heat로 교체되는 구조:
TestPlayerAnimatorBoot.cs:94 (heat Instantiate → Object.Destroy(oldModel) → NormalizeSoldierHeight(heat))
- L2854는 PlayerPlaceholder가 GLB 인스턴스를 정규화 → L2867 GLB 로드 성공 → L3014는 Heat FBX가 정규화.
- Heat는 L94에서 `localScale=Vector3.one`으로 초기화 후 정규화 → 1.16→1.8m. 정규화 자체는 정상.
- L2854와 L3014는 서로 다른 오브젝트. **같은 오브젝트를 이중 스케일링하는 것은 아님.**

### 1-2. [F4] 측정 오염 (L4067/L5129)
- L4067: `[ArmorVisual] 바디 측정 진단(Test_10_TerritoryCombat): anim=PlayerModel, avatar=OK, Head본=2.93m Spine본=2.43m LeftFoot본=2.13m RightHand본=2.62m, fullBody=(1.68, 4.34, 1.20), parts h=(1.44,1.46,1.05) t=(1.65,0.64,0.90) fl=(0.68,0.32,0.53) fr=(0.34,0.31,0.50) hl=invalid hr=invalid, 제외 렌더러=2`
- L5129: `[침하감시] 모델 bounds.center.y=1.651 / 루트 y=0.874 / 편차=0.778m`
- fullBody H=4.34m — 1.8m 계약 대비 2.4배. Heat FBX는 1.8m로 정규화 완료 상태에서의 측정.
- → **오염원: 플레이어 루트(Player) 하위 전 렌더러 합산** — placeholder cube body/head(0.6×0.6×0.4 / 0.35)와 BlobShadow가 플레이어 루트 하위에 공존(렌더러). 제외 렌더러=2(무기+트레일)만 제외.
- L4049: `본 매핑 의심 — 본 월드 y가 계약 밴드 이탈(head=False, spine=False, foot=True) — 비율 밴드 임계로 대체` — 105차 F2 폴백 발동. 오염 bounds로 인한 본 정상성 교차검증 실패.

### 1-3. 장비 피팅 오염 전파
- L4120/L4540: `피팅 Helmet: part=(1.44,1.46,1.05) scale보정=x2.52` → L4140/L4560: `✅ Helmet 부착: wood_helmet → Head (스케일 x0.28, 본↔중심 0.80m)` + `⚠️ Helmet 중심이 본에서 0.54m 이상 벗어남` — 투구 부유
- L4227/L4647: `피팅 Armor: part=(1.65,0.64,0.90) scale보정=x2.45(2차 x2.84)` — 어깨폭 오염
- L4297: `부위 측정 게이트 실패 Shoes part=(0.68,0.32,0.53) H=4.34 — 폴백 상수 사용`
- → 부위 bounds가 fullBody 오염(H=4.34m)을 전파받아 IsPartPlausible 게이트를 우회(비율 밴드 폴백) → 스케일 클램프(x2.52)로 과대 + 부유.

### 1-4. 무기 뒤집힘 (L3889)
- L3889: `[Weapon] 팁-끝 진단: 손 거리 min=1.08m / max=0.49m → 칼끝(min축) = 손에서 가까운 쪽 ⚠뒤집힌다, tipWorld=(0.88,1.27,0.38)`
- L3873: `[Weapon] 그립 정렬(DATA): gripT=0.67` — wood_spear GripStrategy=3(DATA 모드)
- → wood_spear GripEnd=-1(min축)인데 실제 블레이드는 max축. WeaponEquipManager.ApplyBoundsGripAlignment의 칼끝 축/부호 선택 수리 필요.

## 2. 계획 (Phase)

### P1 — [F4] 측정 오염 차단 (ArmorVisualAttachSystem.cs / PlayerBodyMeasure)
- **목표**: PlayerBodyMeasure.TryMeasurePlayerBody에서 fullBody 합산 대상을 Heat FBX 스켈레톤(anim의 본)으로 제한.
- **수리**: fullBody 계산 대상 렌더러를 placeAnim(anim=PlayerModel)의 본 계층에 실제 속한 렌더러만으로 제한 —
  `r.transform.IsChildOf(placeAnim.transform)` 필터 추가(placeholder cube body/head, BlobShadow 등 anim 계층 밖 렌더러 제외).
- **기대**: fullBody H → 1.8m 수렴, 부위 bounds 정상화, IsPartPlausible 게이트 정상 작동, 투구/갑옷 스케일 정상화.
- **파일**: Assets/Scripts/Systems/ArmorVisualAttachSystem.cs (PlayerBodyMeasure.TryMeasurePlayerBody)

### P2 — 무기 뒤집힘 수리 (WeaponEquipManager.cs)
- **목표**: wood_spear DATA 모드에서 팁-끝이 손에서 가까운 쪽(min축)을 선택하는 오류 수리.
- **수리**: ApplyBoundsGripAlignment의 칼끝 선택 로직 — GripEnd=-1(min축) 지정이 실제 블레이드 방향(max축)과 불일치.
  - wood_spear GripEnd=-1 → GripEnd=+1 로 교체 또는 팁-끝 진단(손 거리 비교)이 우선하도록 수리.
- **파일**: Assets/Scripts/Systems/WeaponEquipManager.cs (L55-61 _gripTable, ApplyBoundsGripAlignment)

### P3 — [F4] 진단 로그 보강 (선택)
- **목표**: [F4] 진단에 fullBody 합산 대상 렌더러 수(anim 계층 내/외) 표시 추가 → 오염원 특정 즉시화.
- **파일**: Assets/Scripts/Systems/ArmorVisualAttachSystem.cs (PlayerBodyMeasure.TryMeasurePlayerBody)

## 3. 검증
- 부모 게이트: `rm -f Temp/UnityLockfile && timeout 550 bash compile_test.sh` → `grep -c 'error CS' compile.log` = 0
- EditMode 전수(run_editmode_check.sh) + baseline 10건 1:1 대조
- Play 판정: 사용자 승인 후 — [F4] 진단의 fullBody H ≈ 1.8m 수렴, 투구/갑옷 정상 부착, 창 뒤집힘 해소.

## 4. 승인
- 사용자 "진행" (11-12) — P1 이중 정규화 봉합 → P2 무기 뒤집힘 → P3 진단 보강 순서 구현 위임.
