@echo off
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Unity\code -executeMethod ProjectName.Editor.MixamoRetargetSetup.ApplyHumanoidRigs -logFile C:\Unity\code\buildlog_humanoid.txt
exit /b 0
