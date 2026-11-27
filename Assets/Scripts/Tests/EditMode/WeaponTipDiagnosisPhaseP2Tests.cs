using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;
using System.Collections.Generic;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [무번호 27-P2] 팁-끝 진단 오경보 수리 — WeaponEquipManager 소스 계약 잠금 테스트.
    /// 스펙: /tmp/p2_brief.md §2~§3 (ApplyBoundsGripAlignment 내 진단 블록).
    /// 구 판정(tipIsFar = handDistMax >= handDistMin)은 "칼끝=max 코너"를 암시 가정해서
    /// wood_spear(min 끝=칼끝, 실제 팁이 손에서 먼 쪽 1.08m)에서 ⚠뒤집힘 오경보를 냈다
    /// (11-12 Play 로그 실측, 선택 로직 자체는 정상).
    /// 수리 후 판정 = 실제 선택된 tipWorld의 손 거리 vs 메시 중심 손 거리 비교(선택값 직접 검증).
    /// 실행 판정은 QA 서브에이전트 증거로 확정(구현 서브에이전트는 실행하지 않음).
    /// </summary>
    public class WeaponTipDiagnosisPhaseP2Tests
    {
        private static string ReadWeapon() => System.IO.File.ReadAllText("Assets/Scripts/Systems/WeaponEquipManager.cs");

        // ===== T1: 수리 후 양성 판정기 =====

        [Test]
        public void Source_PositiveComparator()
        {
            string src = ReadWeapon();
            Assert.That(src, Does.Contain("bool tipIsFar = handDistTip >= handDistCenter;"),
                "[무번호 27-P2] 팁-끝 판정은 구 코너 비교(max>=min)가 아니라 실제 선택된 tipWorld 손 거리 vs 메시 중심 손 거리 비교로 수리되어야 한다.");
        }

        // ===== T2: 진단 로그에 팁/중심 실측 노출 =====

        [Test]
        public void Source_TipDistanceLogged()
        {
            string src = ReadWeapon();
            Assert.That(src, Does.Contain("팁={handDistTip:F2}m/중심={handDistCenter:F2}m"),
                "[무번호 27-P2] 팁-끝 진단 로그에 팁 거리/중심 거리 실측값이 출력되어야 한다 — 오경보 재발 시 원인 판독 재료.");
        }

        // ===== T3: 구 코너 비교 코드 부정 잠금 =====

        [Test]
        public void Source_OldCornerComparator_Removed()
        {
            string src = ReadWeapon();
            // [무번호 27-P2] 구 코너 비교 '코드' 형태만 부정 잠금한다.
            //   교체 후 블록의 [무번호 27-P2] 주석은 구판정 식을 서술적으로 인용하는데,
            //   주석 인용은 'bool ' 접두어가 없으므로 코드 형태와 무충돌 — 주석 서술은 회귀가 아니다.
            Assert.That(src, Does.Not.Contain("bool tipIsFar = handDistMax >= handDistMin"),
                "[무번호 27-P2] 구 코너 비교 코드(tipIsFar = handDistMax >= handDistMin)는 제거되어야 한다 — 주석의 서술적 인용은 허용.");
        }

        // ===== T4: 그립 테이블 원형 유지 =====

        [Test]
        public void Source_GripTableUntouched()
        {
            // [무번호 27-P2] wood_spear 테이블 항목 라인 단위 잠금 — 이번 수리로 _gripTable 플립 회귀 금지.
            //   DATA 모드에서 GripEnd는 사용되지 않지만(분기로 폴백 경로만 유발) 원값 유지가 계약.
            string spearLine = null;
            foreach (var line in System.IO.File.ReadLines("Assets/Scripts/Systems/WeaponEquipManager.cs"))
                if (line.Contains("\"wood_spear\"") && line.Contains("new GripPose"))
                    spearLine = line;
            Assert.That(spearLine, Is.Not.Null,
                "[무번호 27-P2] wood_spear 그립 테이블 항목(wood_spear + new GripPose)이 존재해야 한다.");
            Assert.That(spearLine, Does.Contain("GripEnd = -1, IdOverride = true, GripStrategy = 3, GripGuardFactor = 0.75f"),
                "[무번호 27-P2] wood_spear 라인의 GripEnd = -1, IdOverride = true, GripStrategy = 3, GripGuardFactor = 0.75f 원형 유지 — 테이블 플립 회귀 방지.");
        }

        // ===== T5: DATA 모드 분기 원형 유지 =====

        [Test]
        public void Source_DataModeBranchUnchanged()
        {
            string src = ReadWeapon();
            Assert.That(src, Does.Contain("pose.GripStrategy == 3 && ComputeWeaponDataGrip"),
                "[무번호 27-P2] DATA 모드 분기(pose.GripStrategy == 3 && ComputeWeaponDataGrip) 원형 유지 — 선택 로직은 정상이므로 변경 금지.");
        }

        // ===== T6: 수리 마커 =====

        [Test]
        public void Source_Marker()
        {
            string src = ReadWeapon();
            Assert.That(src, Does.Contain("[무번호 27-P2]"),
                "[무번호 27-P2] 수리 블록 마커([무번호 27-P2])가 WeaponEquipManager.cs에 존재해야 한다.");
        }
    }
}
