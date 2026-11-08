# Layout probe for issue #21 (plan.md, *Layout probe*). Read-only: opens each archive under maindata with
# FileShare 'ReadWrite, Delete', writes nothing into the installation, and prints entry paths, numbers and at most
# two bytes per marker. Set $root to the Anno 117 installation folder (no trailing backslash) and run it in
# Windows PowerShell 5.1 or PowerShell 7; the result is written to censio-rda-probe.txt on the desktop.

$root = 'D:\Games\Anno 117'
$out = Join-Path $env:USERPROFILE 'Desktop\censio-rda-probe.txt'
$detail = 'config.rda', 'en_us0.rda', 'zz_patchfiles_200.rda'
$maxBlocks = 1048576

function Get-Hex2([System.IO.BinaryReader] $r, [long] $pos) {
    if ($pos -lt 0 -or $pos -gt $r.BaseStream.Length - 2) { return 'outside file' }
    $r.BaseStream.Position = $pos
    ($r.ReadBytes(2) | ForEach-Object { $_.ToString('X2') }) -join ' '
}

& {
    Get-ChildItem -LiteralPath (Join-Path $root 'maindata') -File -Filter '*.rda' | Sort-Object Name | ForEach-Object {
        $name = $_.Name
        $len = $_.Length
        $fs = [System.IO.File]::Open($_.FullName, 'Open', 'Read', 'ReadWrite, Delete')
        $r = New-Object System.IO.BinaryReader($fs)
        try {
            $fs.Position = 784
            $first = $r.ReadUInt64()
            $pos = $first; $blocks = 0; $flags = @{}; $firstOf = @{}; $seen = @{}
            $end = "chain error: more than $maxBlocks blocks"
            while ($blocks -lt $maxBlocks) {
                if ($pos -eq [uint64]$len) { $end = 'chain ends at file length'; break }
                if ($pos -lt 792 -or $pos -gt [uint64]($len - 32)) { $end = "chain error: offset $pos outside the file"; break }
                if ($seen.ContainsKey($pos)) { $end = "chain error: offset $pos seen before"; break }
                $seen[$pos] = $true
                $fs.Position = [long]$pos
                $f = $r.ReadUInt32(); $null = $r.ReadBytes(20); $next = $r.ReadUInt64()
                if (-not $flags.ContainsKey($f)) { $flags[$f] = 0; $firstOf[$f] = $pos }
                $flags[$f]++; $blocks++; $pos = $next
            }
            $fl = ($flags.Keys | Sort-Object | ForEach-Object { "$_=$($flags[$_])" }) -join ', '
            "$name | length $len | first block at $first | blocks $blocks | $end | flags $fl"
            if ($detail -contains $name) {
                foreach ($f in ($firstOf.Keys | Sort-Object)) {
                    $b = [long]$firstOf[$f]
                    $fs.Position = $b
                    $null = $r.ReadUInt32(); $n = $r.ReadUInt32(); $ds = $r.ReadUInt64(); $dd = $r.ReadUInt64(); $next = $r.ReadUInt64()
                    $dir = $b - [long]$ds
                    "  block flags $f at $b | files $n | directory stored $ds | decompressed $dd | files x 560 = $([long]$n * 560) | next $next | directory first bytes $(Get-Hex2 $r $dir)"
                    if ($dir -lt 16) { '  directory outside file'; continue }
                    if ($f -band 4) {
                        $fs.Position = $dir - 16
                        $cs = $r.ReadUInt64(); $us = $r.ReadUInt64()
                        "  memory-resident sizes: stored $cs | decompressed $us | data first bytes $(Get-Hex2 $r ($dir - 16 - [long]$cs))"
                    }
                    if (($f -band 2) -or $n -eq 0) { continue }
                    $e = New-Object byte[] 560
                    if ($f -band 1) {
                        $fs.Position = $dir + 2
                        $z = New-Object System.IO.Compression.DeflateStream($fs, [System.IO.Compression.CompressionMode]::Decompress, $true)
                        $got = 0
                        while ($got -lt 560) { $k = $z.Read($e, $got, 560 - $got); if ($k -le 0) { break }; $got += $k }
                        $z.Dispose()
                    }
                    else {
                        $fs.Position = $dir
                        $e = $r.ReadBytes(560)
                    }
                    $p = [System.Text.Encoding]::Unicode.GetString($e, 0, 520)
                    $i = $p.IndexOf([char]0)
                    if ($i -ge 0) { $p = $p.Substring(0, $i) }
                    $off = [BitConverter]::ToUInt64($e, 520); $cs = [BitConverter]::ToUInt64($e, 528); $us = [BitConverter]::ToUInt64($e, 536)
                    $data = if ($f -band 4) { 'relative to block data' } else { Get-Hex2 $r ([long]$off) }
                    "  first entry: $p | offset $off | stored $cs | size $us | data first bytes $data"
                }
            }
        }
        catch { "$name | probe error: $($_.Exception.GetType().Name)" }
        finally { $r.Dispose() }
    }
} | Out-File -LiteralPath $out -Encoding utf8
"Written to $out"
