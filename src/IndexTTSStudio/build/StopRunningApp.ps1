# Closes any Yakkinator running from the folder a build or publish is about to overwrite, so its
# exe and DLLs aren't locked. Copies running from anywhere else (e.g. an installed release) are
# left alone. Called from IndexTTSStudio.csproj; never fails the build.
param(
    [Parameter(Mandatory = $true)][string]$Dir,
    [string]$Name = 'Yakkinator'
)

try {
    $target = [System.IO.Path]::GetFullPath($Dir).TrimEnd('\') + '\'
    $running = Get-Process -Name $Name -ErrorAction SilentlyContinue | Where-Object {
        $_.Path -and $_.Path.StartsWith($target, [System.StringComparison]::OrdinalIgnoreCase)
    }

    foreach ($p in $running) {
        Write-Host "Closing $Name (PID $($p.Id)) running from $target"
        # A normal close lets the app shut down its Python/Echo backends and free the GPU
        [void]$p.CloseMainWindow()
        if (-not $p.WaitForExit(8000)) {
            Write-Host "$Name (PID $($p.Id)) didn't close; ending it and its backends"
            & taskkill.exe /PID $p.Id /T /F | Out-Null
            [void]$p.WaitForExit(5000)
        }
    }
}
catch {
    Write-Host "StopRunningApp: $($_.Exception.Message)"
}
exit 0
