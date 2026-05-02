[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Name
)

$ErrorActionPreference = 'Stop'

dotnet ef migrations add $Name `
    --project ./src/RocketLeagueStats.Core `
    --startup-project ./src/RocketLeagueStats.WebApi `
    --output-dir Persistence/Migrations
