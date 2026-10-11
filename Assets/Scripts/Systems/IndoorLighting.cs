using System.Collections;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// C11-05: 실내 조명 시스템.
    /// 방(Room)에 어두운 앰비언트 라이트 + 천장 중앙 Point Light 배치.
    /// 선택적 깜빡임 효과 지원.
    /// </summary>
    public static class IndoorLighting
    {
        // ===== 상수 =====
        private const float DEFAULT_LIGHT_RANGE = 15f;
        private const float DEFAULT_LIGHT_INTENSITY = 1.2f;
        private const float CEILING_OFFSET = 0.2f;
        private const float FLICKER_SPEED = 2.5f;
        private const float FLICKER_MIN_MULT = 0.8f;
        private const float FLICKER_MAX_MULT = 1.2f;
        private static readonly Color DEFAULT_LIGHT_COLOR = new Color(1f, 0.95f, 0.8f);

        /// <summary>
        /// 실내 조명 설정: 앰비언트 어둡게 + 천장 중앙 Point Light.
        /// </summary>
        /// <param name="room">방 GameObject (자식으로 Light 추가)</param>
        /// <param name="ambientColor">앰비언트 색상</param>
        /// <param name="ambientIntensity">앰비언트 강도 (0~1)</param>
        /// <param name="flicker">깜빡임 효과 활성화 여부</param>
        public static void SetupIndoorLighting(GameObject room, Color ambientColor, float ambientIntensity, bool flicker = false, float centralLightIntensity = DEFAULT_LIGHT_INTENSITY, float centralLightRange = -1f)
        {
            if (room == null)
            {
                Debug.LogError("[IndoorLighting] room이 null입니다.");
                return;
            }

            // 앰비언트 라이트를 어둡게 설정
            // NOTE: URP에서 RenderSettings.ambientMode가 Skybox인 경우 이 설정은 효과가 없습니다.
            // 실내 씬에서는 Lighting 창에서 Ambient Mode를 Color/Trilight로 설정하거나
            // Volume Override를 통해 Ambient Probe를 제어해야 정상 동작합니다.
            RenderSettings.ambientLight = new Color(
                ambientColor.r * ambientIntensity,
                ambientColor.g * ambientIntensity,
                ambientColor.b * ambientIntensity
            );

            // 방 크기 추정 (자식 MeshFilter들의 bounds로, 월드 공간 정확 변환)
            Bounds worldBounds = CalculateWorldBounds(room);
            if (worldBounds.size == Vector3.zero)
            {
                Debug.LogWarning($"[IndoorLighting] {room.name}: MeshFilter를 찾을 수 없어 기본 위치에 조명을 배치합니다.");
                worldBounds = new Bounds(room.transform.position, Vector3.one * 5f);
            }

            // 방 중앙 천장 위치 계산 (월드 좌표)
            Vector3 center = worldBounds.center;
            float height = worldBounds.size.y;
            Vector3 lightWorldPos = new Vector3(center.x, center.y + height * 0.5f - CEILING_OFFSET, center.z);

            // 방 크기에 따라 Point Light range 조정
            float roomDiagonal = worldBounds.size.magnitude;
            float lightRange = centralLightRange > 0f ? centralLightRange : Mathf.Max(DEFAULT_LIGHT_RANGE, roomDiagonal * 0.7f);

            // Point Light 생성 (월드 좌표 → 로컬 좌표로 변환)
            Vector3 lightLocalPos = room.transform.InverseTransformPoint(lightWorldPos);
            Light pointLight = AddPointLight(room, lightLocalPos, DEFAULT_LIGHT_COLOR, lightRange, Mathf.Max(0f, centralLightIntensity));

            // 선택적 깜빡임 효과
            if (flicker && pointLight != null)
            {
                var runner = room.GetComponent<FlickerRunner>() ?? room.AddComponent<FlickerRunner>();
                runner.StartFlicker(pointLight);
            }
        }

        /// <summary>
        /// GameObject와 그 자식들의 MeshFilter로부터 정확한 월드 공간 Bounds를 계산합니다.
        /// 회전된 오브젝트의 8개 코너를 모두 월드 공간으로 변환하여 정확도를 보장합니다.
        /// </summary>
        private static Bounds CalculateWorldBounds(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>();
            Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
            bool hasBounds = false;

            for (int i = 0; i < filters.Length; i++)
            {
                var mesh = filters[i].sharedMesh;
                if (mesh == null) continue;

                Transform t = filters[i].transform;
                Bounds localBounds = mesh.bounds;

                // 8개 코너를 로컬 → 월드 공간으로 변환
                Vector3[] corners = new Vector3[]
                {
                    new Vector3(localBounds.min.x, localBounds.min.y, localBounds.min.z),
                    new Vector3(localBounds.max.x, localBounds.min.y, localBounds.min.z),
                    new Vector3(localBounds.min.x, localBounds.max.y, localBounds.min.z),
                    new Vector3(localBounds.max.x, localBounds.max.y, localBounds.min.z),
                    new Vector3(localBounds.min.x, localBounds.min.y, localBounds.max.z),
                    new Vector3(localBounds.max.x, localBounds.min.y, localBounds.max.z),
                    new Vector3(localBounds.min.x, localBounds.max.y, localBounds.max.z),
                    new Vector3(localBounds.max.x, localBounds.max.y, localBounds.max.z)
                };

                for (int j = 0; j < corners.Length; j++)
                {
                    Vector3 worldCorner = t.TransformPoint(corners[j]);
                    if (!hasBounds)
                    {
                        bounds = new Bounds(worldCorner, Vector3.zero);
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(worldCorner);
                    }
                }
            }

            return bounds;
        }

        /// <summary>
        /// Point Light를 생성하고 부모에 추가.
        /// </summary>
        /// <param name="parent">부모 GameObject</param>
        /// <param name="localPosition">부모 기준 로컬 위치</param>
        /// <param name="color">라이트 색상</param>
        /// <param name="range">라이트 범위</param>
        /// <param name="intensity">라이트 강도</param>
        public static Light AddPointLight(GameObject parent, Vector3 localPosition, Color color, float range, float intensity)
        {
            GameObject lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.localPosition = localPosition;

            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            // [2026-10-10 H4 — 그림자 디더링 튜닝] 포인트라이트 실시간 섀도우 OFF —
            //   저해상도 큐브 섀도우가 점무늬 노이즈를 만들고(실내씬 7/8 지적), 어두운 실내에서
            //   그림자 기여는 낮다. 방향성은 쿠키 무늬+여러 광원 조합으로 대체.
            light.shadows = LightShadows.None;
            // [2026-10-10 F2] 절차 불꽃 쿠키(큐브맵) — 라이트 풀에 화염 무늬(고르지 않은 밝기) 부여.
            //   쿠키 생성 실패 시 무쿠키(기존 동작) — 선택 강화.
            var cookie = GetOrCreateFlameCookie();
            if (cookie != null) light.cookie = cookie;

            return light;
        }

        // ===== [F2] 절차 불꽃 쿠키 =====
        private static Cubemap _flameCookie;

        /// <summary>절차 불꽃 쿠키 큐브맵(32² ×6면) — 중심 웜 블롭 + 미세 노이즈. 1회 생성 캐시.</summary>
        private static Cubemap GetOrCreateFlameCookie()
        {
            if (_flameCookie != null) return _flameCookie;
            try
            {
                const int face = 32;
                var cube = new Cubemap(face, TextureFormat.RGBA32, false) { name = "IndoorFlameCookie" };
                var pixels = new Color[face * face];
                for (int f = 0; f < 6; f++)
                {
                    for (int y = 0; y < face; y++)
                    {
                        for (int x = 0; x < face; x++)
                        {
                            float dx = (x + 0.5f - face * 0.5f) / (face * 0.5f);
                            float dy = (y + 0.5f - face * 0.5f) / (face * 0.5f);
                            float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                            // 불꽃 실루엣 — 중심 밝고 가장자리 페이드 + 블롭 무늬(고르지 않은 그을음)
                            float blob = Mathf.Pow(1f - d, 1.6f);
                            float noise = 0.85f + 0.10f * Mathf.Sin(d * 14f + x * 0.7f + y * 1.3f)
                                          + 0.05f * Mathf.Sin(x * 2.1f + y * 3.7f);
                            float a = Mathf.Clamp01(blob * noise);
                            pixels[y * face + x] = new Color(1f, 1f, 1f, a);
                        }
                    }
                    var faceEnum = (CubemapFace)f;
                    cube.SetPixels(pixels, faceEnum);
                }
                cube.Apply(false, true);
                _flameCookie = cube;
                return cube;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[IndoorLighting] 불꽃 쿠키 생성 실패 — 무쿠키 진행: " + e.Message);
                return null;
            }
        }

        /// <summary>
        /// 깜빡임 효과를 실행하는 내부 MonoBehaviour.
        /// 방 GameObject에 직접 Component로 추가되어 별도 GameObject 생성을 피합니다.
        /// </summary>
        [DisallowMultipleComponent]
        private class FlickerRunner : MonoBehaviour
        {
            private Light _target;
            private float _baseIntensity;
            private Coroutine _coroutine;

            public void StartFlicker(Light target)
            {
                // 중복 코루틴 실행 방지
                if (_coroutine != null)
                {
                    StopCoroutine(_coroutine);
                    _coroutine = null;
                }

                _target = target;
                _baseIntensity = target != null ? target.intensity : 1f;
                _coroutine = StartCoroutine(FlickerLoop());
            }

            private IEnumerator FlickerLoop()
            {
                // [2026-10-10 F2] 화재 플리커 고도화 — 구 균등 PingPong 사인은 기계적으로 보였다.
                //   3중 사인(서로 다른 주파수) + 러너별 위상차 → 촛불/화로가 서로 다르게 흔들린다.
                //   배율은 FLICKER_MIN~MAX(0.8~1.2)로 클램프 — 기존 계약 밴드 유지.
                float phase1 = Random.value * Mathf.PI * 2f;
                float phase2 = Random.value * Mathf.PI * 2f;
                float phase3 = Random.value * Mathf.PI * 2f;
                while (_target != null)
                {
                    float t = Time.unscaledTime;
                    float multiplier = 1f
                        + 0.085f * Mathf.Sin(t * FLICKER_SPEED * 1.0f + phase1)
                        + 0.050f * Mathf.Sin(t * FLICKER_SPEED * 2.3f + phase2)
                        + 0.045f * Mathf.Sin(t * FLICKER_SPEED * 5.9f + phase3);
                    multiplier = Mathf.Clamp(multiplier, FLICKER_MIN_MULT, FLICKER_MAX_MULT);
                    _target.intensity = _baseIntensity * multiplier;
                    yield return null;
                }
            }

            private void OnDisable()
            {
                if (_coroutine != null)
                {
                    StopCoroutine(_coroutine);
                    _coroutine = null;
                }

                // 비활성화 시 intensity를 기본값으로 복원
                if (_target != null)
                    _target.intensity = _baseIntensity;
            }

            private void OnDestroy()
            {
                if (_coroutine != null)
                {
                    StopCoroutine(_coroutine);
                    _coroutine = null;
                }

                // 컴포넌트 제거 시 intensity를 기본값으로 복원
                if (_target != null)
                    _target.intensity = _baseIntensity;
            }
        }
    }
}
