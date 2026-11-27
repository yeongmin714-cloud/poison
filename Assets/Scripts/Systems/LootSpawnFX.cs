using UnityEngine;

namespace ProjectName.Systems
{
    /// <summary>
    /// P3 전리품 스폰 반짝임 연출 (LootBasket.Create 훅용).
    /// [2026-10-15 최고사양 전환] 메인씬 실행 PC가 별도 고사양 데스크톱(사용자 고지)이라
    /// 저사양 보호 가드를 전면 제거했다:
    ///  - 0.1s 시간 쿨다운 삭제 → 동일 프레임+동일 위치 중복 제거만 유지(품질 제한 아닌 중복 제거)
    ///  - 합성 버스트 예산 캡(4/s) 삭제 — 전리품이 몰려도 전부 연출된다
    /// 스폰 1회당 합성: ①LootWipe(존재 시 우선) ②LootGold(Hovl Holy hit 5PS 멀티 레이어)
    /// ③LootSparks(직선 스파크) ④임팩트 더스트(FX/Death/DeathSmoke 재활용 0.25배 — 지면 충격 기준감)
    /// ⑤트리플 골드 링(0.8/1.4/2.2m 캐스케이드) ⑥골드 순간 라이트(Point 4m, 0.35s 페이드아웃, 섀도우 없음)
    /// - 46차 에셋-only 계약: 파티클은 전부 Hovl 에셋 프리팹. 링/라이트는 절차 지오메트리(금지 대상 아님).
    /// - 색은 에셋 고유값 유지(47차 규약) — 에셋 파티클 재색상 없음.
    /// </summary>
    public static class LootSpawnFX
    {
        const string ResourcePath = "FX/Loot/LootWipe";
        const string GoldPath = "FX/Loot/LootGold";
        const string SparksPath = "FX/Loot/LootSparks";
        const string DustPath = "FX/Death/DeathSmoke"; // 사망용 복사본 Smoke puff — 임팩트 더스트로 재활용
        const float LifetimeSeconds = 3f;
        const float WipeScale = 0.6f;
        const float DustScale = 0.25f; // 원본 startSize 2~4m/속도 15 — 0.25배로 지면 충격 감

        static bool _goldWarnedOnce;
        static bool _sparksWarnedOnce;
        static int _lastFrame = -1;
        static Vector3 _lastPos;

        /// <summary>
        /// InventoryWindow 리플렉션 호환 앨리어스 — GetMethod("Spawn")가 PlaySpawn을 찾도록.
        /// </summary>
        public static void Spawn(Vector3 position) => PlaySpawn(position);

        /// <summary>전리품 스폰 위치에서 골드 합성 연출을 재생합니다. 실패해도 게임 로직에 영향 없음.</summary>
        public static void PlaySpawn(Vector3 position)
        {
            // 동일 프레임+동일 위치 중복 제거 (시간 쿨다운 대체 — 서로 다른 전리품은 모두 연출됨)
            if (Time.frameCount == _lastFrame && (position - _lastPos).sqrMagnitude < 0.01f) return;
            _lastFrame = Time.frameCount;
            _lastPos = position;

            // ① URP Linear Wipe 계열 프리팹 (있으면 우선 — 원래 의도된 코인 와이프 룩)
            GameObject wipe = Resources.Load<GameObject>(ResourcePath);
            if (wipe != null)
            {
                GameObject fx = Object.Instantiate(wipe, position, Quaternion.identity);
                fx.transform.localScale = Vector3.one * WipeScale;
                AttachAutoDestroy(fx, LifetimeSeconds);
                return;
            }

            // ② 골드 버스트 (Hovl Holy hit — 5 PS: 플래시 0.4m + 대형 플래시 3m/0.1s +
            //    서클 2m/0.2s + 골드 점 방사 2단, startColor 자체가 골드 그라디언트) — 원본 스케일
            GameObject gold = Resources.Load<GameObject>(GoldPath);
            if (gold != null)
            {
                GameObject fx = Object.Instantiate(gold, position + Vector3.up * 0.4f, Quaternion.identity);
                PlayAllParticleSystems(fx);
                AttachAutoDestroy(fx, LifetimeSeconds);
            }
            else
            {
                WarnOnce(ref _goldWarnedOnce, GoldPath,
                    "Assets/Hovl Studio/Magic effects pack/Prefabs/Hits and explosions/Holy hit.prefab");
            }

            // ③ 액센트 직선 스파크 (Sparks explode yellow — 방사형 날카로움)
            GameObject sparks = Resources.Load<GameObject>(SparksPath);
            if (sparks != null)
            {
                GameObject fx = Object.Instantiate(sparks, position + Vector3.up * 0.4f, Quaternion.identity);
                PlayAllParticleSystems(fx);
                AttachAutoDestroy(fx, LifetimeSeconds);
            }
            else
            {
                WarnOnce(ref _sparksWarnedOnce, SparksPath,
                    "Assets/Hovl Studio/Magic effects pack/Prefabs/Sparks/Sparks explode yellow.prefab");
            }

            // ④ 임팩트 더스트 (Smoke puff 재활용 — 지면 충격 기준감, 0.25배)
            GameObject dust = Resources.Load<GameObject>(DustPath);
            if (dust != null)
            {
                GameObject fx = Object.Instantiate(dust, position + Vector3.up * 0.2f, Quaternion.identity);
                fx.transform.localScale = Vector3.one * DustScale;
                PlayAllParticleSystems(fx);
                AttachAutoDestroy(fx, LifetimeSeconds);
            }

            // ⑤ 트리플 골드 링 캐스케이드 — 빠른 내부 → 느린 외곽 (보상 펄스)
            SpawnGoldRing(position, 0.8f, 0.4f, new Color(1f, 0.85f, 0.35f));
            SpawnGoldRing(position, 1.4f, 0.7f, new Color(1f, 0.88f, 0.45f));
            SpawnGoldRing(position, 2.2f, 1.0f, new Color(1f, 0.92f, 0.55f));

            // ⑥ 골드 순간 라이트 — 지면 밝힘, 0.35s 페이드아웃 (단명이라 섀도우 없음)
            SpawnGoldLight(position);
        }

