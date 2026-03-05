# ClaudeNest Agent Installer for Windows
# Backend URL and version are substituted by the server when this script is downloaded.
# The pairing token must be provided via the CLAUDENEST_TOKEN environment variable.
$BackendUrl = "%%BACKEND_URL%%"
$Version = "%%LATEST_VERSION%%"

$ErrorActionPreference = "Stop"

$Token = $env:CLAUDENEST_TOKEN
if (-not $Token) {
    Write-Error "CLAUDENEST_TOKEN environment variable is required.`nUsage: `$env:CLAUDENEST_TOKEN='<token>'; irm '$BackendUrl/install.ps1' | iex"
    exit 1
}

$Repo = "GordonBeeming/ClaudeNest"
$InstallDir = Join-Path $env:USERPROFILE ".claudenest\bin"

Write-Host "ClaudeNest Agent Installer" -ForegroundColor Cyan
Write-Host "==========================" -ForegroundColor Cyan
Write-Host ""

# Detect architecture
$Arch = $env:PROCESSOR_ARCHITECTURE
switch ($Arch) {
    "AMD64" { $Rid = "win-x64" }
    "ARM64" { $Rid = "win-arm64" }
    default { Write-Error "Unsupported architecture: $Arch"; exit 1 }
}

$BinaryName = "claudenest-agent-${Rid}.exe"
$DownloadUrl = "https://github.com/${Repo}/releases/download/agent-v${Version}/${BinaryName}"
$VersionedName = "claudenest-agent-${Version}.exe"
$ConvenienceName = "claudenest-agent.exe"

Write-Host "Platform: $Rid"
Write-Host "Version:  $Version"
Write-Host ""

# Create install directory
if (-not (Test-Path $InstallDir)) {
    New-Item -ItemType Directory -Path $InstallDir -Force | Out-Null
}

# Enable TLS 1.2 (required for GitHub on PS 5.1)
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

# Download binary
Write-Host "Downloading agent binary..."
$VersionedPath = Join-Path $InstallDir $VersionedName
$wc = New-Object System.Net.WebClient
try {
    $wc.DownloadFile($DownloadUrl, $VersionedPath)
} finally {
    $wc.Dispose()
}

# Unblock the file
Unblock-File -Path $VersionedPath -ErrorAction SilentlyContinue

# Copy for CLI convenience (Windows doesn't support symlinks without admin)
$ConveniencePath = Join-Path $InstallDir $ConvenienceName
Copy-Item -Path $VersionedPath -Destination $ConveniencePath -Force
Unblock-File -Path $ConveniencePath -ErrorAction SilentlyContinue

Write-Host "Binary installed to $VersionedPath"
Write-Host ""

# Prompt for Windows password (needed for Windows Service registration)
Write-Host "The agent will be installed as a Windows Service." -ForegroundColor Yellow
Write-Host "Your Windows account password is required for service registration." -ForegroundColor Yellow
Write-Host ""

# Validate credentials before proceeding
$MaxAttempts = 3
$Attempt = 0
$Password = $null
$AccountName = "$env:USERDOMAIN\$env:USERNAME"

while ($Attempt -lt $MaxAttempts) {
    $Attempt++
    $Credential = Get-Credential -UserName $AccountName -Message "Enter your Windows password for service registration (attempt $Attempt of $MaxAttempts)"
    if (-not $Credential) {
        Write-Error "Credential prompt was cancelled."
        exit 1
    }
    $Password = $Credential.GetNetworkCredential().Password

    # Validate the credentials using a local logon test
    Add-Type -AssemblyName System.DirectoryServices.AccountManagement
    $ContextType = [System.DirectoryServices.AccountManagement.ContextType]::Machine
    try {
        $PrincipalContext = New-Object System.DirectoryServices.AccountManagement.PrincipalContext($ContextType)
        $Valid = $PrincipalContext.ValidateCredentials($env:USERNAME, $Password)
        $PrincipalContext.Dispose()
    } catch {
        # If machine context fails, try domain context
        try {
            $ContextType = [System.DirectoryServices.AccountManagement.ContextType]::Domain
            $PrincipalContext = New-Object System.DirectoryServices.AccountManagement.PrincipalContext($ContextType)
            $Valid = $PrincipalContext.ValidateCredentials($env:USERNAME, $Password)
            $PrincipalContext.Dispose()
        } catch {
            $Valid = $false
        }
    }

    if ($Valid) {
        Write-Host "Credentials verified successfully." -ForegroundColor Green
        break
    } else {
        Write-Host "Invalid password. Please try again." -ForegroundColor Red
        $Password = $null
    }
}

if (-not $Password) {
    Write-Error "Failed to validate credentials after $MaxAttempts attempts."
    exit 1
}

# Run the install command
Write-Host ""
Write-Host "Pairing agent with backend..."
$CurrentDir = (Get-Location).Path
& $VersionedPath install --token $Token --backend $BackendUrl --service-password $Password --path $CurrentDir
