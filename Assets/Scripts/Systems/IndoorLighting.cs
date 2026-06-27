using System.Collections;
using UnityEngine;

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
        public static void SetupIndoorLighting(GameObject room, Color ambientColor, float ambientIntensity, bool flicker = false)
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
            float lightRange = Mathf.Max(DEFAULT_LIGHT_RANGE, roomDiagonal * 0.7f);

            // Point Light 생성 (월드 좌표 → 로컬 좌표로 변환)
            Vector3 lightLocalPos = room.transform.InverseTransformPoint(lightWorldPos);
            Light pointLight = AddPointLight(room, lightLocalPos, DEFAULT_LIGHT_COLOR, lightRange, DEFAULT_LIGHT_INTENSITY);

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
            light.shadows = LightShadows.Soft;

            return light;
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
                while (_target != null)
                {
                    // FLICKER_MIN_MULT ~ FLICKER_MAX_MULT 배율로 왕복
                    float t = Mathf.PingPong(Time.time * FLICKER_SPEED, 1f);
                    float multiplier = FLICKER_MIN_MULT + t * (FLICKER_MAX_MULT - FLICKER_MIN_MULT);
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
