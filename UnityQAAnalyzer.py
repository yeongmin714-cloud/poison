import os
import json
import re
from typing import List, Dict, Any

def analyze_ui_core_scripts(file_paths: List[str]) -> Dict[str, Any]:
    """Analyze UI core scripts for quality issues"""
    issues = []
    warnings = []
    
    for file_path in file_paths:
        if not os.path.exists(file_path):
            continue
            
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
                
            # Check for potential breaking changes
            if 'using UnityEngine;' not in content and 'using UnityEngine.UI;' not in content:
                warnings.append(f"Missing UnityEngine usings in {file_path}")
            
            # Check for common code smells
            if 'Debug.Log(' in content and not 'DEBUG' in content:
                warnings.append(f"Potential Debug.Log usage in {file_path}")
                
            # Check for empty method bodies
            empty_methods = re.findall(r'(?:public|private|protected)\s+\w+\s+\w+\s*\([^)]*\)\s*\{\s*\}', content)
            if empty_methods:
                warnings.append(f"Empty method body found in {file_path}")
                
            # Check for commented out code
            commented_out = re.findall(r'//.*\b(?:if|for|while|switch|try|catch)\b', content)
            if commented_out:
                warnings.append(f"Commented out control structures found in {file_path}")
                
        except Exception as e:
            issues.append(f"Error reading {file_path}: {str(e)}")
    
    return {
        "total_files": len(file_paths),
        "issues": issues,
        "warnings": warnings
    }

def main():
    # Define the file paths from the JSON files
    batch1_files = [
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
    
    batch2_files = [
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
    
    batch3_files = [
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
    
    # Analyze each batch
    batch1_result = analyze_ui_core_scripts(batch1_files)
    batch2_result = analyze_ui_core_scripts(batch2_files)
    batch3_result = analyze_ui_core_scripts(batch3_files)
    
    # Create final report
    report = {
        "ui_qa_batch1": batch1_result,
        "ui_qa_batch2": batch2_result,
        "ui_qa_batch3": batch3_result
    }
    
    # Write the report to the shared mailbox
    output_path = "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/"
    os.makedirs(output_path, exist_ok=True)
    
    with open(os.path.join(output_path, "ui_qa_report.json"), 'w') as f:
        json.dump(report, f, indent=2)
        
    print("QA Analysis completed and report written to ui_qa_report.json")

if __name__ == "__main__":
    main()