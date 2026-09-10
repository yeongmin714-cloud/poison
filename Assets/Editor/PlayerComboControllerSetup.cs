using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>
/// WeaponCombo 상태를 Player_AC.controller에 적용하는 배치 훅 (B안: 단일 테이크 슬라이스 + 플레이헤드 제어).
/// 실행:
///   Unity.exe -quit -batchmode -projectPath &lt;path&gt; -executeMethod PlayerComboControllerSetup.SetupWeaponComboState
///
/// ■ 트랜지션은 추가하지 않는다 — HumanoidClipDriver가 _anim.Play/CrossFade로 직접 제어한다.
/// ■ idempotent: 재실행해도 안전(상태 존재 시 motion 갱신만 수행).
/// </summary>
public static class PlayerComboControllerSetup
{
    private const string ControllerPath = "Assets/Resources/Animation/Controllers/Player_AC.controller";
    private const string ComboFbxPath = "Assets/Animations/MeshyUser/Weapon_Combo_2.fbx";
    private const string ComboStateName = "WeaponCombo";
    private const string ComboClipNamePart = "Weapon_Combo_2";

    public static void SetupWeaponComboState()
    {
        // 1. 컨트롤러 로드
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            Debug.LogError($"[ComboSetup] 컨트롤러 로드 실패: {ControllerPath}");
            return;
        }

        // 2. 콤보 클립 로드 — FBX 하위 AnimationClip 중 이름에 "Weapon_Combo_2" 포함 (실제 클립명: Armature|Weapon_Combo_2_withSkin)
        var clip = AssetDatabase.LoadAllAssetsAtPath(ComboFbxPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(c => c.name != null && c.name.Contains(ComboClipNamePart));
        if (clip == null)
        {
            Debug.LogError($"[ComboSetup] 콤보 클립 없음: {ComboFbxPath} (이름에 '{ComboClipNamePart}' 포함)");
            return;
        }

        // 3. layer 0 stateMachine에서 "WeaponCombo" 상태 확인/추가
        var sm = controller.layers[0].stateMachine;
        if (sm == null)
        {
            Debug.LogError("[ComboSetup] layer 0 stateMachine 없음");
            return;
        }

        var existing = sm.states.FirstOrDefault(s => s.state != null && s.state.name == ComboStateName);
        if (existing.state != null)
        {
            if (existing.state.motion != clip)
            {
                existing.state.motion = clip;
                Debug.Log($"[ComboSetup] '{ComboStateName}' 상태 이미 존재 — motion 갱신: {existing.state.motion?.name} → {clip.name}");
            }
            else
            {
                Debug.Log($"[ComboSetup] '{ComboStateName}' 상태 이미 존재 — 변경 없음 (motion={clip.name})");
            }
        }
        else
        {
            var st = sm.AddState(ComboStateName, new Vector3(300f, -100f));
            st.motion = clip;
            Debug.Log($"[ComboSetup] '{ComboStateName}' 상태 추가 (motion={clip.name})");
        }

        // 5. 저장 (트랜지션 추가 없음 — Play/CrossFade 직접 제어)
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // 6. 재로드 후 검증
        var reloaded = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        var sm2 = reloaded != null ? reloaded.layers[0].stateMachine : null;
        var check = sm2 != null ? sm2.states.FirstOrDefault(s => s.state != null && s.state.name == ComboStateName) : default;
        if (check.state != null && check.state.motion == clip)
            Debug.Log($"✅ WeaponCombo 상태 준비 완료 (motion={check.state.motion.name})");
        else
            Debug.LogError("[ComboSetup] 검증 실패 — WeaponCombo 상태 또는 motion 확인 불가");
    }
}
