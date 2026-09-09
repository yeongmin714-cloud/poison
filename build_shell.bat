@echo off
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Unity\code -executeMethod IndoorShellMenu.RunForBatch -logFile C:\Unity\code\buildlog_shell.txt
echo UNITY_EXIT_CODE=%ERRORLEVEL%
