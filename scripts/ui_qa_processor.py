#!/usr/bin/env python3
"""
This script handles UI QA tasks for Unity projects by:
1. Checking UI files for Debug.Log usage and other patterns
2. Running compilation tests to detect real issues
3. Returning structured results following the QA protocol
"""

import json
import subprocess
import os
import sys

def run_unity_compile_test(project_path):
    """
    Run Unity compile test and check for runtime vs editor errors
    """
    compile_command = [
        "/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe",
        "-quit", "-batchmode", "-projectPath", project_path,
        "-executeMethod", "TestCompile.CompileTest",
        "-logFile", "/mnt/c/tmp/compile.log"
    ]
    
    try:
        result = subprocess.run(compile_command, capture_output=True, text=True, timeout=300)
        
        # Read log file to check for errors
        try:
            with open('/mnt/c/tmp/compile.log', 'r') as f:
                log_content = f.read()
        except FileNotFoundError:
            log_content = ""
            
        # Filter for runtime errors (not Editor-only)
        runtime_errors = []
        for line in log_content.splitlines():
            if 'error CS' in line and 'Editor/' not in line:
                runtime_errors.append(line)
                
        return runtime_errors, log_content
    except Exception as e:
        return [f"Compilation test failed with error: {str(e)}"], ""

def check_ui_files_for_debug_log(file_paths):
    """
    Check UI files for Debug.Log usage and return found files
    """
    debug_log_files = []
    
    for file_path in file_paths:
        if os.path.exists(file_path):
            try:
                with open(file_path, 'r') as f:
                    content = f.read()
                
                if 'Debug.Log' in content:
                    debug_log_files.append({
                        'file': file_path,
                        'has_debug_log': True
                    })
            except Exception as e:
                debug_log_files.append({
                    'file': file_path,
                    'error': f"Failed to read file: {str(e)}",
                    'has_debug_log': False
                })
        else:
            debug_log_files.append({
                'file': file_path,
                'error': 'File not found',
                'has_debug_log': False
            })
            
    return debug_log_files

def main():
    # UI files to check
    files_to_check = [
        "Assets/Scripts/UI/Utils/UIUtils.cs",
        "Assets/Scripts/UI/Effects/UIEffectManager.cs",
        "Assets/Scripts/UI/Tutorial/UITutorialManager.cs",
        "Assets/Scripts/UI/Themes/UIThemeManager.cs"
    ]
    
    # Unity project path
    project_path = "/mnt/c/Unity/code"
    
    # Initialize result structure
    result = {
        "task": "ui_qa_batch3",
        "summary": "UI QA process completed successfully",
        "errors": [],
        "modified_files": []
    }
    
    try:
        # Run compilation test first
        runtime_errors, log_content = run_unity_compile_test(project_path)
        
        if runtime_errors:
            result["errors"].extend([f"Runtime compilation error: {err}" for err in runtime_errors])
            result["summary"] = "Compilation errors found. Process halted."
        else:
            result["summary"] = "Compilation successful. Checking UI files."
            
            # Check UI files for Debug.Log usage
            debug_files = check_ui_files_for_debug_log(files_to_check)
            
            # Report any Debug.Log usage found
            for file_info in debug_files:
                if file_info.get('has_debug_log', False):
                    result["errors"].append(f"Debug.Log found in {file_info['file']}")
                    
            # Even if files with Debug.Log are found, we continue processing
            # since this is not necessarily a breaking error
            result["summary"] = "All checks completed."
            
    except Exception as e:
        result["errors"].append(f"Error during processing: {str(e)}")
    
    # Write response JSON file
    response_path = '/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/responses/from_qa/_done_ui_qa_batch3.json'
    try:
        with open(response_path, 'w') as f:
            json.dump(result, f, indent=2, ensure_ascii=False)
    except Exception as e:
        result["errors"].append(f"Failed to write response file: {str(e)}")
    
    # Print result for stdout
    print(json.dumps(result, ensure_ascii=False))
    return result

if __name__ == "__main__":
    main()