using UnityEngine;
using UnityEditor;
using ProjectName.Systems;

/// <summary>
/// 씬 내 환경 오브젝트(나무/바위/식물)에 SwayController를 자동 부착.
/// 실행 메뉴: Tools/Install Sway Controllers
/// 
/// 분류 기준 (GameObject 이름 기반):
///   - Tree_*, *tree*, *fir*, *jacaranda* → 나무 (scale 기준 대/소 분류)
///   - Rock_*, *boulder*, *cliff*, *rock* → 바위
///   - Plant_*, *plant*, *searsia*, *shrub*, *bush* → 풀/식물
/// 
/// Poly Haven GLB 모델 (fir_tree_01_1k, jacaranda_tree_1k, boulder_01_1k 등)도 포함.
/// </summary>
public static class SwayInstaller
{
    [MenuItem("Tools/Install Sway Controllers")]
    public static void InstallSwayControllers()
    {
        int attachedCount = 0;
        int skippedCount = 0;

        // 씬 내 모든 GameObject 검색
        GameObject[] allObjects = Object.FindObjectsByType<GameObject>();

        foreach (GameObject go in allObjects)
        {
            if (go == null) continue;

            // 이미 SwayController가 있으면 스킵
            if (go.GetComponent<SwayController>() != null)
            {
                skippedCount++;
                continue;
            }

            SwayControllerPreset preset = ClassifyObject(go);
            if (preset == null) continue;

            // SwayController 부착
            SwayController controller = go.AddComponent<SwayController>();

            // 프리셋 적용
            controller.SetSwaySpeed(preset.swaySpeed);
            controller.SetSwayAmount(preset.swayAmount);
            controller.SetBobSpeed(preset.bobSpeed);
            controller.SetBobAmount(preset.bobAmount);

            attachedCount++;
        }

        Debug.Log($"[SwayInstaller] ✅ SwayController {attachedCount}개 부착 완료 (기존 보유: {skippedCount}개)");
    }

    [MenuItem("Tools/Install Sway Controllers", true)]
    private static bool Validate() => true;

    /// <summary>분류별 파라미터 프리셋</summary>
    private class SwayControllerPreset
    {
        public float swaySpeed;
        public float swayAmount; // 도(degree)
        public float bobSpeed;
        public float bobAmount;
    }

    /// <summary>
    /// GameObject 이름과 속성 기반 분류.
    /// Tree_* prefix + Poly Haven naming (*tree*, *fir*, *jacaranda*, *boulder*, *plant* 등).
    /// null 반환 시 SwayController 부착하지 않음.
    /// </summary>
    private static SwayControllerPreset ClassifyObject(GameObject go)
    {
        string name = go.name.ToLowerInvariant();

        // --- 나무: Tree_* 또는 이름에 tree/fir/jacaranda 포함 ---
        if (name.StartsWith("tree_") || name.Contains("tree") || name.Contains("fir") || name.Contains("jacaranda"))
        {
            float scale = go.transform.localScale.magnitude;
            if (scale >= 1.0f)
            {
                // 큰나무: swaySpeed=1.0, swayAmount=2도, bobAmount=0.02
                return new SwayControllerPreset
                {
                    swaySpeed = 1.0f,
                    swayAmount = 2f,
                    bobSpeed = 1.0f,
                    bobAmount = 0.02f
                };
            }
            else
            {
                // 작은나무: swaySpeed=1.5, swayAmount=3도, bobAmount=0.03
                return new SwayControllerPreset
                {
                    swaySpeed = 1.5f,
                    swayAmount = 3f,
                    bobSpeed = 1.2f,
                    bobAmount = 0.03f
                };
            }
        }

        // --- 바위: Rock_* 또는 이름에 boulder/cliff/rock 포함 ---
        if (name.StartsWith("rock_") || name.Contains("boulder") || name.Contains("cliff") || name.Contains("rock"))
        {
            // 바위: swaySpeed=0.3, swayAmount=0.5도 (미세한 흔들림)
            return new SwayControllerPreset
            {
                swaySpeed = 0.3f,
                swayAmount = 0.5f,
                bobSpeed = 0.5f,
                bobAmount = 0.01f
            };
        }

        // --- 식물/덤불: Plant_* 또는 이름에 plant/searsia/shrub/bush 포함 ---
        if (name.StartsWith("plant_") || name.Contains("plant") || name.Contains("searsia") || name.Contains("shrub") || name.Contains("bush"))
        {
            // 풀/식물: swaySpeed=2.5, swayAmount=5도, bobAmount=0.05
            return new SwayControllerPreset
            {
                swaySpeed = 2.5f,
                swayAmount = 5f,
                bobSpeed = 1.5f,
                bobAmount = 0.05f
            };
        }

        // 미분류 — 부착하지 않음
        return null;
    }
}