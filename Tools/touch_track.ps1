$files = @(
  'C:\Unity\code\Assets\Scripts\Systems\CombatFXGate.cs',
  'C:\Unity\code\Assets\Scripts\Systems\CombatFXGate.cs.meta'
)
foreach ($f in $files) {
  if (Test-Path -LiteralPath $f) {
    $c = Get-Content -LiteralPath $f -Raw
    Set-Content -LiteralPath $f -Value $c -NoNewline
    Write-Output "TOUCHED: $f"
  } else {
    Write-Output "SKIP(none): $f"
  }
}