[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$output = dotnet build VoidCraftLauncher/VoidCraftLauncher.csproj -c Debug --no-incremental 2>&1
$errors = $output | Where-Object { $_ -match "error CS" }
$errors | Set-Content -Path "build_errors.txt" -Encoding utf8
