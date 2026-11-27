using System.Reflection;
using NUnit.Framework;
using ProjectName.Systems;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [95차 후속 SFX] 패링 실오디오 도입 계약 — 절차 합성 WAV(모달 신디시스) + SoundEffectManager 실재생 경로.
    /// 스펙 문맥: docs/95_PARRY_ACTION_QUALITY_PLAN.md (95차 패링 강화 완료 상태 — 체감 병목이 SFX 부재).
    /// 검증 축: ①SFXType 패링 케이스 2종+GetSFXName(기존 16타입 무손상) ②Resources 경로/캐시 로드 코드
    /// ③PlaySFX 실재생 분기(clip 존재 시)+무해 폴백 유지 ④TryParry 연결(흡수점/등급별/isPlaying 가드)
    /// ⑤WAV 파일 디스크 실측 존재(5종, 44.1kHz/16bit/mono 헤더 실측). 기존 계약 무손상.
    /// </summary>
    public class ParrySfxAudioTests
    {
        private GameObject _player;
        private PlayerCombat _combat;

        [SetUp]
        public void SetUp()
        {
            _player = new GameObject("ParrySfxAudioTestPlayer");
            _combat = _player.AddComponent<PlayerCombat>();
            ResetSoundEffectManagerStatics();
        }

        [TearDown]
        public void TearDown()
        {
            ResetSoundEffectManagerStatics();
            if (_player != null) Object.DestroyImmediate(_player);
        }

        // ===== [95차 후속 SFX] ① SFXType 패링 케이스 2종 + GetSFXName 매핑 — 기존 16타입 무손상 =====

        [Test]
        public void SfxType_ParryGradeCases_MappedAndLegacySixteenTypesIntact()
        {
            Assert.That(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Parry_Perfect"), Is.True,
                "[95차 후속 SFX] SFXType.Parry_Perfect 케이스 존재");
            Assert.That(System.Enum.IsDefined(typeof(SoundEffectManager.SFXType), "Parry_Good"), Is.True,
                "[95차 후속 SFX] SFXType.Parry_Good 케이스 존재");

            Assert.That(SoundEffectManager.GetSFXName(SoundEffectManager.SFXType.Parry_Perfect),
                Is.EqualTo("SFX_Parry_Perfect"), "[95차 후속 SFX] PERFECT → SFX_Parry_Perfect 매핑(WAV 파일명 계약)");
            Assert.That(SoundEffectManager.GetSFXName(SoundEffectManager.SFXType.Parry_Good),
                Is.EqualTo("SFX_Parry_Good"), "[95차 후속 SFX] GOOD → SFX_Parry_Good 매핑(WAV 파일명 계약)");

            // 기존 16타입 무손상 — 신규 2종 추가는 사후 부첨(기존 멤버/이름 계약 유지)
            var legacy = new (SoundEffectManager.SFXType type, string name)[]
            {
                (SoundEffectManager.SFXType.Footstep, "SFX_Footstep"),
                (SoundEffectManager.SFXType.Gather, "SFX_Gather"),
                (SoundEffectManager.SFXType.Craft, "SFX_Craft"),
                (SoundEffectManager.SFXType.Combat_Hit, "SFX_Combat_Hit"),
                (SoundEffectManager.SFXType.Combat_Swing, "SFX_Combat_Swing"),
                (SoundEffectManager.SFXType.Assassination, "SFX_Assassination"),
                (SoundEffectManager.SFXType.DoorOpen, "SFX_DoorOpen"),
                (SoundEffectManager.SFXType.DoorClose, "SFX_DoorClose"),
                (SoundEffectManager.SFXType.ItemPickup, "SFX_ItemPickup"),
                (SoundEffectManager.SFXType.ItemDrop, "SFX_ItemDrop"),
                (SoundEffectManager.SFXType.Alarm, "SFX_Alarm"),
                (SoundEffectManager.SFXType.Victory, "SFX_Victory"),
                (SoundEffectManager.SFXType.Defeat, "SFX_Defeat"),
                (SoundEffectManager.SFXType.AutoMove_Start, "SFX_AutoMove_Start"),
                (SoundEffectManager.SFXType.AutoMove_Complete, "SFX_AutoMove_Complete"),
                (SoundEffectManager.SFXType.AutoMove_Cancel, "SFX_AutoMove_Cancel"),
            };
            Assert.That(legacy.Length, Is.EqualTo(16), "기존 SFXType 계약 — 16종");
            foreach (var (type, name) in legacy)
                Assert.That(SoundEffectManager.GetSFXName(type), Is.EqualTo(name),
                    $"기존 16타입 무손상 — {type}");
        }

        // ===== [95차 후속 SFX] ② Resources 경로 / 캐시 로드 코드 =====

        [Test]
        public void SoundEffectManager_LoadsClipsViaCachedResourcesPath()
        {
            var src = SoundEffectManagerSource();
            Assert.That(src, Does.Contain("Resources.Load<AudioClip>($\"Audio/SFX/{GetSFXName(type)}\")"),
                "[95차 후속 SFX] Resources/Audio/SFX/{GetSFXName(type)} 클립 로드 경로");
            Assert.That(src, Does.Contain("Dictionary<SFXType, AudioClip>"),
                "[95차 후속 SFX] 클립 캐시 딕셔너리(1회 로드)");
            Assert.That(src, Does.Contain("_clipCache.TryGetValue"),
                "[95차 후속 SFX] 캐시 히트 우선 — 반복 로드 방지");
            Assert.That(src, Does.Contain("_clipCache[type] = clip"),
                "[95차 후속 SFX] 로드 결과 캐시 저장");
            Assert.That(src, Does.Contain("[95차 후속 SFX]"),
                "[95차 후속 SFX] 마커 존재");
        }

        // ===== [95차 후속 SFX] ③ PlaySFX 실재생 분기(clip 존재 시) + 무해 폴백 유지 =====

        [Test]
        public void PlaySfx_RealPlaybackBranchWhenClipExists_KeepsHarmlessMissingClipFallback()
        {
            var src = SoundEffectManagerSource();
            // 실재생 분기 — PlaySFX(2D)/PlaySFXAtPoint(3D) 양쪽 모두 clip != null → source.clip = clip 후 Play
            Assert.That(CountOccurrences(src, "source.clip = clip;"), Is.GreaterThanOrEqualTo(2),
                "[95차 후속 SFX] PlaySFX/PlaySFXAtPoint 실재생 분기(source.clip = clip)");
            Assert.That(CountOccurrences(src, "LoadCachedClip(type)"), Is.GreaterThanOrEqualTo(2),
                "[95차 후속 SFX] 양쪽 재생 경로 모두 캐시 로드 사용");
            // 무해 폴백 — 클립 부재 시 기존 플레이스홀더 로그/null 클립 경로 유지
            Assert.That(src, Does.Contain("source.clip = null;"),
                "[95차 후속 SFX] 클립 부재 폴백(null 클립) 유지");
            // [133차-P2] 계약 진화 — 실재화 완료로 로그 문구만 실상화("클립 부재 폴백"). 폴백 동작 자체는 유지.
            Assert.That(src, Does.Contain("클립 부재 폴백"),
                "[133차-P2] 클립 부재 폴백 경로 유지(로그 문구 실상화 — 플레이스홀더 주장 소멸)");
        }

        // ===== [95차 후속 SFX] ④ TryParry 연결 — 흡수점에서 등급별 PlaySFXAtPoint + isPlaying 가드 =====

        [Test]
        public void PlayerCombat_TryParry_WiresGradeBasedParrySfxAtAbsorbPoint()
        {
            var src = PlayerCombatSource();
            Assert.That(src, Does.Contain("[95차 후속 SFX] 패링 실오디오"),
                "[95차 후속 SFX] 패링 연결 마커");
            Assert.That(src, Does.Contain("SoundEffectManager.SFXType.Parry_Perfect"),
                "[95차 후속 SFX] PERFECT → SFXType.Parry_Perfect");
            Assert.That(src, Does.Contain("SoundEffectManager.SFXType.Parry_Good"),
                "[95차 후속 SFX] GOOD → SFXType.Parry_Good");
            Assert.That(src, Does.Contain("PlaySFXAtPoint(parrySfxType, p)"),
                "[95차 후속 SFX] 흡수점(p = 전방 1m + up 0.9)에서 PlaySFXAtPoint");
            Assert.That(src, Does.Contain("Application.isPlaying"),
                "[95차 후속 SFX] Application.isPlaying 가드 — EditMode 안전(기존 패턴)");
        }

        // ===== [95차 후속 SFX] ⑤ WAV 파일 디스크 실측(5종) — 44.1kHz/16bit/mono 헤더 실측 =====

        [Test]
        public void ParrySfxWavs_ExistOnDisk_WithContractFormat()
        {
            string dir = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Resources", "Audio", "SFX");
            Assert.That(System.IO.Directory.Exists(dir), Is.True,
                "[95차 후속 SFX] Assets/Resources/Audio/SFX 폴더 존재");

            // [171차] 패막 클립 3종 = Kenney CC0 ogg — OggS 매직 실측(구 wav는 Retired/ 이격).
            string[] parryNames =
            {
                "SFX_Parry_Perfect", "SFX_Parry_Good", "SFX_Parry_Fail",
            };
            foreach (string name in parryNames)
            {
                string path = System.IO.Path.Combine(dir, name + ".ogg");
                Assert.That(System.IO.File.Exists(path), Is.True,
                    $"[171차] {name}.ogg 디스크 실측 존재");
                byte[] b = System.IO.File.ReadAllBytes(path);
                Assert.That(System.Text.Encoding.ASCII.GetString(b, 0, 4), Is.EqualTo("OggS"),
                    $"{name}: OggS 헤더");
                Assert.That(b.Length, Is.GreaterThan(1000), $"{name}: 유효 길이(빈 파일 아님)");
            }

            // [95차 후속 SFX] 기존 전투 wav 3종 — 44.1kHz/16bit/mono RIFF 실측 유지.
            string[] wavNames =
            {
                "SFX_Combat_Swing", "SFX_Combat_Hit", "SFX_Footstep",
            };
            foreach (string name in wavNames)
            {
                string path = System.IO.Path.Combine(dir, name + ".wav");
                Assert.That(System.IO.File.Exists(path), Is.True,
                    $"[95차 후속 SFX] {name}.wav 디스크 실측 존재");
                byte[] b = System.IO.File.ReadAllBytes(path);
                Assert.That(System.Text.Encoding.ASCII.GetString(b, 0, 4), Is.EqualTo("RIFF"),
                    $"{name}: RIFF 헤더");
                Assert.That(System.Text.Encoding.ASCII.GetString(b, 8, 4), Is.EqualTo("WAVE"),
                    $"{name}: WAVE 포맷");
                int channels = b[22] | (b[23] << 8);
                int sampleRate = b[24] | (b[25] << 8) | (b[26] << 16) | (b[27] << 24);
                int bitsPerSample = b[34] | (b[35] << 8);
                Assert.That(channels, Is.EqualTo(1), $"{name}: mono");
                Assert.That(sampleRate, Is.EqualTo(44100), $"{name}: 44.1kHz");
                Assert.That(bitsPerSample, Is.EqualTo(16), $"{name}: 16bit PCM");
                Assert.That(b.Length, Is.GreaterThan(1000), $"{name}: 유효 길이(빈 파일 아님)");
            }
        }

        // ===== [95차 후속 SFX] EditMode 안전 동작 실측 — TryParry가 매니저 인스턴스를 생성하지 않는다 =====

        [Test]
        public void TryParry_InEditMode_SucceedsWithoutInstantiatingSoundEffectManager()
        {
            Assert.That(RequestParry(), Is.True);
            Assert.That(_combat.TryParry(), Is.True, "기존 계약 — EditMode에서도 TryParry 상태 소비 성공");
            Assert.That(GetSoundEffectManagerInstance(), Is.Null,
                "[95차 후속 SFX] EditMode 가드 — TryParry가 SoundEffectManager 인스턴스를 생성하지 않는다(기존 플레이스홀더 계약 보존)");
        }

        // ===== 헬퍼 =====

        private static string SoundEffectManagerSource()
        {
            var sep = System.IO.Path.DirectorySeparatorChar.ToString();
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Scripts" + sep + "Systems" + sep + "SoundEffectManager.cs");
            return System.IO.File.ReadAllText(path);
        }

        private static string PlayerCombatSource()
        {
            var sep = System.IO.Path.DirectorySeparatorChar.ToString();
            var path = System.IO.Path.Combine(UnityEngine.Application.dataPath,
                "Scripts" + sep + "Systems" + sep + "PlayerCombat.cs");
            return System.IO.File.ReadAllText(path);
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        private static void ResetSoundEffectManagerStatics()
        {
            var instanceField = typeof(SoundEffectManager).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            var quittingField = typeof(SoundEffectManager).GetField("_instanceQuitting",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (instanceField != null) instanceField.SetValue(null, null);
            if (quittingField != null) quittingField.SetValue(null, false);
        }

        private static object GetSoundEffectManagerInstance()
        {
            var instanceField = typeof(SoundEffectManager).GetField("_instance",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(instanceField, Is.Not.Null, "SoundEffectManager._instance 정적 필드 존재");
            return instanceField.GetValue(null);
        }

        private bool RequestParry()
        {
            var method = typeof(PlayerCombat).GetMethod("TryStartParry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, "패링 전용 핸들러 TryStartParry 존재");
            return (bool)method.Invoke(_combat, null);
        }
    }
}