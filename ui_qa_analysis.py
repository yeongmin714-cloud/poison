import json
import os
import sys
from pathlib import Path

# Define the paths
base_dir = Path('/mnt/c/Unity/code')
qa_batch1_path = base_dir / 'Assets/Scripts/UI/Core'
qa_batch2_path = base_dir / 'Assets/Scripts/UI/Core'
qa_batch3_path = base_dir / 'Assets/Scripts/UI/Effects'
qa_batch3_path2 = base_dir / 'Assets/Scripts/UI/Tutorial'
qa_batch3_path3 = base_dir / 'Assets/Scripts/UI/Utils'

# Create results directory
results_dir = base_dir / 'temp_qa_results'
results_dir.mkdir(exist_ok=True)

# Check all files for breaking changes
def check_breaking_changes(file_path):
    breaking_issues = []
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Check for common breaking change patterns
        # Use of deprecated Unity APIs
        deprecated_apis = [
            'GUI.DrawTexture',
            'GUI.TextField',
            'GUI.Label',
            'GUI.Box',
            'GUI.Button',
            'GUILayout.Button',
            'GUILayout.TextField'
        ]
        for api in deprecated_apis:
            if api in content:
                breaking_issues.append(f"Use of deprecated API: {api}")
        
        # Check for missing using directives or references
        if 'using UnityEngine;' not in content and 'using UnityEditor;' not in content:
            breaking_issues.append("Missing Unity namespace references")
            
        # Check for non-existent classes or methods that we know exist
        required_classes = ['MonoBehaviour', 'ScriptableObject']
        for cls in required_classes:
            if cls in content and ('using UnityEngine;' not in content or 'using UnityEditor;' not in content):
                # Add more detailed check if needed
                pass
                
        # Check for structural issues in core files
        if 'public class' in content and 'using UnityEngine;' not in content:
            # Try to find if it's a UICore class
            if 'class UICore' in content:
                breaking_issues.append("UICore class found but missing required using directives")
        
    except Exception as e:
        breaking_issues.append(f"Analysis error: {str(e)}")
        
    return breaking_issues

# Function to analyze a batch of files with breaking checks
def analyze_batch_files(batch_files, output_file):
    all_issues = {}
    error_count = 0
    
    for file_path in batch_files:
        full_path = base_dir / file_path
        if not full_path.exists():
            all_issues[file_path] = {
                'error': f'File not found: {file_path}'
            }
            error_count += 1
            continue
            
        breaking_issues = check_breaking_changes(full_path)
        all_issues[file_path] = {
            'breaking_issues': breaking_issues
        }
        
    # Save the report as a JSON file
    with open(output_file, 'w', encoding='utf-8') as f:
        json.dump(all_issues, f, indent=4, ensure_ascii=False)
    
    print(f"Analysis completed. {error_count} errors found during file scanning.")
    return all_issues

# Process each batch
# Batch 1: UI Core Scripts QA
print("Starting Batch 1 analysis...")
result_batch1 = analyze_batch_files([
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
], results_dir / 'ui_qa_batch1_results.json')

# Batch 2: UI Functionality Scripts QA
print("Starting Batch 2 analysis...")
result_batch2 = analyze_batch_files([
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
], results_dir / 'ui_qa_batch2_results.json')

# Batch 3: UI Theme/Tutorial/Effects/Utils QA
print("Starting Batch 3 analysis...")
result_batch3 = analyze_batch_files([
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
], results_dir / 'ui_qa_batch3_results.json')

print("All batches completed.")