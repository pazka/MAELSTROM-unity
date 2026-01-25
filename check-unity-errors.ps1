# PowerShell script to check Unity compilation errors
# Usage: .\check-unity-errors.ps1

$projectPath = "Maelstrom"
$logPath = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"

Write-Host "Checking Unity compilation errors..." -ForegroundColor Cyan
Write-Host "Project path: $PWD\$projectPath" -ForegroundColor Gray
Write-Host ""

# Method 1: Check the Unity Editor log file (if Unity is running or was recently run)
if (Test-Path $logPath) {
    Write-Host "=== Checking Unity Editor Log ===" -ForegroundColor Yellow
    $errors = Select-String -Path $logPath -Pattern "error CS\d+|Compilation failed|error:" -Context 0,2
    if ($errors) {
        Write-Host "Found compilation errors:" -ForegroundColor Red
        $errors | ForEach-Object { Write-Host $_.Line -ForegroundColor Red }
    } else {
        Write-Host "No compilation errors found in log." -ForegroundColor Green
    }
    Write-Host ""
}

# Method 2: Check for Unity installation and run batch mode compilation check
$unityPaths = @(
    "$env:ProgramFiles\Unity\Hub\Editor\*\Editor\Unity.exe",
    "$env:ProgramFiles\Unity\Editor\Unity.exe",
    "${env:ProgramFiles(x86)}\Unity\Editor\Unity.exe"
)

$unityExe = $null
foreach ($path in $unityPaths) {
    $found = Get-ChildItem -Path $path -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) {
        $unityExe = $found.FullName
        break
    }
}

if ($unityExe) {
    Write-Host "=== Running Unity Batch Mode Compilation Check ===" -ForegroundColor Yellow
    Write-Host "Unity found at: $unityExe" -ForegroundColor Gray
    
    $logFile = "unity-compile-log.txt"
    $fullProjectPath = (Resolve-Path $projectPath).Path
    
    Write-Host "Running Unity in batch mode (this may take a moment)..." -ForegroundColor Gray
    
    & $unityExe -batchmode -quit -projectPath $fullProjectPath -logFile $logFile 2>&1 | Out-Null
    
    if (Test-Path $logFile) {
        Write-Host ""
        Write-Host "=== Compilation Results ===" -ForegroundColor Yellow
        $content = Get-Content $logFile -Raw
        
        # Check for errors
        if ($content -match "error CS\d+") {
            Write-Host "COMPILATION ERRORS FOUND:" -ForegroundColor Red
            $errorLines = Select-String -Path $logFile -Pattern "error CS\d+|Compilation failed" -Context 2,5
            $errorLines | ForEach-Object {
                Write-Host "---" -ForegroundColor DarkGray
                Write-Host $_.Context.PreContext -ForegroundColor Gray
                Write-Host $_.Line -ForegroundColor Red
                Write-Host $_.Context.PostContext -ForegroundColor Gray
            }
        } else {
            Write-Host "No compilation errors found!" -ForegroundColor Green
        }
        
        # Show summary
        $errorCount = ([regex]::Matches($content, "error CS\d+")).Count
        $warningCount = ([regex]::Matches($content, "warning CS\d+")).Count
        
        Write-Host ""
        Write-Host "Summary:" -ForegroundColor Cyan
        Write-Host "  Errors: $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { "Red" } else { "Green" })
        Write-Host "  Warnings: $warningCount" -ForegroundColor $(if ($warningCount -gt 0) { "Yellow" } else { "Gray" })
        
        Write-Host ""
        Write-Host "Full log saved to: $logFile" -ForegroundColor Gray
    }
} else {
    Write-Host "Unity executable not found in standard locations." -ForegroundColor Yellow
    Write-Host "Please specify Unity path manually or check Unity Editor log at:" -ForegroundColor Yellow
    Write-Host "  $logPath" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Done!" -ForegroundColor Cyan
