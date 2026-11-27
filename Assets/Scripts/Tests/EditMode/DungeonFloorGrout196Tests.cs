using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [196차] 던전 바닥 타일 그루트 lip z-fighting 수술 계약 — floor_grout_zfix196.py 적용 결과 고정.
    /// 근원(진단 확정): Floor_1 6변형 중 5종(3A 제외)이 주 상면 +0.4~0.8mm 위에 lip 상단 캡 평면을 추가 보유 →
    /// 중거리 깊이버퍼 정밀도 내 z-fighting → 이동 중 줄눈 반짝임(사용자 보고 "맨바닥 타일이 반짝반짝").
    /// 수리: 근접 코플래너 상향 삼각형 인덱스 퇴화(3개 동일 값 — GPU 폐기) — 바이트 레이아웃·COLOR_0(AO 베이크) 보존.
    /// 판정 방식: 현재 GLB md5 ≠ 백업 md5(수술 적용 증명) + 백업/매니페스트 롤백 계약. 기하 재검증은
    /// Tools/floor_grout_zfix196.py 자체 재파싱(근접 상향 그룹 잔존 0)과 196차 QAPROGRESS 실측으로 이원화.
    /// </summary>
    public class DungeonFloorGrout196Tests
    {
        const string GlbDir = "Assets/Resources/Models/Dungeon";
        const string BackupDir = "Tools/ao_prebake_backup/DungeonFloorGrout196";
        static readonly string[] Operated = { "Floor_1_1A.glb", "Floor_1_2A.glb", "Floor_1_4A.glb", "Floor_1_5A.glb", "Floor_1_6A.glb" };
        const string Untouched = "Floor_1_3A.glb";

        static string Md5(string path)
        {
            using (var md5 = System.Security.Cryptography.MD5.Create())
            using (var stream = File.OpenRead(path))
                return System.BitConverter.ToString(md5.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [Test]
        public void G1_OperatedGlbs_DifferFromBackup()
        {
            Assert.That(Directory.Exists(BackupDir), Is.True, "[196차] 수술 원본 백업 디렉터리 존재.");
            foreach (var name in Operated)
            {
                Assert.That(File.Exists($"{GlbDir}/{name}"), Is.True, $"GLB 존재: {name}");
                Assert.That(File.Exists($"{BackupDir}/{name}"), Is.True, $"[196차] 백업 존재: {name}");
                Assert.That(Md5($"{GlbDir}/{name}"), Is.Not.EqualTo(Md5($"{BackupDir}/{name}")),
                    $"[196차] {name} 수술 미적용 — 현재 GLB가 백업과 동일함(퇴화 처리 전 상태).");
            }
        }

        [Test]
        public void G2_Untouched3A_BackupAbsent()
        {
            Assert.That(File.Exists($"{GlbDir}/{Untouched}"), Is.True, "GLB 존재: " + Untouched);
            Assert.That(File.Exists($"{BackupDir}/{Untouched}"), Is.False,
                "[196차] 3A는 근접 코플래너 그룹이 없어 무수술 대상 — 백업(=수술 흔적) 부재 계약.");
        }

        [Test]
        public void G3_Manifest_Registered()
        {
            string manifest = File.ReadAllText("Tools/ao_bake_manifest.json");
            Assert.That(manifest, Does.Contain("DungeonFloorGrout196"),
                "[196차] 매니페스트 키 등록 — 롤백/재수술 추적 계약.");
        }

        [Test]
        public void G4_SurgeryTool_Contract()
        {
            string src = File.ReadAllText("Tools/floor_grout_zfix196.py");
            Assert.That(src, Does.Contain("WIN_LO, WIN_HI = 0.0005, 0.002"),
                "[196차] 근접 코플래너 판정 창(0.5~2.0mm) 상수 계약 — 주면 보존 하한.");
            Assert.That(src, Does.Contain("snap_ys[vi] = main_y"),
                "[196차] v2 스냅 경로 — 근접면 삭제 없이 주면 높이로 스냅(구멍 방지) 계약.");
            Assert.That(src, Does.Contain("struct.pack(\"<HHH\", same, same, same)"),
                "[196차] 겹침 lip 퇴화 = 인덱스 3개 동일 값(GPU 폐기) — 바이트 레이아웃 무변경 계약.");
        }
    }
}
