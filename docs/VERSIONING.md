# Versioning Strategy

This document describes the versioning strategy used by the Forza Telemetry Client.

## Overview

The project uses [semantic versioning](https://semver.org/) with the following format:
```
MAJOR.MINOR.PATCH[-PRERELEASE][+BUILD]
```

## Version Sources

The version is determined by the GitHub Actions workflow during release builds using the following priority:

1. **Project File Version**: Defined in `forza-telemetry-client-winui.csproj` as `<Version>X.Y.Z</Version>`
2. **Version Suffix**: Optional pre-release identifier (alpha, beta, preview, experiment)
3. **Build Metadata**: Auto-generated when the project version matches an existing tag

## Version Resolution Logic

### 1. Pre-release Versions
If the project file contains a version suffix:
- **Format**: `1.1.0-alpha`
- **Pre-release**: `true`
- **Use case**: Development snapshots, alpha/beta releases

### 2. New Release Versions
If no tags exist or the project version is newer than the latest tag:
- **Format**: `1.1.0`
- **Pre-release**: `false`
- **Use case**: Official releases

### 3. Build Versions
If a tag already exists for the current project version:
- **Format**: `1.1.0+build.123.sha.abc1234`
- **Pre-release**: `false`
- **Use case**: Hotfixes, multiple builds from the same version

## Configuration

The versioning behavior can be configured in the `.github/workflows/dotnet-release.yml` file:

```yaml
env:
  # Check if tag exists before creating new one
  CHECK_TAG_EXISTENCE_BEFORE_CREATING_TAG: false
  
  # Release configuration
  IS_PRE_RELEASE: false  # Overridden by version logic
  SKIP_IF_RELEASE_EXIST: true
  MAKE_LATEST: true
```

## Examples

| Project Version | Existing Tags | Result | Pre-release | Use Case |
|----------------|---------------|--------|-------------|----------|
| `1.0.0` | None | `1.0.0` | false | Initial release |
| `1.1.0-alpha` | `v1.0.0` | `1.1.0-alpha` | true | Alpha testing |
| `1.1.0` | `v1.0.0` | `1.1.0` | false | Minor release |
| `1.1.0` | `v1.1.0` | `1.1.0+build.45.sha.def5678` | false | Hotfix build |

## Updating Versions

To release a new version:

1. Update the `<Version>` in `forza-telemetry-client-winui/forza-telemetry-client-winui.csproj`
2. For pre-releases, add a suffix: `<Version>1.2.0-beta</Version>`
3. Commit and trigger the workflow
4. The workflow will automatically create tags and releases

## Best Practices

- **Increment versions** according to semantic versioning rules
- **Use pre-release suffixes** for development versions
- **Keep version properties consistent** (don't duplicate version information)
- **Let the workflow handle** tag creation and release naming