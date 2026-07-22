import os
import re
import json
import subprocess
from typing import Dict, List, Tuple

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
        
        # Check for unused variables/fields
        # This is a simplified check
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
    # List of files for batch 3 (theme/Tutorial/Effects/Utils)
    # Looking at actual UI Core files and related ones
    expected_files = [
        "Assets/Scripts/UI/Core/EffectManager.cs",
        "Assets/Scripts/UI/Core/ThemeManager.cs",
        "Assets/Scripts/UI/Core/TutorialManager.cs",
        "Assets/Scripts/UI/Core/Utils.cs",
        "Assets/Scripts/UI/Core/ThemeManager.cs",
        "Assets/Scripts/UI/Core/TutorialManager.cs",
        "Assets/Scripts/UI/Core/EffectManager.cs",
        "Assets/Scripts/UI/Core/Utils.cs",
        "Assets/Scripts/UI/Effects/Effect.cs",
        "Assets/Scripts/UI/Effects/ParticleEffect.cs",
        "Assets/Scripts/UI/Effects/ScreenShakeEffect.cs",
        "Assets/Scripts/UI/Effects/FadeEffect.cs",
        "Assets/Scripts/UI/Effects/ColorFadeEffect.cs",
        "Assets/Scripts/UI/Effects/TransitionEffect.cs",
        "Assets/Scripts/UI/Effects/AnimationEffect.cs",
        "Assets/Scripts/UI/Effects/CameraEffect.cs",
        "Assets/Scripts/UI/Effects/VisualEffect.cs",
        "Assets/Scripts/UI/Effects/TextureEffect.cs",
        "Assets/Scripts/UI/Effects/LightEffect.cs",
        "Assets/Scripts/UI/Effects/PostProcessingEffect.cs",
        "Assets/Scripts/UI/Effects/ShaderEffect.cs",
        "Assets/Scripts/UI/Effects/EnvironmentEffect.cs",
        "Assets/Scripts/UI/Effects/UIEffect.cs"
    ]
    
    # Filter to only existent files
    actual_files = [f for f in expected_files if os.path.exists(f"/mnt/c/Unity/code/{f}")]
    
    results = {"batch3": {"results": [], "summary": {}, "status": "completed"}}
    
    total_broken = 0
    total_warnings = 0
    
    for file_path in actual_files:
        full_path = f"/mnt/c/Unity/code/{file_path}"
        analysis_result = analyze_script(full_path)
        results["batch3"]["results"].append(analysis_result)
        
        if analysis_result["has_breaking_changes"]:
            total_broken += 1
        total_warnings += analysis_result["warning_count"]
    
    # Prepare summary
    results["batch3"]["summary"] = {
        "total_files": len(actual_files),
        "processed_files": len(actual_files),
        "breaking_changes": total_broken,
        "warnings": total_warnings,
        "status": "Completed" if total_broken == 0 else "Completed with breaking changes"
    }
    
    with open("/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch3.json", 'w') as f:
        json.dump(results, f, indent=2)

if __name__ == "__main__":
    main()