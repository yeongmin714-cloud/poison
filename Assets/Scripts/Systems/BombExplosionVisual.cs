using UnityEngine;
#pragma warning disable 0414

namespace ProjectName.Systems
{
    /// <summary>
    /// 폭탄 폭발 시각 효과 — Sprite 스파크 버스트 + 주황 섬광.
    /// Bomb.Explode가 폭탄 오브젝트를 Destroy(gameObject)할 때 이 컴포넌트의
    /// OnDisable이 발화하여 CombatVFXController.SpawnHitSparks(골드화이트 직선 스파크)를
    /// 폭발 위치에 생성한다. 폭탄 오브젝트에 addComponent로 함께 부착한다.
    /// </summary>
    public class BombExplosionVisual : MonoBehaviour
    {
        private bool _spawned; // 폭발/파괴 직전 1회만 발화

        private const string FirePath = "FX/Fire/VFX_Fire_01_Big";
        private const float FireLifetime = 2.5f; // 프리팹 stopAction:0(자가소멸 없음)이라 수동 Destroy 필요

        private void OnDisable()
        {
            // 게임 셧다운/씬 전환 시에도 방어적으로 1회만 처리
            if (_spawned) return;
            if (transform == null) return;

            Vector3 pos = transform.position;
            _spawned = true;

            // 골드화이트 직선 스파크 버스트 (기존 CombatVFXController 재사용)
            CombatVFXController.SpawnHitSparks(pos);

            // 불꽃 VFX — Resources에서 로드해 폭발 위치에 생성, 일정 시간 후 수동 Destroy
            var firePrefab = Resources.Load<GameObject>(FirePath);
            if (firePrefab != null)
            {
                var fire = GameObject.Instantiate(firePrefab, pos, Quaternion.identity);
                fire.transform.localScale = Vector3.one * 1.4f;
                GameObject.Destroy(fire, FireLifetime);
            }

            // 주황 섬광 — 프리미티브 구체 (일회성, 0.25초 후 소멸)
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "BombFlash";
            flash.transform.position = pos;
            flash.transform.localScale = Vector3.one * 0.6f;
            var col = flash.GetComponent<Collider>();
            if (col != null) Object.Destroy(col); // 시각 전용
            var ren = flash.GetComponent<MeshRenderer>();
            if (ren != null && ren.material != null)
                ren.material.color = new Color(1f, 0.6f, 0.2f); // 주황 섬광
            Object.Destroy(flash, 0.25f);
        }
    }
}