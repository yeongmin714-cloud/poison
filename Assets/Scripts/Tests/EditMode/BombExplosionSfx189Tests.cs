using NUnit.Framework;
using UnityEngine;

namespace ProjectName.Tests.EditMode
{
    /// <summary>
    /// [189차] 폭탄 폭발 SFX 절차 합성 + 배선 계약 — 소스 문자열 어서션(BombVfxTests S() 관례).
    /// 배경: 폭탄 "불 소리"의 원인 — BombExplosionVisual.OnDisable이 Resources 프리팹 FX/Fire/VFX_Fire_01_Big를
    /// Instantiate할 때 프리팹에 묻어있는 AudioSource(불 후르릉음)가 함께 재생되는 것. 프로젝트에 폭발음
    /// 에셋이 부재하여 폭발음은 무음이었음.
    /// 계약:
    ///  ① 절차 합성 폭발음 WAV 디스크 실존 — Assets/Resources/Audio/SFX/SFX_Bomb_Explosion.wav.
    ///  ② 불 VFX 인스턴스에 묻은 AudioSource 차단 블록 — fire 인스턴스 범위 내 GetComponentsInChildren&lt;AudioSource&gt;(true) + Destroy.
    ///  ③ BombArmController.SpawnThrownBomb — bomb.explosionSound = null 제거, Resources.Load&lt;AudioClip&gt; 폭발음 로드로 교체.
    ///  ④ 동결 핀 — Bomb.Explode가 explosionSound를 PlayClipAtPoint로 재생하는 기존 계약 잔존.
    ///  ⑤ 동결 핀 — Molotov 화염 경로(Bomb.cs → BombVfx.PlayPrefab FireBigPath) 무수정(본 변경 무영향 확인).
    /// </summary>
    public sealed class BombExplosionSfx189Tests
    {
        private const string SrcBomb = "Assets/Scripts/Systems/Bomb.cs";
        private const string SrcBombArm = "Assets/Scripts/Systems/BombArmController.cs";
        private const string SrcBombVisual = "Assets/Scripts/Systems/BombExplosionVisual.cs";
        private const string SfxWavRel = "Assets/Resources/Audio/SFX/SFX_Bomb_Explosion.wav";

        private static string S(string path) => System.IO.File.ReadAllText(path);

        // ── ① 절차 합성 폭발음 WAV 디스크 실존 ──
        [Test]
        public void Explosion_Sfx_Wav_Exists_On_Disk()
        {
            Assert.That(System.IO.File.Exists(SfxWavRel), Is.True,
                "[189] 폭발 SFX WAV 실존(에디터 cwd=프로젝트 루트 기준): " + SfxWavRel);
            // 이중 확인 — Application.dataPath 기준(= "<프로젝트>/Assets", BombVfxTests §② 선례)
            string assets = Application.dataPath;
            Assert.That(System.IO.File.Exists($"{assets}/Resources/Audio/SFX/SFX_Bomb_Explosion.wav"), Is.True,
                "[189] 폭발 SFX WAV 실존(dataPath 기준) — Resources/Audio/SFX 하위");
        }

        // ── ② 불 VFX 인스턴스 범위 내 묻어있는 AudioSource 차단 블록 ──
        [Test]
        public void BombExplosionVisual_Disables_Embedded_FireAudio_In_Fire_Scope()
        {
            var src = S(SrcBombVisual);
            int fireIdx = src.IndexOf("var fire = GameObject.Instantiate(firePrefab", System.StringComparison.Ordinal);
            int destroyIdx = src.IndexOf("GameObject.Destroy(fire, FireLifetime)", System.StringComparison.Ordinal);
            Assert.That(fireIdx, Is.GreaterThanOrEqualTo(0), "[189] fire 인스턴스화 존재");
            Assert.That(destroyIdx, Is.GreaterThan(fireIdx), "[189] fire 수명 Destroy 위치 확인용");
            string fireScope = src.Substring(fireIdx, destroyIdx - fireIdx);
            Assert.That(fireScope, Does.Contain("GetComponentsInChildren<AudioSource>(true)"),
                "[189] 불 VFX 프리팹에 묻은 AudioSource 전수 탐색(비활성 포함) — fire 인스턴스 범위 내");
            Assert.That(fireScope, Does.Contain("Object.Destroy(a)"),
                "[189] 불 소리 차단 — 불 인스턴스에 붙은 AudioSource만 파괴(프리팹 에셋 무손상)");
        }

        // ── ③ SpawnThrownBomb 폭발음 배선 — null(무음) 제거 → 절차 합성 에셋 로드 ──
        [Test]
        public void BombArm_SpawnThrownBomb_Loads_Synthesized_Explosion_Sfx()
        {
            var src = S(SrcBombArm);
            Assert.That(src, Does.Not.Contain("bomb.explosionSound = null;"),
                "[189] 무음 배선 제거 — bomb.explosionSound = null 잔존 금지");
            Assert.That(src, Does.Contain("bomb.explosionSound = Resources.Load<AudioClip>(\"Audio/SFX/SFX_Bomb_Explosion\");"),
                "[189] 폭발 SFX 배선 — Resources.Load<AudioClip>(\"Audio/SFX/SFX_Bomb_Explosion\")");
        }

        // ── ④ 동결 핀 — Bomb.Explode의 PlayClipAtPoint 재생 계약 잔존 ──
        [Test]
        public void Bomb_Explode_Still_Plays_ExplosionSound_Via_PlayClipAtPoint()
        {
            var src = S(SrcBomb);
            int explodeIdx = src.IndexOf("private void Explode", System.StringComparison.Ordinal);
            int applyIdx = src.IndexOf("private void ApplyExplosiveDamage", System.StringComparison.Ordinal);
            Assert.That(explodeIdx, Is.GreaterThanOrEqualTo(0), "[189] Explode 메서드 존재");
            Assert.That(applyIdx, Is.GreaterThan(explodeIdx), "[189] ApplyExplosiveDamage 순서 확인용");
            string explodeBody = src.Substring(explodeIdx, applyIdx - explodeIdx);
            Assert.That(explodeBody, Does.Contain("AudioSource.PlayClipAtPoint(explosionSound, transform.position);"),
                "[189] 동결 핀 — 폭발음 재생 경로(AudioSource.PlayClipAtPoint) 잔존");
        }

        // ── ⑤ 동결 핀 — Molotov 화염 경로 무수정(불 소리 차단은 불 인스턴스 범위 한정) ──
        [Test]
        public void Molotov_Fire_Path_Untouched()
        {
            var bombSrc = S(SrcBomb);
            Assert.That(bombSrc, Does.Contain("BombVfx.PlayPrefab(BombVfx.FireBigPath, transform.position, 6f);"),
                "[189] 동결 핀 — Molotov 화염 영역 경로(BombVfx.PlayPrefab FireBigPath) 무수정");
            var visualSrc = S(SrcBombVisual);
            Assert.That(visualSrc, Does.Not.Contain("BombVfx."),
                "[189] Molotov 경로 무영향 — BombExplosionVisual은 BombVfx 공용 경로를 참조하지 않음");
        }
    }
}
