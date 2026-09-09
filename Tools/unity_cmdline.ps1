$ci = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction SilentlyContinue
if ($ci) {
  foreach ($p in $ci) {
    Write-Output ("PID " + $p.ProcessId)
    Write-Output ("CMD " + $p.CommandLine)
  }
} else { Write-Output "NO Unity.exe" }
