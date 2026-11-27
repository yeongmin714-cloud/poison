using System.Collections.Generic;
using UnityEngine;
using ProjectName.Core.Utils;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A3] 이계 오브 — VS식 재화 오브 (XP/골드) (AFTERLIFE_SURVIVAL_PLAN §A3-1).
    /// - 웨이브 적 사망 → 결정론 구성 드랍(GetLootComposition — 순수 수학, EditMode 검증)
    /// - 자석 반경 진입 → 가속 흡수 → AfterlifeSurvivalSystem.NotifyOrbAbsorbed 집계(귀환 시 이관)
    /// - 3택1 증강 대기(IsPending) 중 드리프트/흡수 정지 — 아레나 전체 정지와 동기
    /// - 시각: 발광 구 (골드=금색 / XP=연보라) — AfterlifeEnemy 눈빛 동일 Emissive 패턴,
    ///   소재는 2개 정적 공유(오브별 인스턴스화 누수 없음 — 75차 renderer.material 함정 회피)
    /// - 오브 상한 초과분은 즉시 자동 정산(스폰 폭발 가드 — 승률/성능 우선)
    /// </summary>
    public class AfterlifeOrb : MonoBehaviour
    {
        public enum OrbType { XP, Gold }

        // ===== 상수 =====
        public const float BaseAbsorbRadius = 3.5f;   // 자석 반경 기본 (증강 ×1.5까지)
        public const float AbsorbDistance = 0.9f;     // 흡수 판정 거리
        public const int MaxOrbCount = 80;            // 상한 — 초과 드랍분은 즉시 자동 정산
        public const float OrbHeight = 0.7f;          // 부유 높이 (아레나 바닥 상판 +0.1 위)

        private static readonly List<AfterlifeOrb> _live = new List<AfterlifeOrb>();
        private static Transform _player;
        private static bool _playerSearched;
        private static bool _runActive;
        private static Material _goldMat;
        private static Material _xpMat;

        /// <summary>현재 자석 반경 — 증강(흡수의 인장)이 갱신, 런 시작/종료 시 기본값 리셋.</summary>
        public static float MagnetRadius { get; set; } = BaseAbsorbRadius;
        public static int LiveCount => _live.Count;
        /// <summary>[156차 P5] 활성 오브 읽기 전용 뷰 — 흡혈 촉수(가장 먼 오브) 판정용.</summary>
        public static System.Collections.Generic.IReadOnlyList<AfterlifeOrb> Live => _live;

        private OrbType _type;
        private int _value;
        private float _seed;
        private float _magnetSpeed;
        private Vector3 _homePos;
        private bool _magnetized;

        // ===== 결정론 수확 구성 (순수 — EditMode 검증) =====

        /// <summary>
        /// 레벨별 드랍 구성 — 골드 2오브 + XP 3오브. 합계 = 레벨 산식 총합과 정확히 일치(결정론).
        /// 골드 총합 = RUN_GOLD_PER_KILL + level×2 / XP 총합 = 8 + level×4.
        /// </summary>
        public static void GetLootComposition(int level, List<int> goldValues, List<int> xpValues)
        {
            goldValues.Clear();
            xpValues.Clear();

            int goldTotal = AfterlifeSurvivalSystem.RUN_GOLD_PER_KILL + level * 2;
            int g1 = Mathf.Max(1, goldTotal / 2);
            goldValues.Add(g1);
            goldValues.Add(Mathf.Max(1, goldTotal - g1));

            int xpTotal = 8 + level * 4;
            int a = Mathf.CeilToInt(xpTotal * 0.40f);
            int b = Mathf.CeilToInt((xpTotal - a) * 0.50f);
            int c = Mathf.Max(1, xpTotal - a - b);
            xpValues.Add(a);
            xpValues.Add(b);
            xpValues.Add(c);
        }

        // ===== 런 라이프사이클 =====

        /// <summary>런 시작 — 오브 전멸 + 플레이어 참조 캡처 + 자석 반경 기본 리셋.</summary>
        public static void BeginRun(Transform player)
        {
            ClearAll();
            _player = player;
            _playerSearched = player != null;
            _runActive = true;
            MagnetRadius = BaseAbsorbRadius;
        }

        /// <summary>런 종료/리셋 — 잔존 오브 전멸(편집 모드 DestroyImmediate 게이트 — 75차 함정).</summary>
        public static void ClearAll()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var orb = _live[i];
                if (orb != null)
                {
                    if (Application.isPlaying) Destroy(orb.gameObject);
                    else DestroyImmediate(orb.gameObject);
                }
            }
            _live.Clear();
            _runActive = false;
            _player = null;
            _playerSearched = false;
            MagnetRadius = BaseAbsorbRadius;
        }

        // ===== 드랍 =====

        /// <summary>적 사망 드랍 — 결정론 구성(레벨) + 황금각 링 배치(무작위 없음).</summary>
        public static void DropLoot(Vector3 position, int level)
        {
            if (!_runActive) return;

            var goldValues = new List<int>();
            var xpValues = new List<int>();
            GetLootComposition(level, goldValues, xpValues);

            int slot = 0;
            foreach (var v in goldValues) SpawnOrb(OrbType.Gold, v, position, slot++);
            foreach (var v in xpValues) SpawnOrb(OrbType.XP, v, position, slot++);
        }

        private static void SpawnOrb(OrbType type, int value, Vector3 basePos, int slot)
        {
            if (_live.Count >= MaxOrbCount)
            {
                // 상한 가드 — 오브 생성 대신 즉시 자동 정산 (동일 수확, 성능 보호)
                AfterlifeSurvivalSystem.NotifyOrbAbsorbed(type, value);
                return;
            }

            // 황금각 링 — 결정론 배치 (시드/난수 불사용)
            float angle = slot * 2.399963f;
            float r = 0.4f + slot * 0.13f;
            var go = new GameObject(type == OrbType.Gold ? "AfterlifeOrb_Gold" : "AfterlifeOrb_XP");
            var pos = basePos;
            pos.x += Mathf.Cos(angle) * r;
            pos.z += Mathf.Sin(angle) * r;
            pos.y = OrbHeight;
            go.transform.position = pos;

            var orb = go.AddComponent<AfterlifeOrb>();
            orb._type = type;
            orb._value = Mathf.Max(1, value);
            orb._seed = slot * 1.7f + (type == OrbType.Gold ? 0f : 0.9f); // 시각 전용 — 결정론
            orb._homePos = pos;
            orb.BuildVisual(type);
            _live.Add(orb);
        }

        private void Awake()
        {
            _live.Add(this);
        }

        private void BuildVisual(OrbType type)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphere.transform.SetParent(transform, false);
            sphere.transform.localPosition = Vector3.zero;
            sphere.transform.localScale = Vector3.one * (type == OrbType.Gold ? 0.22f : 0.26f);
            var col = sphere.GetComponent<Collider>();
            if (col != null) Destroy(col); // 런타임 전용 경로 — 편집 모드 도달 없음(DropLoot 게이트)

            var renderer = sphere.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                var mat = GetSharedMaterial(type);
                if (mat != null) renderer.sharedMaterial = mat;
            }
        }

        private static Material GetSharedMaterial(OrbType type)
        {
            if (_goldMat == null)
            {
                _goldMat = MaterialHelper.CreateLitMaterial(new Color(1f, 0.82f, 0.25f), "AfterlifeOrb_GoldMat");
                if (_goldMat.HasProperty("_EmissionColor"))
                {
                    _goldMat.SetColor("_EmissionColor", new Color(1f, 0.82f, 0.25f) * 1.6f);
                    _goldMat.EnableKeyword("_EMISSION");
                }
                _xpMat = MaterialHelper.CreateLitMaterial(new Color(0.62f, 0.45f, 1f), "AfterlifeOrb_XPMat");
                if (_xpMat.HasProperty("_EmissionColor"))
                {
                    _xpMat.SetColor("_EmissionColor", new Color(0.62f, 0.45f, 1f) * 1.6f);
                    _xpMat.EnableKeyword("_EMISSION");
                }
            }
            return type == OrbType.Gold ? _goldMat : _xpMat;
        }

        // ===== 자석/흡수 =====

        private void Update()
        {
            if (!_runActive) return;
            if (AfterlifeAugmentSystem.IsPending) return; // 3택1 대기 — 아레나 정지 동기

            if (_player == null && !_playerSearched)
            {
                _playerSearched = true;
                var playerGo = GameObject.FindGameObjectWithTag("Player");
                if (playerGo != null) _player = playerGo.transform;
            }
            if (_player == null) return;

            var chest = _player.position + Vector3.up * 0.8f;
            Vector3 toPlayer = chest - transform.position;
            float dist = toPlayer.magnitude;

            if (dist > MagnetRadius)
            {
                // 자유 부유 — 밥(수직 미동) + 자전 (시각)
                _magnetSpeed = 0f;
                _magnetized = false;
                var bob = _homePos;
                bob.y += Mathf.Sin((Time.time + _seed) * 3f) * 0.08f;
                transform.position = bob;
                transform.rotation = Quaternion.Euler(0f, (Time.time + _seed) * 90f, 0f);
                return;
            }

            if (!_magnetized)
            {
                _magnetized = true;
                _magnetSpeed = 4f;
            }
            _magnetSpeed = Mathf.MoveTowards(_magnetSpeed, 18f, 26f * Time.deltaTime);
            if (dist > 0.001f)
                transform.position += toPlayer / dist * _magnetSpeed * Time.deltaTime;

            if (dist <= AbsorbDistance)
            {
                AfterlifeSurvivalSystem.NotifyOrbAbsorbed(_type, _value);
                RemoveOrb(this);
            }
        }

        private static void RemoveOrb(AfterlifeOrb orb)
        {
            _live.Remove(orb);
            if (Application.isPlaying) Destroy(orb.gameObject);
            else DestroyImmediate(orb.gameObject);
        }
    }
}
