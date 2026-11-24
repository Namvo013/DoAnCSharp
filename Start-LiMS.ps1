[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Web", "Admin")]
    [string]$Mode
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$apiProject = Join-Path $root "LiMS.Api\LiMS.Api.csproj"
$webProject = Join-Path $root "LiMS.Web\LiMS.Web.csproj"
$adminProject = Join-Path $root "LiMS.Admin\LiMS.Admin.csproj"
$apiDirectory = Join-Path $root "LiMS.Api"
$webDirectory = Join-Path $root "LiMS.Web"
$apiUrl = "http://localhost:5107"
$apiHealthUrl = "$apiUrl/api/library/health"
$webUrl = "http://localhost:5500"
$apiPort = 5107

foreach ($project in @($apiProject, $webProject, $adminProject)) {
    if (-not (Test-Path -LiteralPath $project -PathType Leaf)) {
        throw "Project file not found: $project"
    }
}

$dotnetCommand = Get-Command "dotnet.exe" -ErrorAction Stop
$dotnetPath = $dotnetCommand.Source

function Test-LiMSApi {
    try {
        $health = Invoke-RestMethod -Uri $apiHealthUrl -TimeoutSec 3 -ErrorAction Stop
        if ($health.status -eq "ok" -and $health.database -eq $true) {
            return $true
        }

        $script:lastApiCheck = "API reported status '$($health.status)' and database '$($health.database)'."
    }
    catch {
        $script:lastApiCheck = $_.Exception.Message
    }

    return $false
}

function Test-LiMSRequiredRoutes {
    $requiredRoutes = @(
        @{
            Uri = "$apiUrl/api/admin/database-backups"
            Name = "database-backups"
        },
        @{
            Uri = "$apiUrl/api/admin/books/paged?page=1&pageSize=50"
            Name = "paged book listing"
        }
    )

    foreach ($route in $requiredRoutes) {
        try {
            $null = Invoke-WebRequest `
                -Uri $route.Uri `
                -TimeoutSec 3 `
                -UseBasicParsing `
                -ErrorAction Stop
        }
        catch {
            $response = $_.Exception.Response
            if ($response) {
                $statusCode = [int]$response.StatusCode
                if ($statusCode -eq 401 -or $statusCode -eq 403) {
                    continue
                }
                if ($statusCode -eq 404) {
                    $script:lastApiCheck = "API is healthy but is missing the required '$($route.Name)' route (HTTP 404)."
                    return $false
                }
            }

            $script:lastApiCheck = $_.Exception.Message
            return $false
        }
    }

    return $true
}

$apiHealthy = Test-LiMSApi
if ($apiHealthy -and -not (Test-LiMSRequiredRoutes)) {
    $apiListener = Get-NetTCPConnection -LocalPort $apiPort -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1
    $listenerProcessId = if ($apiListener) { $apiListener.OwningProcess } else { "unknown" }
    throw "The API on port $apiPort is an older build and is missing required routes. Stop its process (PID $listenerProcessId), then run this launcher again. $script:lastApiCheck"
}

if (-not $apiHealthy) {
    $apiListener = Get-NetTCPConnection -LocalPort $apiPort -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1

    if (-not $apiListener) {
        Write-Host "Starting LiMS API. Waiting for SQL Server connection..."
        $apiArguments = 'run --project "{0}" --launch-profile http' -f $apiProject
        Start-Process -FilePath $dotnetPath `
            -ArgumentList $apiArguments `
            -WorkingDirectory $apiDirectory | Out-Null
    }
    else {
        Write-Host "Port $apiPort is already listening. Waiting for the existing API; no duplicate will be started."
    }

    $apiReady = $false
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if ((Test-LiMSApi) -and (Test-LiMSRequiredRoutes)) {
            $apiReady = $true
            break
        }
        Start-Sleep -Seconds 2
    }

    if (-not $apiReady) {
        throw "LiMS API did not become ready at $apiHealthUrl. Last check: $script:lastApiCheck"
    }
}

Write-Host "LiMS API is ready and connected to the database."

if ($Mode -eq "Admin") {
    $existingAdmin = Get-Process -Name "LiMS.Admin" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($existingAdmin) {
        Write-Host "LiMS Admin is already running (PID $($existingAdmin.Id))."
        return
    }

    $adminArguments = 'run --project "{0}" --no-launch-profile' -f $adminProject
    $adminProcess = Start-Process -FilePath $dotnetPath `
        -ArgumentList $adminArguments `
        -WorkingDirectory $root `
        -PassThru
    Start-Sleep -Seconds 2
    if ($adminProcess.HasExited) {
        throw "LiMS Admin exited during startup. Check the Admin console for details."
    }

    Write-Host "LiMS Admin started (PID $($adminProcess.Id))."
    return
}

function Test-LiMSWeb {
    try {
        $response = Invoke-WebRequest -Uri $webUrl -TimeoutSec 3 -UseBasicParsing -ErrorAction Stop
        if ($response.StatusCode -ge 200 -and $response.StatusCode -lt 400) {
            return $true
        }

        $script:lastWebCheck = "Website returned HTTP $($response.StatusCode)."
    }
    catch {
        $script:lastWebCheck = $_.Exception.Message
    }

    return $false
}

if (-not (Test-LiMSWeb)) {
    $webListener = Get-NetTCPConnection -LocalPort 5500 -State Listen -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if (-not $webListener) {
        Write-Host "Starting LiMS Web..."
        $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
        $previousUrls = $env:ASPNETCORE_URLS
        try {
            $env:ASPNETCORE_ENVIRONMENT = "Development"
            $env:ASPNETCORE_URLS = $webUrl
            $webArguments = 'run --project "{0}" --no-launch-profile' -f $webProject
            Start-Process -FilePath $dotnetPath `
                -ArgumentList $webArguments `
                -WorkingDirectory $webDirectory | Out-Null
        }
        finally {
            $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment
            $env:ASPNETCORE_URLS = $previousUrls
        }
    }
    else {
        Write-Host "Port 5500 is already listening. Waiting for the existing website; no duplicate will be started."
    }

    $webReady = $false
    $deadline = (Get-Date).AddSeconds(120)
    while ((Get-Date) -lt $deadline) {
        if (Test-LiMSWeb) {
            $webReady = $true
            break
        }
        Start-Sleep -Seconds 2
    }

    if (-not $webReady) {
        throw "LiMS Web did not become ready at $webUrl. Last check: $script:lastWebCheck"
    }
}

Write-Host "LiMS Web is ready at $webUrl."
Start-Process $webUrl
