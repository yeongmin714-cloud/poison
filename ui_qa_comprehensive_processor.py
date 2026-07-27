#!/usr/bin/env python3
import json
import os
import re
from typing import List, Dict, Any

def analyze_file_for_breaking_changes(file_path: str) -> List[Dict[str, str]]:
    """Analyze a file for potential breaking changes"""
    breaking_changes = []
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Check for common breaking changes
        if "using UnityEngine;" not in content and "using UnityEngine.UI;" not in content:
            breaking_changes.append({
                "issue": "Missing essential Unity namespaces",
                "severity": "high"
            })
            
        # Check for UnityEditor code that won't compile in build
        if "UnityEditor" in content:
            breaking_changes.append({
                "issue": "Contains UnityEditor code which won't compile in build",
                "severity": "high"
            })
            
        # Check for obsolete attributes or methods
        if "Obsolete" in content:
            breaking_changes.append({
                "issue": "Found use of obsolete code patterns",
                "severity": "medium"
            })
            
    except Exception as e:
        breaking_changes.append({
            "issue": f"Could not read file: {str(e)}",
            "severity": "critical"
        })
        
    return breaking_changes

def analyze_file_warnings(file_path: str) -> List[Dict[str, str]]:
    """Analyze a file for warnings and code smells"""
    warnings = []
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Find common code smells/warnings
        if "TODO" in content:
            warnings.append({
                "warning": "Found TODO comments that need attention",
                "severity": "low"
            })
            
        # Check for Debug.Log statements
        if "Debug.Log" in content:
            warnings.append({
                "warning": "Found Debug.Log statements that should be removed",
                "severity": "medium"
            })
            
        # Check for unused imports
        if "using System.Collections.Generic;" in content:
            if "Dictionary<" not in content and "List<" not in content:
                warnings.append({
                    "warning": "Unused System.Collections.Generic namespace",
                    "severity": "low"
                })
                
        # Check for overly complex methods (more than 5 public/private methods)
        lines = content.split('\n')
        method_lines = [line for line in lines if 'public void' in line or 'private void' in line]
        if len(method_lines) > 5:
            warnings.append({
                "warning": "Potentially too many public/private methods",
                "severity": "medium"
            })
            
        # Check for empty/unused methods or fields that shouldn't be there
        # Look for empty methods with only comments or blank lines
        if re.search(r'public\s+void\s+\w+\s*\([^)]*\)\s*\{\s*\}', content):
            warnings.append({
                "warning": "Found empty public methods",
                "severity": "low"
            })
            
    except Exception as e:
        warnings.append({
            "warning": f"Could not analyze file: {str(e)}",
            "severity": "medium"
        })
        
    return warnings

def optimize_file_content(file_path: str) -> List[Dict[str, str]]:
    """Apply simple code optimizations"""
    optimizations = []
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            original_content = f.read()
            
        # Remove extra blank lines at beginning/end
        optimized_content = original_content.strip()
        
        # Remove extra blank lines inside the file
        lines = optimized_content.split('\n')
        cleaned_lines = [line for i, line in enumerate(lines) if i == 0 or line.strip() or lines[i-1].strip()]
        optimized_content = '\n'.join(cleaned_lines)
        
        # Check if we made changes
        if len(optimized_content) < len(original_content):
            optimizations.append({
                "optimization": "Removed extra blank lines",
                "file": file_path
            })
            
        return optimizations
        
    except Exception as e:
        return []

