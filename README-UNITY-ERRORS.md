# Checking Unity Compilation Errors from Command Line

## Quick Methods

### Method 1: Check Unity Editor Log (Fastest)
```powershell
# View recent errors from Unity Editor log
Get-Content "$env:LOCALAPPDATA\Unity\Editor\Editor.log" -Tail 100 | Select-String "error CS"
```

### Method 2: Use the PowerShell Script
```powershell
# Run the provided script
.\check-unity-errors-simple.ps1
```

### Method 3: Unity Batch Mode (Most Accurate)
```powershell
# Find Unity executable (adjust version as needed)
$unityPath = "C:\Program Files\Unity\Hub\Editor\6000.2.8f1\Editor\Unity.exe"

# Run compilation check
& $unityPath -batchmode -quit -projectPath "Maelstrom" -logFile "compile-log.txt"

# Check results
Get-Content "compile-log.txt" | Select-String "error CS"
```

### Method 4: Direct Log File Check
```powershell
# Check for errors in Unity log
$logPath = "$env:LOCALAPPDATA\Unity\Editor\Editor.log"
if (Test-Path $logPath) {
    Get-Content $logPath | Select-String "error CS\d+" -Context 2,5
}
```

## Common Error Patterns

- **Missing type errors**: `error CS0246: The type or namespace name 'X' could not be found`
- **Compilation failed**: Usually appears at the end of compilation
- **Assembly errors**: `error CS0006: Metadata file 'X.dll' could not be found`

## Finding Unity Installation

Unity is typically installed at:
- `C:\Program Files\Unity\Hub\Editor\[VERSION]\Editor\Unity.exe`
- `C:\Program Files\Unity\Editor\Unity.exe` (older installations)

To find your Unity version, check:
```
Maelstrom\ProjectSettings\ProjectVersion.txt
```

## Notes

- Unity Editor log is updated in real-time when Unity is running
- Batch mode requires Unity to be closed
- Log file location: `%LOCALAPPDATA%\Unity\Editor\Editor.log`
