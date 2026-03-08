$projects = @(
    @{ name="wordpress"; displayName="WVU Medicine WordPress"; keywords="wordpress,divi,wvumedicine,algolia,wvu,plugin,modules,divi5"; facts=$null; isActive=$true },
    @{ name="apex"; displayName="APEX Middleware"; keywords="apex,mcp,qdrant,ollama,blazor,middleware,context,injection"; facts=$null; isActive=$true },
    @{ name="peakhealth"; displayName="Peak Health"; keywords="peakhealth,peak,medicare,hangfire,pdf,itext,provider"; facts=$null; isActive=$true },
    @{ name="findadoc"; displayName="Find a Doc"; keywords="findadoc,doctors,provider,search,scheduling,dotnet,redirect"; facts=$null; isActive=$true },
    @{ name="crunchtime"; displayName="CrunchTime Gourmet Popcorn"; keywords="crunchtime,popcorn,trailer,store,inventory"; facts=$null; isActive=$true }
)

foreach ($p in $projects) {
    $body = $p | ConvertTo-Json
    try {
        $r = Invoke-RestMethod -Uri 'http://127.0.0.1:5000/api/projects' -Method POST -Body $body -ContentType 'application/json'
        Write-Host "OK: $($r.name) (ID $($r.projectId))"
    } catch {
        Write-Host "FAIL $($p.name): $($_.Exception.Message)"
    }
}
