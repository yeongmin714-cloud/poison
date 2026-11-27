# 무번호 181 — 폭탄 폭발 VFX 에셋 배선 + Test_10 인벤 지급 + 착지 텔레그래프 계획

## 사용자 요구
"테스트 10씬에 인벤토리에 폭탄 추가 + 터질 때 VFX를 에셋으로 + 날아가는 것 + 떨어질 때 피격 범위를 몬스터 피격 범위처럼 표시" — 계획서 요청(구현은 승인 후).

## 현황 실측 (Q&A 답변 포함)
**Q. 폭탄은 VFX 에셋 붙어있나? → 아니오(미배선).** 폭탄 시스템 자체는 이미 완전 존재:
- `Bomb.cs`(147줄): 4종(Explosive/PoisonGas/Smoke/Molotov)·퓨즈 0.5s·OverlapSphere 피해(소스 "Explosion" — 패막블록리스트 비포함 = 기존 판정 유지)
- `BombArmController.cs`(255줄): **투척 이미 구현** — 퀵슬롯 등록→번호키 무장→좌클릭 커서 방향 포물선 투척(throwForce 12+upBoost)→인벤 소모. 날아가는 것 = 기존 작동.
- 아이템: `PlayerInventory.Bomb_Explosive`("bomb_explosive", Rare, maxStack 5) — UseItem 무장 계약 존재
- **부재**: ①폭발 VFX = `bomb.explosionEffectPrefab = null` 하드코딩 + 절차 스파크(BombExplosionVisual)만 — 에셋 경로 0 ②Test_10 인벤 지급 부재 ③착지 텔레그래프 부재

## 설계 원칙
기존 폭탄 로직(피해·퓨즈·투척 물리) **무수정** — 이번 라운드는 ①VFX 에셋 배선 ②지급 ③텔레그래프 3건만. 절차 파티클 금지(46차)·에셋 경로 승인 경로(78차 러너 선례) 준수. VFX 색/머티리얼 조작 없음(LDR 계약).

## P1 — 폭발 VFX 에셋 배선 (Bomb.cs)
Bomb.Explode의 효과 인스턴스화 지점에 타입별 에셋 VFX 발화 추가(기존 로직 무수정 — 시각 호출만):
- **Explosive**: `MeteoriteVFXRunner.PlayImpactSequence(pos)` — 180차에 신설된 임팩트 시퀀스(라이트 플래시+2차 파장) 재사용 — **신규 작업 0**
- **Molotov**: `FX/Fire/VFX_Fire_01_Big.prefab` 인스턴스(화염 영역 연출)
- **Smoke**: `FX/Death/DeathSmoke.prefab` 인스턴스
- **PoisonGas**: 별도 없음(기존 유지 — 가스 시각은 기존 가스 시스템 소관, 기록)
- 신규 헬퍼 `BombVfx`(Bomb.cs 내 private static): 프리팹 지연로드+캐시+Instantiate+수명 소멸 — MonsterSkillVfx(180차) 동일 패턴.

## P2 — Test_10 인벤 폭탄 지급 (TestTerritoryCombatSetup.cs)
기존 인벤 지급 블록(L262 근처 — Mat_Wood 5/Mat_Stone 3 지급부)에 `inv.AddItem(PlayerInventory.Bomb_Explosive, 3)` 추가 + 퀵슬롯 등록 안내 로그 1행. 기존 지급 무수정.

## P3 — 착지 텔레그래프 (원형 — TelegraphArcIndicator 확장)
- **사용자 요구 = 몬스터 텔레그래프 같은 룩**: 기존 `TelegraphArcIndicator`는 부채꼴(호) 전용 — **원형 360° 변형 신설**(additive — 기존 호 Spawn 무수정).
- `TelegraphArcIndicator.SpawnCircle(pos, radius, duration, isElite)` — 기존 BuildFanMesh의 재질/펄스/스윕 밴드 코드 재사용(적색 패밀리·[176차] 스윕 밴드 승계 — 벡터메시 금지 계약은 HUD 게이지 소관, 텔레그래프 링은 기존 절차 메시 패밀리 유지).
- **표시 시점**: 낙하 중 퓨즈가 이미 시작되므로 **Bomb.Start(퓨즈 시작)에서 지면 투영 위치(new Vector3(pos.x, 0.02f, pos.z))에 SpawnCircle(explosionRadius, 퓨즈+0.05s)** — 폭탄이 지면에서 흔들리는 동안 반경이 그대로 보이다가 폭발(사용자 요구 "떨어질 때 피격 범위 표시" 충족).

## P4 — 계약테스트 (신규 BombVfxTests, 소스 문자열 계약 관례)
①Explosive=PlayImpactSequence 배선 소스계약 ②VFX 프리팹 경로 문자열+디스크 실존 ③기존 로직 무수정 핀(퓨즈 0.5f·throwForce 12f·피해 루프 유지) ④SpawnCircle 신설+기존 Spawn(호) 무수정 ⑤Test_10 지급 핀(Bomb_Explosive, 3개) ⑥절차 파티클/벡터메시 금지(신규 코드 범위).

## P5 — 부모 QA
컴파일 CS 0 → 신규 테스트 집중 → 전수 EditMode(기준선 20 유지) → QAPROGRESS 기록.

## 대기 게이트
Play 육안(투척→낙하 중 지면 원형 텔레그래프→운석 임팩트 폭발 시퀀스·몬스터 텔레그래프와 톤 일치) · 커밋 별도 승인.
