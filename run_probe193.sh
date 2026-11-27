#!/bin/bash
cd /mnt/c/Unity/code
rm -rf TestOutput/icon_bake_probe_193
cmd.exe /c '"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -quit -batchmode -projectPath C:\Unity\code -executeMethod IconBakeProbe193.Run -logFile C:\Unity\code\TestOutput\probe193.log'
echo "EXIT=$?"
ls -la TestOutput/icon_bake_probe_193/ 2>/dev/null