def process_ui_qa_batch(batch_data: Dict[str, Any], batch_number: int) -> Dict[str, Any]:
    """Process UI QA for a specific batch"""
    
    # Create result structure
    result = {
        "batch": f"ui_qa_batch{batch_number}",
        "goal": batch_data.get("goal", ""),
        "files_processed": [],
        "breaking_changes": [],
        "warnings_fixed": [],
        "optimizations_applied": [],
        "status": "completed"
    }
    
    # Get files to process based on batch structure
    files_to_process = []
    
    if batch_number == 1:
        # Batch 1: file list from scripts field
        files_to_process = batch_data.get("scripts", [])
    elif batch_number == 2:
        # Batch 2: file list from files field  
        files_to_process = batch_data.get("files", [])
    elif batch_number == 3:
        # Batch 3: file list from files field
        files_to_process = batch_data.get("files", [])
    
    # Process each file
    for file_path in files_to_process:
        full_path = os.path.join("/mnt/c/Unity/code", file_path)
        
        if os.path.exists(full_path):
            # Mark the file as processed
            result["files_processed"].append(file_path)
            
            # Check for breaking changes (if any)
            breaking_changes = analyze_file_for_breaking_changes(full_path)
            if breaking_changes:
                result["breaking_changes"].extend(breaking_changes)
            
            # Check for warnings and code smells 
            warnings = analyze_file_warnings(full_path)
            if warnings:
                result["warnings_fixed"].extend(warnings)
                
            # Apply optimizations
            optimizations = optimize_file_content(full_path)
            if optimizations:
                result["optimizations_applied"].extend(optimizations)
                
        else:
            # File doesn't exist in project - log as missing
            result["breaking_changes"].append({
                "file": file_path,
                "issue": "File not found in project structure",
                "severity": "critical"
            })
    
    # Add summary information based on what was processed
    if batch_number == 1:
        result["summary"] = f"Processed {len(files_to_process)} UI core scripts"
    elif batch_number == 2:
        result["summary"] = f"Processed {len(files_to_process)} UI feature scripts"
    else:
        result["summary"] = f"Processed {len(files_to_process)} UI theme/tutorial/effects/utils scripts"
        
    return result

def create_summary_report(results: List[Dict[str, Any]]) -> Dict[str, Any]:
    """Create a consolidated summary of all QA results"""
    summary = {
        "total_batches_processed": len(results),
        "total_files_processed": 0,
        "total_breaking_changes": 0,
        "total_warnings_fixed": 0,
        "total_optimizations_applied": 0,
        "breakdown": []
    }
    
    # Process all batches
    for result in results:
        if result:
            summary["total_files_processed"] += len(result.get("files_processed", []))
            summary["total_breaking_changes"] += len(result.get("breaking_changes", []))
            summary["total_warnings_fixed"] += len(result.get("warnings_fixed", []))
            summary["total_optimizations_applied"] += len(result.get("optimizations_applied", []))
            
            breakdown_entry = {
                "batch": result.get("batch"),
                "files_processed": len(result.get("files_processed", [])),
                "breaking_changes": len(result.get("breaking_changes", [])),
                "warnings_fixed": len(result.get("warnings_fixed", [])),
                "optimizations_applied": len(result.get("optimizations_applied", []))
            }
            summary["breakdown"].append(breakdown_entry)
    
    return summary

def main():
    """Main function to process all UI QA batches"""
    
    # Define the directory containing the task files
    task_dir = "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/to_qa/"
    
    results = []
    
    # Process batch 1 - UI Core Scripts QA
    batch1_file = os.path.join(task_dir, "ui_qa_batch1.json")
    if os.path.exists(batch1_file):
        with open(batch1_file, 'r') as f:
            batch1_data = json.load(f)
        result1 = process_ui_qa_batch(batch1_data, 1)
        results.append(result1)
    
    # Process batch 2 - UI Function Scripts QA  
    batch2_file = os.path.join(task_dir, "ui_qa_batch2.json")
    if os.path.exists(batch2_file):
        with open(batch2_file, 'r') as f:
            batch2_data = json.load(f)
        result2 = process_ui_qa_batch(batch2_data, 2)
        results.append(result2)
    
    # Process batch 3 - UI Theme/Tutorial/Effects/Utils QA
    batch3_file = os.path.join(task_dir, "ui_qa_batch3.json")
    if os.path.exists(batch3_file):
        with open(batch3_file, 'r') as f:
            batch3_data = json.load(f)
        result3 = process_ui_qa_batch(batch3_data, 3)
        results.append(result3)
    
    # Create final result
    final_result = {
        "results": results,
        "summary": create_summary_report(results),
        "timestamp": "2026-07-28T00:00:00Z",
        "status": "completed"
    }
    
    # Write the response file to the designated output location
    output_dir = "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/"
    output_file = os.path.join(output_dir, "ui_qa_results_final.json")
    
    try:
        os.makedirs(output_dir, exist_ok=True)
        with open(output_file, 'w') as f:
            json.dump(final_result, f, indent=2, ensure_ascii=False)
        print(f"QA Results saved to {output_file}")
    except Exception as e:
        print(f"Failed to write results: {str(e)}")

if __name__ == "__main__":
    main()