# 📦 Asset Store 임포트 자산 인벤토리 (적재적소 매핑)

> **목적**: 스토어에서 받은 이펙트/에셋 자산을 등록해두고, 필요할 때 적재적소에 배치한다.
> 새 에셋을 받으면 이 표에 추가한다. **최종 갱신: 2026-09-12 (41차 기준 실측)**

## 1. 이펙트 에셋

| 에셋 폴더 | 내용 | 현재/계획 용도 | 상태 |
|:--|:--|:--|:--:|
| `Travis Game Assets/Hit Impact Effects` | Hits/Hit_01~04 프리팹(방사형 히트 버스트)+Guard 프리팹+`Pipeline Upgrade/Hit Impacts Effects FREE - URP.unitypackage`(**URP 변환 미적용**) | **공격 히트 임팩트 주력**(42차 P2 — 공격 모션 예시 재현) | 🔜 |
| `Matthew Guz/Hits Effects FREE` | BasicHit/BasicHit2/Fire Hit (현재 임팩트 소스 — Resources/FX/Impact/에 복사본 사용 중) | 피격 임팩트(기존), Travis 적용 후 보조 | ✅ 사용중 |
| `Free Slash VFX` | Slash VFX/Multiple Slashes (현재 스윙/십자가 소스) | 42차 P2에서 **스윙 용도 제거**(무기 트레일로 교체), 임팩트 마커 잔여 검토 | ⚠️ 교체 예정 |
| `Vefects/Invisible VFX URP` | 클로ak VFX + 루프 SFX 5종 + Linear Wipe URP 데모 프리팹 | **은신 모드 연출**(42차 P4) | 🔜 |
| `Vefects/Linear Wipe VFX HDRP` | 코인 라이저 와이프(골드) — **HDRP판**, URP판은 Invisible 폴더 데모 내 | **전리품 드롭/스폰 반짝임**(42차 P3) — URP 재질 호환 검증 필요 | 🔜 |
| `Hovl Studio/Magic effects pack` | 오라/캐릭터 오라/AoE/발사체 마법 FX (URP) | 훗날: 스킬/영주 연출/던전 이펙트 | 📦 대기 |
| `Vefects/Free Fire VFX URP` | 화염 FX | 훗날: 화염도마뱀/화염 스킬 | 📦 대기 |
| `GabrielAguiarProductions/FreeQuickEffectsVol1` | **unitypackage 3종 미추출**(BIRP/HDRP/URP) — 임포트 필요 | URP 패키지 임포트 후 히트/스윙 퀵이펙트 보조 후보 | ⬜ 미임포트 |
| `Free Slash VFX`(기존) | 위와 동일 | — | ✅ 사용중 |

## 2. 비이펙트 에셋

| 에셋 폴더 | 내용 | 용도 | 상태 |
|:--|:--|:--|:--:|
| `DoubleL` | RPG Animations Pack (FBX Unity/Unreal) | 병사/NPC 휴머노이드 애니 소스 | ✅ 사용중 |
| `Idyllic Fantasy Nature` | 지형/식생 텍스처 | 지형(이미 사용 중) | ✅ 사용중 |
| `플레이어 애니메이션` | 유저 제공 로코모션 FBX | 플레이어 애니(리타깃 소스) | ✅ 사용중 |

## 3. 운용 규칙
- 런타임 로드 에셋은 `Assets/Resources/FX/...`로 복사 후 `Resources.Load`(VFXResourceInstaller 선례 — 원본 폴더 유지, 복사본 사용).
- HDRP 전용 셰이더 자산은 URP 재질 호환 검증 후 적용(미호환 시 URP판 프리팹/재질로 대체).
- Pipeline Upgrade 패키지(URP 변환)는 임포트 후 Materials가 URP로 갱신되는지 확인.