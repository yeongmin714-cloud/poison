@echo off
"C:\Program Files\Unity\Hub\Editor\6000.4.10f1\Editor\Unity.exe" -batchmode -quit -projectPath C:\Unity\code -executeMethod ProjectName.EditorTools.MixamoControllerBuilder.BuildAll -logFile C:\Unity\code\buildlog_ctrl.txt
exit /b 0
