# Simple script to check Unity compilation errors
# Usage: .\check-unity-errors-simple.ps1

$projectPath = "Maelstrom"
$logPath = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"

Write-Host "=== Unity Compilation Error Checker ===" -ForegroundColor Cyan
Write-Host ""

# Check Unity Editor log
if (Test-Path $logPath) {
    Write-Host "Checking Unity Editor log: $logPath" -ForegroundColor Yellow
    $logContent = Get-Content $logPath -Tail 500 -ErrorAction SilentlyContinue
    
    if ($logContent) {
        $errors = $logContent | Select-String -Pattern "error CS\d+|Compilation failed" -Context 1,3
        if ($errors) {
            Write-Host "`n=== COMPILATION ERRORS FOUND ===" -ForegroundColor Red
            $errors | ForEach-Object {
                Write-Host "`n---" -ForegroundColor DarkGray
                Write-Host $_.Context.PreContext -ForegroundColor Gray
                Write-Host $_.Line -ForegroundColor Red
                Write-Host $_.Context.PostContext -ForegroundColor Gray
            }
        } else {
            Write-Host "No compilation errors found in recent log entries." -ForegroundColor Green
        }
    }
} else {
    Write-Host "Unity Editor log not found at: $logPath" -ForegroundColor Yellow
    Write-Host "Make sure Unity has been run at least once." -ForegroundColor Gray
}

Write-Host "`n=== Quick Check: Missing References ===" -ForegroundColor Yellow
$csFiles = Get-ChildItem -Path $projectPath -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue
$missingRefs = $csFiles | Select-String -Pattern "NetworkManager|MaelstromData|DataTag|INetworkData|IUdpService|UdpService" | Where-Object { $_.Line -match "^\s*(using|class|interface|enum)" }

if ($missingRefs) {
    Write-Host "Found potential missing references:" -ForegroundColor Yellow
    $missingRefs | ForEach-Object {
        Write-Host "  $($_.Filename):$($_.LineNumber) - $($_.Line.Trim())" -ForegroundColor Gray
    }
} else {
    Write-Host "No obvious missing references found." -ForegroundColor Green
}

Write-Host "`nDone! For full compilation check, run Unity in batch mode." -ForegroundColor Cyan
