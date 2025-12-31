try {
    Write-Host "Loading System.Drawing..."
    Add-Type -AssemblyName System.Drawing
    
    $path = "c:\Users\adamj\Documents\GitHub\TESTLAUNCHER3\VoidCraftLauncher\src\Assets\icon.png"
    $dest = "c:\Users\adamj\Documents\GitHub\TESTLAUNCHER3\VoidCraftLauncher\src\Assets\icon.ico"

    if (-not (Test-Path $path)) {
        throw "Input file not found: $path"
    }

    Write-Host "Reading image from $path ..."
    $img = [System.Drawing.Bitmap]::FromFile($path)
    
    Write-Host "Creating icon handle..."
    $handle = $img.GetHicon()
    
    Write-Host "Creating icon object..."
    $icon = [System.Drawing.Icon]::FromHandle($handle)
    
    Write-Host "Saving to $dest ..."
    $stream = [System.IO.File]::OpenWrite($dest)
    $icon.Save($stream)
    $stream.Close()
    
    $img.Dispose()
    [System.Drawing.Icon]::DestroyIcon($handle)
    
    Write-Host "SUCCESS: Icon saved."
}
catch {
    Write-Error "ERROR DETAILS: $($_.Exception.ToString())"
    exit 1
}
