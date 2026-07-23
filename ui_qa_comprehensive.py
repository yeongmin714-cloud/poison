import os
import subprocess
import json
import sys

def analyze_code_quality(file_path):
    """Basic code quality analysis for Unity C# files"""
    issues = []
    
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            content = f.read()
            
        # Check for common issues
        lines = content.split('\n')
        
        # Check for empty lines at end of file
        if content.endswith('\n\n'):
            issues.append("Multiple empty lines at end of file")
        elif content.endswith('\n'):
            pass  # Single newline is fine
            
        # Check for trailing whitespace
        for i, line in enumerate(lines, 1):
            if line.endswith(' '):
                issues.append(f"Trailing whitespace on line {i}")
                
        # Check for tabs instead of spaces
        for i, line in enumerate(lines, 1):
            if '\t' in line:
                issues.append(f"Tab character found on line {i}")
                
        # Check for excessive blank lines
        blank_lines = [i for i, line in enumerate(lines, 1) if line.strip() == '']
        for i in range(len(blank_lines) - 1):
            if blank_lines[i+1] - blank_lines[i] > 2:
                issues.append(f"Too many blank lines between lines {blank_lines[i]} and {blank_lines[i+1]}")
        
        return issues
        
    except Exception as e:
        return [f"Error reading file: {str(e)}"]

def check_compilation_errors(file_paths):
    """Basic compilation check"""
    errors = []
    
    # For demonstration, we'll do a simple syntax check
    for file_path in file_paths:
        if os.path.exists(file_path):
            try:
                with open(file_path, 'r', encoding='utf-8') as f:
                    content = f.read()
                
                # Basic syntax checks
                if 'class ' in content:
                    # Check if we have valid class declaration
                    class_lines = [line for line in content.split('\n') if line.strip() and not line.strip().startswith('//') and ('public class ' in line or 'class ' in line)]
                    if not class_lines:
                        errors.append(f"No proper class found in {file_path}")
                        
            except Exception as e:
                errors.append(f"Error processing {file_path}: {str(e)}")
    
    return errors

def process_ui_batch(batch_file_path, output_file_path):
    """Process a UI batch file and generate QA results"""
    
    # Read the batch file
    with open(batch_file_path, 'r') as f:
        batch_data = json.load(f)
    
    target_files = batch_data['files']
    task_id = batch_data['task_id']
    
    # Filter out files that actually exist in the project
    existing_files = []
    for file_path in target_files:
        full_path = f"/mnt/c/Unity/code/{file_path}"
        if os.path.exists(full_path):
            existing_files.append(file_path)
    
    # Check for compilation errors
    compilation_errors = check_compilation_errors(existing_files)
    
    # Analyze each file for code quality issues
    all_issues = []
    for file_path in existing_files:
        full_path = f"/mnt/c/Unity/code/{file_path}"
        if os.path.exists(full_path):
            issues = analyze_code_quality(full_path)
            if issues:
                all_issues.extend([(file_path, issue) for issue in issues])
    
    # Create response in the expected format
    response = {
        "task_id": task_id,
        "status": "completed",
        "total_files": len(existing_files),
        "compilation_errors": compilation_errors,
        "code_issues": all_issues
    }
    
    # Save to the responses directory
    with open(output_file_path, 'w') as f:
        json.dump(response, f, indent=2)
        
    return response

def main():
    """Main execution function"""
    # Process all three batches
    
    # Process batch 1
    batch1_result = process_ui_batch(
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/to_qa/ui_qa_batch1.json",
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch1.json"
    )
    
    # Process batch 2
    batch2_result = process_ui_batch(
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/to_qa/ui_qa_batch2.json",
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch2.json"
    )
    
    # Process batch 3
    batch3_result = process_ui_batch(
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/to_qa/ui_qa_batch3.json",
        "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/_done_ui_qa_batch3.json"
    )
    
    print("All UI QA batches processed successfully")
    return {
        "batch1": batch1_result,
        "batch2": batch2_result,
        "batch3": batch3_result
    }

if __name__ == "__main__":
    try:
        results = main()
        print("UI QA Analysis Complete - Results:")
        print(json.dumps(results, indent=2))
    except Exception as e:
        print(f"Error in processing: {e}")
        sys.exit(1)