@echo off
REM Batch compile check for StatusWindowUI implementation
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Unity\code -logFile C:\Unity\code\buildlog_stats.txt
echo UNITY_EXIT_CODE=%ERRORLEVEL%
