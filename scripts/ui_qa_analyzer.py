import os
import json
import subprocess
import sys
from typing import List, Dict, Any

def run_unity_checks(files: List[str]) -> Dict[str, Any]:
    """Run Unity-specific QA checks on the provided files"""
    results = {}
    
    for file_path in files:
        if not os.path.exists(file_path):
            results[file_path] = {"status": "error", "message": "File not found"}
            continue
            
        try:
            # Basic syntax check
            result = subprocess.run([
                "dotnet", "build", 
                "--no-restore", 
                "--configuration", "Release"
            ], 
            capture_output=True, 
            text=True, 
            timeout=300,
            cwd="/mnt/c/Unity/code"
            )
            
            if result.returncode == 0:
                results[file_path] = {"status": "success", "message": "Compilation successful"}
            else:
                # Parse compilation errors
                errors = []
                for line in result.stderr.split('\n'):
                    if 'error' in line.lower():
                        errors.append(line.strip())
                results[file_path] = {
                    "status": "error", 
                    "message": f"Compilation failed: {'; '.join(errors[:3])}"
                }
        except subprocess.TimeoutExpired:
            results[file_path] = {"status": "error", "message": "Compilation timeout"}
        except Exception as e:
            results[file_path] = {"status": "error", "message": f"Error during compilation: {str(e)}"}
    
    return results

def main():
    # 메인 작업 디렉터리
    base_dir = "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/to_qa/"
    
    # 읽을 JSON 파일 목록
    json_files = [
        "ui_qa_batch1.json",
        "ui_qa_batch2.json", 
        "ui_qa_batch3.json"
    ]
    
    # 각 파일에 대해 처리
    for json_file in json_files:
        json_path = os.path.join(base_dir, json_file)
        
        if not os.path.exists(json_path):
            print(f"Warning: {json_file} not found")
            continue
            
        with open(json_path, 'r') as f:
            data = json.load(f)
            
        # UI QA 작업 수행
        results = run_unity_checks(data['files'])
        
        # 결과를응답 디렉토리에 저장
        response_dir = "/mnt/c/Unity/hermes_director/workspace/SHARED_MAILBOX/director_orders/responses/from_qa/"
        os.makedirs(response_dir, exist_ok=True)
        
        response_file = os.path.join(response_dir, f"ui_qa_{json_file.replace('.json', '_response.json')}")
        with open(response_file, 'w') as f:
            json.dump({
                "task_id": data["task_id"],
                "status": "completed",
                "goal": data["goal"],
                "results": results,
                "timestamp": "2026-07-24T21:16:00Z"
            }, f, indent=2)

if __name__ == "__main__":
    main()