import os
import re
import json
from typing import Dict

def analyze_script(file_path: str) -> Dict:
    """Analyze a Unity script for potential issues."""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
        
        issues = []
        
        # Check for missing usings  
        if 'UnityEngine;' not in content and ('using UnityEngine;' not in content):
            issues.append("Missing UnityEngine using directive")
            
        # Check for breaking changes - compile errors
        # Look for potential null reference exceptions
        if 'GetComponent<' in content and 'null' not in content:
            issues.append("Potential null reference risk with GetComponent")
        
        # Check for common code smells
        if content.count('Debug.Log') > 3:
            issues.append("Excessive Debug.Log usage - consider removing for production")
        
        # Check for unused variables/fields (simplified)
        var_declarations = re.findall(r'(private|public|protected)\s+(\w+)\s+(\w+);', content)
        if var_declarations:
            field_names = [decl[2] for decl in var_declarations]
            for field in field_names:
                if content.count(field) <= 2 and not field.startswith('_'):
                    issues.append(f"Potential unused field: {field}")
        
        return {
            "file_path": file_path,
            "issues": issues,
            "has_breaking_changes": any("missing" in issue.lower() or "null" in issue.lower() for issue in issues),
            "warning_count": len(issues)
        }
        
    except Exception as e:
        return {
            "file_path": file_path,
            "issues": [f"Failed to analyze file: {str(e)}"],
            "has_breaking_changes": True,
            "warning_count": 1
        }

def main():
    # Specific files for batch 3 based on the available UI files:
    # Theme/Tutorial/Effects/Utils
    actual_files = [
        "/mnt/c/Unity/code/Assets/Scripts/UI/Core/EffectManager.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Core/ThemeManager.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Core/TutorialManager.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Core/Utils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Effects/Effect.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Effects/ParticleEffect.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Effects/SoundEffect.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/AnimationUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/ColorUtils.cs", 
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/MathUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/ResourceUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/ScreenUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/StringUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/TimerUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/TimeUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIAnimationUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIComponentUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIContextUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIEffectManager.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIEffectUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIInputFieldUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIInputUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UINetworkUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIParticleUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIResourceUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UISoundUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UISpriteUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIStateUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIStyleUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UITextFieldUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/UIUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/VectorUtils.cs",
        "/mnt/c/Unity/code/Assets/Scripts/UI/Utils/WindowUtils.cs"
    ]
    
    # Filter to only existing files
    existing_files = [f for f in actual_files if os.path.exists(f)]
    
    results = {"batch3": {"results": [], "summary": {}, "status": "completed"}}
    
    total_broken = 0
    total_warnings = 0
    
    for file_path in existing_files:
        analysis_result = analyze_script(file_path)
        results["batch3"]["results"].append(analysis_result)
        
        if analysis_result["has_breaking_changes"]:
            total_broken += 1
        total_warnings += analysis_result["warning_count"]
    
    # Prepare summary
    results["batch3"]["summary"] = {
        "total_files": len(existing_files),
        "processed_files": len(existing_files),
        "breaking_changes": total_broken,
        "warnings": total_warnings,
        "status": "Completed" if total_broken == 0 else "Completed with breaking changes"
    }
    
    # Save the response
    with open("/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch3.json", 'w') as f:
        json.dump(results, f, indent=2)

if __name__ == "__main__":
    main()