$folders = Get-ChildItem "C:\Users\sufo\AppData\Local\Blizzard Entertainment\Overwatch" -Recurse -Directory -Filter "Highlights" | Where-Object { (Get-ChildItem -Path $_.FullName -File).Count -gt 0 }

foreach ($folder in $folders) {
    Write-Host "Processing $($folder.FullName)..."
    dotnet run --project "c:\Users\sufo\Scripts\owDataExtractorReplayParser\OWLib\ReplayAnalyzer\ReplayAnalyzer.csproj" -- "$($folder.FullName)"
}
