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
        /// <summary>
        /// 실내 조명 설정: 앰비언트 어둡게 + 천장 중앙 Point Light.
        /// </summary>
        /// <param name="room">방 GameObject (자식으로 Light 추가)</param>
        /// <param name="ambientColor">앰비언트 색상</param>
        /// <param name="ambientIntensity">앰비언트 강도 (0~1)</param>
        /// <param name="flicker">깜빡임 효과 활성화 여부</param>
        public static void SetupIndoorLighting(GameObject room, Color ambientColor, float ambientIntensity, bool flicker = false)
        {
            // 앰비언트 라이트를 어둡게 설정
            RenderSettings.ambientLight = new Color(
                ambientColor.r * ambientIntensity,
                ambientColor.g * ambientIntensity,
                ambientColor.b * ambientIntensity
            );

            // 방 크기 추정 (자식 MeshFilter들의 bounds로)
            Bounds bounds = new Bounds(room.transform.position, Vector3.zero);
            var filters = room.GetComponentsInChildren<MeshFilter>();
            if (filters.Length > 0)
            {
                bounds = filters[0].mesh.bounds;
                for (int i = 1; i < filters.Length; i++)
                {
                    bounds.Encapsulate(filters[i].mesh.bounds);
                }
            }

            // 방 중앙 천장 위치 계산
            Vector3 center = bounds.center;
            float height = bounds.size.y;
            Vector3 lightPos = new Vector3(center.x, center.y + height * 0.5f - 0.2f, center.z);

            // Point Light 생성
            Light pointLight = AddPointLight(room, lightPos, new Color(1f, 0.95f, 0.8f), 15f, 1.2f);

            // 선택적 깜빡임 효과
            if (flicker && pointLight != null)
            {
                var flickerRoutine = new GameObject($"{room.name}_FlickerRoutine");
                flickerRoutine.transform.SetParent(room.transform);
                var runner = flickerRoutine.AddComponent<FlickerRunner>();
                runner.StartFlicker(pointLight);
            }
        }

        /// <summary>
        /// Point Light를 생성하고 부모에 추가.
        /// </summary>
        public static Light AddPointLight(GameObject parent, Vector3 position, Color color, float range, float intensity)
        {
            GameObject lightGo = new GameObject("PointLight");
            lightGo.transform.SetParent(parent.transform);
            lightGo.transform.localPosition = position;

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
        /// </summary>
        private class FlickerRunner : MonoBehaviour
        {
            private Light _target;
            private float _baseIntensity;
            private Coroutine _coroutine;

            public void StartFlicker(Light target)
            {
                _target = target;
                _baseIntensity = target != null ? target.intensity : 1f;
                _coroutine = StartCoroutine(FlickerLoop());
            }

            private IEnumerator FlickerLoop()
            {
                while (_target != null)
                {
                    // 0.8 ~ 1.2 배율로 왕복
                    float t = Mathf.PingPong(Time.time * 2.5f, 1f);
                    float multiplier = 0.8f + t * 0.4f;
                    _target.intensity = _baseIntensity * multiplier;
                    yield return null;
                }
            }

            private void OnDestroy()
            {
                if (_coroutine != null)
                {
                    StopCoroutine(_coroutine);
                    _coroutine = null;
                }
            }
        }
    }
}
