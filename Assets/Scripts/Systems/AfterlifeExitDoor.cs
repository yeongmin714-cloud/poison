using ProjectName.Core;
using ProjectName.Core.Utils;
using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// [A2] 이계 귀환 문 — 타이머 완주 시 활성화되는 귀환 앵커.
    /// 활성 전 = 꺼진 문틀(렌더러 없음), 활성 후 = 문양 빛 기둥 + 플레이어 접촉 → 귀환 정산.
    /// </summary>
    public class AfterlifeExitDoor : MonoBehaviour
    {
        private GameObject _visual;
        private bool _active;
        private float _checkInterval = 0.25f;
        private float _nextCheck;

        public bool IsActive => _active;

        /// <summary>문 비활성(개시) — 시각 요소 없음.</summary>
        public void DeactivateDoor()
        {
            _active = false;
            if (_visual != null) Destroy(_visual);
            _visual = null;
        }

        /// <summary>문 활성(완주) — 문양 빛 기둥 생성 + 귀환 판정 개시.</summary>
        public void ActivateDoor()
        {
            if (_active) return;
            _active = true;

            if (_visual == null)
            {
                _visual = new GameObject("AfterlifeExitDoorVisual");
                _visual.transform.SetParent(transform, false);
                _visual.transform.localPosition = Vector3.zero;

                // 빛 기둥 (문양색 — 문장 시스템 색, 부재 시 백금색)
                Color doorColor = new Color(1f, 0.85f, 0.4f);
                try
                {
                    doorColor = TerritoryCaptureSystem.GetPlayerFlagColor();
                }
                catch { /* 문장 시스템 부재 — 기본 금색 */ }

                var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beam.name = "ExitBeam";
                beam.transform.SetParent(_visual.transform, false);
                beam.transform.localPosition = new Vector3(0f, 3f, 0f);
                beam.transform.localScale = new Vector3(1.4f, 3f, 1.4f);
                var beamCol = beam.GetComponent<Collider>();
                if (beamCol != null) Destroy(beamCol);
                var beamRenderer = beam.GetComponent<MeshRenderer>();
                if (beamRenderer != null)
                {
                    var mat = ProjectName.Core.Utils.MaterialHelper.CreateLitMaterial(doorColor, "AfterlifeDoorBeam");
                    if (mat.HasProperty("_EmissionColor"))
                    {
                        mat.SetColor("_EmissionColor", doorColor * 1.2f);
                        mat.EnableKeyword("_EMISSION");
                    }
                    beamRenderer.material = mat;
                }

                var lightGo = new GameObject("ExitLight");
                lightGo.transform.SetParent(_visual.transform, false);
                lightGo.transform.localPosition = new Vector3(0f, 3f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = doorColor;
                light.intensity = 2f;
                light.range = 15f;
                light.shadows = LightShadows.None;
            }

            Debug.Log("[AfterlifeExitDoor] 🌟 귀환 문 개방 — 진입 시 귀환 정산");
        }

        private void Update()
        {
            if (!_active) return;

            // 저주파 접촉 판정 (0.25s) — 플레이어 근접 → 귀환
            _nextCheck += Time.deltaTime;
            if (_nextCheck < _checkInterval) return;
            _nextCheck = 0f;

            var player = PlayerHealth.Instance;
            if (player == null) return;
            var playerTransform = player.transform;
            if (Vector3.Distance(playerTransform.position, transform.position) < 2.2f)
            {
                _active = false; // 중복 판정 차단
                AfterlifeSurvivalSystem.CompleteRun();
            }
        }
    }
}
