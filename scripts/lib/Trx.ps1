#Requires -Version 7.0
<#
.SYNOPSIS
    Gemeinsame TRX-Helfer fuer die Test-Skripte (per Dot-Sourcing eingebunden).

.DESCRIPTION
    Kapselt das Parsen des VSTest-TRX-Formats (Namespace, UnitTestResult-Knoten,
    Times-Attribute), damit XPath- und Namespace-Details nicht in jedem Skript
    gepflegt werden. Verwendung im aufrufenden Skript:

        . (Join-Path $PSScriptRoot "lib/Trx.ps1")
#>

$script:TrxNamespaceUri = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"

function Get-TrxDocument {
    # Laedt eine TRX-Datei als XML-Dokument; $null bei fehlender/unauswertbarer Datei.
    # Rueckgabe per ","-Operator: XmlDocument implementiert IEnumerable ueber die
    # Kindknoten und wuerde sonst beim Funktions-Output unrollt werden.
    param([string]$TrxPath)
    if (-not (Test-Path $TrxPath)) { return $null }
    try {
        [xml]$trx = Get-Content $TrxPath -Raw
        return ,$trx
    }
    catch {
        return $null
    }
}

function New-TrxNamespaceManager {
    # Namespace-Manager mit dem VSTest-TRX-Präfix "t" fuer XPath-Abfragen.
    # Rueckgabe per ","-Operator: XmlNamespaceManager implementiert IEnumerable und
    # wuerde sonst beim Funktions-Output in die registrierten Praefixe unrollt werden.
    param([xml]$Trx)
    $ns = [System.Xml.XmlNamespaceManager]::new($Trx.NameTable)
    $ns.AddNamespace("t", $script:TrxNamespaceUri)
    return ,$ns
}

function Get-TrxTestResults {
    # Liefert testName -> outcome je UnitTestResult; $null bei fehlender/unauswertbarer Datei.
    param([string]$TrxPath)
    $trx = Get-TrxDocument -TrxPath $TrxPath
    if ($null -eq $trx) { return $null }
    $ns = New-TrxNamespaceManager -Trx $trx
    $resultNodes = @($trx.SelectNodes("//t:UnitTestResult", $ns))
    if ($resultNodes.Count -eq 0) { return $null }
    $results = [ordered]@{}
    foreach ($node in $resultNodes) {
        $results["$($node.testName)"] = "$($node.outcome)"
    }
    return $results
}

function Get-TrxRunStartTime {
    # Lauf-Startzeitstempel (UTC) aus dem <Times start="...">-Attribut des TestRun-Knotens;
    # $null wenn nicht auswertbar. Robuster als LastWriteTimeUtc, weil der Zeitstempel vom
    # Lauf selbst stammt und nicht vom Download-/Kopierzeitpunkt der Datei.
    param([string]$TrxPath)
    $trx = Get-TrxDocument -TrxPath $TrxPath
    if ($null -eq $trx) { return $null }
    $ns = New-TrxNamespaceManager -Trx $trx
    $timesNode = $trx.SelectSingleNode("//t:Times", $ns)
    if ($null -eq $timesNode -or [string]::IsNullOrWhiteSpace("$($timesNode.start)")) { return $null }
    try {
        return [datetimeoffset]::Parse("$($timesNode.start)").UtcDateTime
    }
    catch {
        return $null
    }
}
