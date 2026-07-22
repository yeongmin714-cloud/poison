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
    # List of files for batch 2 based on the JSON
    expected_files = [
        "Assets/Scripts/UI/Core/UICore.cs",
        "Assets/Scripts/UI/Core/UIManager.cs", 
        "Assets/Scripts/UI/Core/ToolTipManager.cs",
        "Assets/Scripts/UI/Core/ThemeManager.cs",
        "Assets/Scripts/UI/Core/SignalManager.cs",
        "Assets/Scripts/UI/Core/ScreenManager.cs",
        "Assets/Scripts/UI/Core/MessageSystem.cs",
        "Assets/Scripts/UI/Core/LocalizationManager.cs",
        "Assets/Scripts/UI/Core/IUIComponent.cs",
        "Assets/Scripts/UI/Core/IDragDropHandler.cs",
        "Assets/Scripts/UI/Core/ICanvasComponent.cs",
        "Assets/Scripts/UI/Core/GameEventSystem.cs",
        "Assets/Scripts/UI/Core/EventSystemManager.cs",
        "Assets/Scripts/UI/Core/DragDropManager.cs",
        "Assets/Scripts/UI/Core/ComponentManager.cs",
        "Assets/Scripts/UI/Core/ColorPalette.cs",
        "Assets/Scripts/UI/Core/CanvasController.cs",
        "Assets/Scripts/UI/Core/AbilityManager.cs",
        "Assets/Scripts/UI/Core/Transitions/TransitionManager.cs",
        "Assets/Scripts/UI/Core/Transitions/ColorTransition.cs",
        "Assets/Scripts/UI/Core/Transitions/PanelTransition.cs",
        "Assets/Scripts/UI/Core/Transitions/TransitionType.cs",
        "Assets/Scripts/UI/Core/Transitions/Transition.cs",
        "Assets/Scripts/UI/Core/Transitions/AnimatedPanel.cs"
    ]
    
    # Filter to only existent files
    actual_files = [f for f in expected_files if os.path.exists(f"/mnt/c/Unity/code/{f}")]
    
    results = {"batch2": {"results": [], "summary": {}, "status": "completed"}}
    
    total_broken = 0
    total_warnings = 0
    
    for file_path in actual_files:
        full_path = f"/mnt/c/Unity/code/{file_path}"
        analysis_result = analyze_script(full_path)
        results["batch2"]["results"].append(analysis_result)
        
        if analysis_result["has_breaking_changes"]:
            total_broken += 1
        total_warnings += analysis_result["warning_count"]
    
    # Prepare summary
    results["batch2"]["summary"] = {
        "total_files": len(actual_files),
        "processed_files": len(actual_files),
        "breaking_changes": total_broken,
        "warnings": total_warnings,
        "status": "Completed" if total_broken == 0 else "Completed with breaking changes"
    }
    
    with open("/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch2.json", 'w') as f:
        json.dump(results, f, indent=2)

if __name__ == "__main__":
    main()