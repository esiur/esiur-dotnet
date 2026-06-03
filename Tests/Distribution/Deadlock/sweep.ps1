# Scalability sweep for the distributed deadlock test (loopback).
# For each size, starts a fresh server hosting a ring of `nodes` plus `res1`+`res2` densely
# cross-referencing resources, then runs the client (WaitWithCycleDetection) and records the
# graph size, back-edges, cycle-breaks, and completion time. Output: sweep-results.csv / .md
$ErrorActionPreference = "SilentlyContinue"
Set-Location $PSScriptRoot\..\..\..

$srv = "Tests\Distribution\Deadlock\Server\bin\Release\net10.0\Esiur.Tests.Deadlock.Server.exe"
$cli = "Tests\Distribution\Deadlock\Client\bin\Release\net10.0\Esiur.Tests.Deadlock.Client.exe"

$sizes = @(25, 50, 100, 200, 400, 800)
$port  = 11200
$rows  = @()

foreach ($s in $sizes) {
    $port++
    Get-Process -Name "Esiur.Tests.Deadlock.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
    $so = "Tests\Distribution\Deadlock\sweep_srv_$s.txt"
    Remove-Item $so -ErrorAction SilentlyContinue
    $p = Start-Process -FilePath $srv -ArgumentList "--port $port --topology ring --nodes $s --res1 $s --res2 $s" -PassThru -NoNewWindow -RedirectStandardOutput $so

    # Wait until the server reports it is listening (poll up to 90s).
    $census = ""
    for ($t = 0; $t -lt 180; $t++) {
        Start-Sleep -Milliseconds 500
        $c = Get-Content $so -ErrorAction SilentlyContinue
        if ($c -match "Listening") { $census = ($c | Select-Object -First 1); break }
    }

    $backEdges = if ($census -match "backEdges=(\d+)") { $matches[1] } else { "?" }

    # Run the client; parse its summary.
    $out = & $cli --host 127.0.0.1 --port $port --nodes $s --mode WaitWithCycleDetection --iterations 3 --stall-ms 30000 --hard-ms 180000 2>&1
    $total = $s * 3
    $median = ($out | Select-String "completion ms: median=([\d.]+)").Matches.Groups[1].Value
    $attached = ($out | Select-String "resources attached per run \(max\)=(\d+)").Matches.Groups[1].Value
    $breaks = ($out | Select-String "cycle-breaks total=(\d+)").Matches.Groups[1].Value
    $completed = ($out | Select-String "completed=(\d+)").Matches.Groups[1].Value
    $deadlocked = ($out | Select-String "deadlocked=(\d+)").Matches.Groups[1].Value

    $rows += [pscustomobject]@{ nodes=$s; res1=$s; res2=$s; totalResources=$total; backEdges=$backEdges; attached=$attached; cycleBreaks=$breaks; medianMs=$median; completed=$completed; deadlocked=$deadlocked }
    Write-Output "size=$s total=$total back=$backEdges attached=$attached breaks=$breaks median=$median ms completed=$completed"

    Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    Get-Process -Name "Esiur.Tests.Deadlock.Server" -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Seconds 1
}

$rows | Export-Csv -Path "Tests\Distribution\Deadlock\sweep-results.csv" -NoTypeInformation
$md = "| nodes | res1 | res2 | total resources | back-edges | attached/run | cycle-breaks(3 runs) | median ms | completed | deadlocked |`n"
$md += "|------:|-----:|-----:|----------------:|-----------:|-------------:|---------------------:|----------:|----------:|-----------:|`n"
foreach ($r in $rows) { $md += "| $($r.nodes) | $($r.res1) | $($r.res2) | $($r.totalResources) | $($r.backEdges) | $($r.attached) | $($r.cycleBreaks) | $($r.medianMs) | $($r.completed) | $($r.deadlocked) |`n" }
Set-Content -Path "Tests\Distribution\Deadlock\sweep-results.md" -Value $md -Encoding utf8
Write-Output "=== DONE ==="
Write-Output $md