        static void WarnOnce(ref bool flag, string path, string sourceHint)
        {
            if (flag) return;
            flag = true;
            Debug.LogWarning($"[LootSpawnFX] Resources/{path} 없음 — 해당 레이어 생략. '{sourceHint}' 복사하면 복구.");
        }

        /// <summary>골드 확장 링 1개 (ShockwaveRingFX — 지면 근처 발화, per-spawn 머티리얼 인스턴스).</summary>
        static void SpawnGoldRing(Vector3 position, float radius, float duration, Color color)
        {
            ShockwaveRingFX.Spawn(position + Vector3.up * 0.1f, radius, color, duration);
        }

        /// <summary>골드 순간 라이트 — Point 4m/강도 2.2 → 0.35s 선형 페이드아웃 후 자가 파괴.</summary>
        static void SpawnGoldLight(Vector3 position)
        {
            var go = new GameObject("LootGoldLight");
            go.transform.position = position + Vector3.up * 0.5f;
            var runner = go.AddComponent<GoldLightRunner>();
            runner.Init(2.2f, 0.35f);
        }

        /// <summary>일부 팩 프리팹은 자체 재생 안 함 → 전체 ParticleSystem 명시 재생(SlashVFXRunner 선례).
        /// [80차] 전리품 합성은 "스폰 1회 버스트" 계약 — Hovl 복사본의 looping:1(LootGold 5PS 전부 + LootSparks
        /// + DeathSmoke 더스트)을 재생 직전에 강제 해제한다. (구: 루프 유지 → AutoDestroy의 IsAlive(true)가 영원히
        /// true → 파괴 누락 → 전리품 위치에서 골드 파티클 무한 재생 — 2026-10-16 영상 실측.)</summary>
        static void PlayAllParticleSystems(GameObject instance)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in systems)
            {
                if (ps == null) continue;
                var main = ps.main;        // MainModule은 래퍼 구조체 — 필드 수정이 즉시 PS에 반영(재대입 불필요·불가)
                main.loop = false;         // [80차] 원샷 강제 — 에셋 파일 무변경(46차 계약)
                ps.Play(true);
            }
        }

        static void AttachAutoDestroy(GameObject go, float seconds)
        {
            if (go.GetComponent<AutoDestroy>() == null)
            {
                var ad = go.AddComponent<AutoDestroy>();
                ad.remaining = seconds;
            }
        }

        /// <summary>골드 라이트 러너 — 강도 선형 페이드 후 자가 파괴 (transient GO + nested Runner 관례).</summary>
        sealed class GoldLightRunner : MonoBehaviour
        {
            Light _light;
            float _baseIntensity;
            float _duration;
            float _t;

            public void Init(float baseIntensity, float duration)
            {
                _light = gameObject.AddComponent<Light>();
                _light.type = LightType.Point;
                _light.color = new Color(1f, 0.85f, 0.4f);
                _light.range = 4f;
                _light.intensity = baseIntensity;
                _light.shadows = LightShadows.None; // 0.35s 단명 — 섀도우 비용 낭비 방지
                _baseIntensity = baseIntensity;
                _duration = duration;
                _t = 0f;
            }

            void Update()
            {
                _t += Time.deltaTime;
                if (_light != null)
                    _light.intensity = Mathf.Lerp(_baseIntensity, 0f, Mathf.Clamp01(_t / _duration));
                if (_t >= _duration) Destroy(gameObject);
            }
        }

        /// <summary>자가 파괴 (static 진입점에서 코루틴 불가) — 파티클 잔여분 소진 대기 포함.
        /// [80차] 절대 상한 추가 — 소진 대기 반복이 무한히 연장되는 것을 차단(loop 강제 해제의 이중 안전장치).</summary>
        sealed class AutoDestroy : MonoBehaviour
        {
            public float remaining = 3f;
            private const float MaxTotalWait = 8f;   // 절대 상한 — IsAlive 대기 반복 무한 방지
            private float _total;

            void Update()
            {
                remaining -= Time.deltaTime;
                _total += Time.deltaTime;
                if (remaining > 0f && _total < MaxTotalWait) return;
                var ps = GetComponent<ParticleSystem>();
                if (ps != null && ps.IsAlive(true) && _total < MaxTotalWait)
                {
                    remaining = 0.5f; // 파티클 잔여분 소진 대기
                    return;
                }
                Destroy(gameObject);
            }
        }
    }
}
