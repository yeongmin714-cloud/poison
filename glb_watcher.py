import os
import json
import subprocess
import time
import glob

USER_PROVIDED_DIR = "/mnt/c/Unity/code/Assets/Resources/Models/UserProvided"
STATE_FILE = "/tmp/.hermes_glb_state.json"
UNITY_EXE = "/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
PROJECT_PATH = "C:\\Unity\\code"

def kill_unity():
    print("Killing Unity processes...")
    subprocess.run("taskkill.exe /F /IM Unity.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run("taskkill.exe /F /IM Unity Hub.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run("taskkill.exe /F /IM UnityPackageManager.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run("taskkill.exe /F /IM UnityCrashHandler64.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    subprocess.run("taskkill.exe /F /IM Unity.Licensing.Client.exe", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    # Remove Unity lock files
    subprocess.run("find \"/mnt/c/Unity/code/Library\" -name \"*-lock\" -delete", shell=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

def wait_for_unity_close():
    print("Waiting for Unity processes to terminate...")
    time.sleep(5)

def run_compile_test():
    print("Running compile test...")
    result = subprocess.run(
        f'"{UNITY_EXE}" -quit -batchmode -projectPath "{PROJECT_PATH}" -executeMethod TestCompile.CompileTest -logFile compile.log',
        shell=True,
        capture_output=True,
        text=True
    )
    # Check for errors in compile.log
    if os.path.exists("compile.log"):
        with open("compile.log", "r") as f:
            log = f.read()
            if "error cs" in log.lower():
                print("Compile test failed. See compile.log for details.")
                return False
    print("Compile test passed.")
    return True

def run_swap():
    print("Running GLB swap...")
    result = subprocess.run(
        f'"{UNITY_EXE}" -quit -batchmode -projectPath "{PROJECT_PATH}" -executeMethod ModelSwapper.SwapAndSave -logFile swap.log',
        shell=True,
        capture_output=True,
        text=True
    )
    if os.path.exists("swap.log"):
        with open("swap.log", "r") as f:
            log = f.read()
            if "error cs" in log.lower():
                print("Swap failed. See swap.log for details.")
                return False
    print("Swap succeeded.")
    return True

def restart_unity():
    print("Restarting Unity Editor...")
    # Convert WSL path to Windows for cmd.exe
    unity_wsl_path = "/mnt/c/Program Files/Unity/Hub/Editor/6000.4.10f1/Editor/Unity.exe"
    unity_windows_path = "C:\\Program Files\\Unity\\Hub\\Editor\\6000.4.10f1\\Editor\\Unity.exe"
    project_windows_path = "C:\\Unity\\code"
    subprocess.run(f'cmd.exe /c start "" "{unity_windows_path}" -projectPath "{project_windows_path}"', shell=True)
    time.sleep(5)

def send_telegram(basename):
    message = f"[Unity] GLB file '{basename}' swapped successfully."
    # We don't have the Telegram bot token and chat ID, so we'll just print for now.
    # In a real implementation, we would use curl to send to Telegram.
    print(f"Telegram notification: {message}")

def main():
    os.chdir(USER_PROVIDED_DIR)
    
    # Scan for .glb files (top-level, exclude processed)
    glb_files = []
    for f in glob.glob("*.glb"):
        glb_files.append(os.path.basename(f))
    
    # Read state file
    processed = []
    if os.path.exists(STATE_FILE):
        try:
            with open(STATE_FILE, "r") as f:
                processed = json.load(f)
        except json.JSONDecodeError:
            processed = []
    
    # Determine new files
    new_files = [f for f in glb_files if f not in processed]
    
    if not new_files:
        print("No new GLB files to process.")
        return
    
    print(f"New GLB files: {new_files}")
    
    for filename in new_files:
        print(f"Processing {filename}...")
        
        kill_unity()
        wait_for_unity_close()
        
        if not run_compile_test():
            print(f"Compile test failed for {filename}. Skipping swap.")
            continue
        
        if not run_swap():
            print(f"Swap failed for {filename}. Skipping state update and Unity restart.")
            continue
        
        # Swap succeeded
        # Update state file to include all currently scanned top-level GLB basenames
        with open(STATE_FILE, "w") as f:
            json.dump(glb_files, f)   # glb_files is the list of all scanned .glb files in the directory
        
        restart_unity()
        send_telegram(filename)
        # Break after first success to avoid reprocessing the same batch
        print(f"Swap successful for {filename}. Breaking to avoid reprocessing the same batch.")
        break
    
    print("GLB watcher agent completed.")

if __name__ == "__main__":
    main()