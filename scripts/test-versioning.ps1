#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Test script for validating the versioning logic used in GitHub Actions workflow.

.DESCRIPTION
    This script simulates the version determination logic used in the GitHub Actions 
    workflow to help validate versioning behavior locally before pushing changes.

.PARAMETER ProjectPath
    Path to the .csproj file. Defaults to the standard project path.

.PARAMETER TestSuffix
    Optional version suffix to test pre-release behavior.

.EXAMPLE
    ./test-versioning.ps1
    Tests with current project version

.EXAMPLE  
    ./test-versioning.ps1 -TestSuffix "alpha"
    Tests with alpha pre-release suffix
#>

param(
    [string]$ProjectPath = "forza-telemetry-client-winui/forza-telemetry-client-winui.csproj",
    [string]$TestSuffix = ""
)

function Get-ProjectVersion {
    param([string]$Path)
    
    try {
        $version = dotnet msbuild $Path -getProperty:Version -v:q 2>$null
        if ($LASTEXITCODE -eq 0) {
            return $version.Trim()
        }
    } catch {
        Write-Warning "Could not read version from project file using dotnet msbuild"
    }
    
    # Fallback: parse XML directly
    try {
        [xml]$proj = Get-Content $Path
        $versionNode = $proj.Project.PropertyGroup.Version | Where-Object { $_ -ne $null } | Select-Object -First 1
        return $versionNode
    } catch {
        throw "Could not read version from project file: $Path"
    }
}

function Test-VersionLogic {
    param(
        [string]$BaseVersion,
        [string]$VersionSuffix
    )
    
    Write-Host "🧪 Testing Version Logic" -ForegroundColor Cyan
    Write-Host "Base Version: $BaseVersion" -ForegroundColor Yellow
    Write-Host "Version Suffix: '$VersionSuffix'" -ForegroundColor Yellow
    Write-Host ""
    
    # Try to get latest tag
    $latestTag = ""
    try {
        $latestTag = git describe --tags --abbrev=0 2>$null
        if ($LASTEXITCODE -ne 0) { $latestTag = "" }
    } catch {
        $latestTag = ""
    }
    
    Write-Host "Latest Git Tag: '$latestTag'" -ForegroundColor Yellow
    Write-Host ""
    
    $finalVersion = $BaseVersion
    $isPreRelease = $false
    $scenario = ""
    
    # Version resolution logic (matches workflow)
    if (![string]::IsNullOrEmpty($VersionSuffix)) {
        $finalVersion = "$BaseVersion-$VersionSuffix"
        $isPreRelease = $true
        $scenario = "Pre-release version with suffix"
        Write-Host "✨ Scenario: $scenario" -ForegroundColor Green
    }
    elseif ([string]::IsNullOrEmpty($latestTag)) {
        $scenario = "New release (no existing tags)"
        Write-Host "✨ Scenario: $scenario" -ForegroundColor Green
    }
    elseif ($latestTag -eq "v$BaseVersion") {
        $commitCount = git rev-list --count HEAD
        $shortSha = git rev-parse --short HEAD
        $finalVersion = "$BaseVersion+build.$commitCount.sha.$shortSha"
        $scenario = "Build from existing tag version"
        Write-Host "✨ Scenario: $scenario" -ForegroundColor Green
    }
    else {
        $scenario = "New release (version newer than latest tag)"
        Write-Host "✨ Scenario: $scenario" -ForegroundColor Green
    }
    
    Write-Host ""
    Write-Host "📋 Results:" -ForegroundColor Cyan
    Write-Host "  Final Version: $finalVersion" -ForegroundColor White
    Write-Host "  Is Pre-release: $isPreRelease" -ForegroundColor White
    Write-Host "  Tag Name: v$BaseVersion" -ForegroundColor White
    Write-Host "  Release Name: v$finalVersion" -ForegroundColor White
    
    return @{
        FinalVersion = $finalVersion
        BaseVersion = $BaseVersion
        IsPreRelease = $isPreRelease
        Scenario = $scenario
        TagName = "v$BaseVersion"
        ReleaseName = "v$finalVersion"
    }
}

# Main execution
try {
    Write-Host "🔍 Forza Telemetry Client - Version Testing Script" -ForegroundColor Magenta
    Write-Host "===================================================" -ForegroundColor Magenta
    Write-Host ""
    
    if (!(Test-Path $ProjectPath)) {
        throw "Project file not found: $ProjectPath"
    }
    
    $baseVersion = Get-ProjectVersion -Path $ProjectPath
    Write-Host "✅ Successfully read project version: $baseVersion" -ForegroundColor Green
    Write-Host ""
    
    # Test current configuration
    $result1 = Test-VersionLogic -BaseVersion $baseVersion -VersionSuffix $TestSuffix
    
    # If no test suffix provided, also test pre-release scenario
    if ([string]::IsNullOrEmpty($TestSuffix)) {
        Write-Host ""
        Write-Host "=" * 60 -ForegroundColor Gray
        Write-Host ""
        
        Write-Host "🧪 Additional Test: Pre-release Scenario" -ForegroundColor Cyan
        $result2 = Test-VersionLogic -BaseVersion $baseVersion -VersionSuffix "alpha"
    }
    
    Write-Host ""
    Write-Host "✅ Version testing completed successfully!" -ForegroundColor Green
    
} catch {
    Write-Host ""
    Write-Host "❌ Error: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}