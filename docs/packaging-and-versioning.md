# Packaging and Versioning Strategy

This document describes the packaging, versioning, and release process for Shardis.

## Multi-Targeting Strategy

### Standard Packages (net8.0 + net9.0)

Most Shardis packages target **both .NET 8.0 and .NET 9.0** to provide maximum compatibility:

- `Shardis` (core)
- `Shardis.Migration`
- `Shardis.Migration.Marten`
- `Shardis.Migration.Sql`
- `Shardis.Query`
- `Shardis.Query.InMemory`
- `Shardis.Query.Marten`
- `Shardis.Redis`
- `Shardis.Marten`
- `Shardis.DependencyInjection`
- `Shardis.Logging.Console`
- `Shardis.Logging.Microsoft`

**Rationale**: These packages have minimal dependencies and can compile and run on both .NET 8 LTS and .NET 9.

### Entity Framework Core Packages (net10.0 only)

The following packages target **only .NET 10** due to dependencies on Entity Framework Core 10.x:

- `Shardis.Query.EntityFrameworkCore`
- `Shardis.Migration.EntityFrameworkCore`

**Rationale**: Entity Framework Core 10.x requires .NET 10. Rather than downgrade EF Core or maintain multiple versions, these packages explicitly require .NET 10+.

**Impact**: Applications targeting .NET 8 or .NET 9 should use Marten-based or in-memory query/migration providers instead.

## Compatibility Matrix

| Package | .NET 8 | .NET 9 | .NET 10 | Notes |
|---------|--------|--------|---------|-------|
| Shardis (core) | ✅ | ✅ | ✅ | Recommended for all |
| Shardis.Migration | ✅ | ✅ | ✅ | Core migration abstractions |
| Shardis.Migration.Marten | ✅ | ✅ | ✅ | Marten-based migration |
| Shardis.Migration.EntityFrameworkCore | ❌ | ❌ | ✅ | Requires EF Core 10.x |
| Shardis.Query | ✅ | ✅ | ✅ | Query abstractions |
| Shardis.Query.InMemory | ✅ | ✅ | ✅ | Testing/prototyping |
| Shardis.Query.Marten | ✅ | ✅ | ✅ | Marten-based querying |
| Shardis.Query.EntityFrameworkCore | ❌ | ❌ | ✅ | Requires EF Core 10.x |
| Shardis.Redis | ✅ | ✅ | ✅ | Redis shard map store |
| Shardis.DependencyInjection | ✅ | ✅ | ✅ | DI extensions |
| Shardis.Logging.* | ✅ | ✅ | ✅ | Logging adapters |

## Package Selection Guide

### For .NET 8 or .NET 9 Applications

**Recommended packages**:
- Core: `Shardis`
- Query: `Shardis.Query` + `Shardis.Query.Marten` or `Shardis.Query.InMemory`
- Migration: `Shardis.Migration` + `Shardis.Migration.Marten`
- Storage: `Shardis.Redis` (optional, for distributed shard maps)
- DI: `Shardis.DependencyInjection` (optional, for per-shard factories)

### For .NET 10+ Applications

**All packages are available**, including:
- EF Core support: `Shardis.Query.EntityFrameworkCore` + `Shardis.Migration.EntityFrameworkCore`
- Plus all packages listed above

### Decision Tree

```
Do you need Entity Framework Core integration?
├─ Yes → Use .NET 10
│   └─ Install: Shardis.Query.EntityFrameworkCore, Shardis.Migration.EntityFrameworkCore
└─ No → Use .NET 8 or .NET 9 (LTS recommended)
    └─ Install: Shardis + provider of choice (Marten, InMemory, Redis)
```

## Versioning Rules

Shardis follows [Semantic Versioning 2.0.0](https://semver.org/):

- **Major** (X.0.0): Breaking changes to public APIs
- **Minor** (0.X.0): New features, backward-compatible
- **Patch** (0.0.X): Bug fixes, backward-compatible

### Pre-release Versions

- **main branch commits**: Automatic pre-release packages (e.g., `0.3.0-prerelease.123`)
- **Pull requests**: PR-scoped pre-releases (e.g., `0.3.0-pr42.5`)
- **Tagged releases**: Stable versions (e.g., `0.3.0`)

### Version Synchronization

All Shardis packages share the **same version number** for a given release. This simplifies dependency management and ensures compatibility.

**Example**: If `Shardis` is at version `0.3.0`, then `Shardis.Query`, `Shardis.Migration`, etc., are also `0.3.0`.

## Breaking Change Policy

### What Constitutes a Breaking Change

- Removal of public types, methods, or properties
- Change in method signatures (parameters, return types)
- Change in behavior that could break existing code
- Removal of supported target frameworks

### How We Handle Breaking Changes

1. **Deprecation warnings**: Mark obsolete with `[Obsolete]` attribute, provide migration guidance
2. **Grace period**: Maintain deprecated APIs for at least one minor version
3. **CHANGELOG**: Document all breaking changes with migration instructions
4. **Major version bump**: Breaking changes trigger a major version increment

### Non-Breaking Changes

- Adding new optional parameters with defaults
- Adding new types, methods, or overloads
- Adding new target frameworks
- Performance improvements (without behavioral changes)
- Internal implementation changes

## Release Process

### Automated Releases (Recommended)

1. **Commit changes** to the `main` branch
2. **Tag the release**: `git tag v0.3.0 && git push --tags`
3. **GitHub Actions** automatically:
   - Runs full test suite
   - Packages all libraries with GitVersion-calculated version
   - Publishes to NuGet.org and GitHub Packages
   - Generates release notes from tag description

### Manual Release Steps (if needed)

```bash
# 1. Ensure clean state
git status  # should be clean
dotnet build Shardis.sln --configuration Release
dotnet test Shardis.sln --configuration Release

# 2. Update CHANGELOG.md with release notes
# Move items from [Unreleased] to [0.3.0] - YYYY-MM-DD

# 3. Commit and tag
git add CHANGELOG.md
git commit -m "Release 0.3.0"
git tag -a v0.3.0 -m "Release 0.3.0

- Feature X
- Feature Y
- Bug fix Z"
git push origin main --tags

# 4. CI/CD takes over from here
```

### Release Checklist

Before tagging a release:

- [ ] All tests pass (`dotnet test Shardis.sln --configuration Release`)
- [ ] CHANGELOG.md updated with version and date
- [ ] Public API changes reviewed (run `Shardis.PublicApi.Tests` if API changed)
- [ ] Documentation updated for new features
- [ ] Breaking changes documented with migration guide
- [ ] Version number follows semver rules
- [ ] README.md reflects current feature set

## Changelog Discipline

We follow [Keep a Changelog](https://keepachangelog.com/) format:

- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Features marked for removal
- **Removed**: Removed features (breaking change)
- **Fixed**: Bug fixes
- **Security**: Security fixes

Every user-facing change should have a CHANGELOG entry under `[Unreleased]` until release.

## Package Metadata

All packages share common metadata defined in `Directory.Build.props`:

- **Authors**: Jesper Veggerby
- **Company**: Shardis Project
- **License**: MIT
- **Repository**: https://github.com/veggerby/shardis
- **Icon**: `icon.png` (128x128, purple theme)
- **SourceLink**: Enabled for source debugging
- **Symbols**: Included as .snupkg

Individual packages define:
- `PackageId`
- `Version`
- `Description`
- `PackageTags`
- `PackageReadmeFile` (README.md in package root)
- `PackageReleaseNotes` (CHANGELOG link)

## NuGet Publishing

### Automatic Publishing

The GitHub Actions workflow (`.github/workflows/publish.yml`) handles publishing:

- **PR builds**: Push to GitHub Packages with `-pr{number}.{run}` suffix
- **main commits**: Push pre-release to GitHub Packages and NuGet.org
- **Tags (v\*.*.*)**: Push stable release to both registries

### Feed URLs

- **NuGet.org**: https://www.nuget.org/packages/Shardis (public)
- **GitHub Packages**: https://nuget.pkg.github.com/veggerby/index.json (authentication required)

### Secrets Required

CI/CD requires these GitHub secrets:
- `NUGET_API_KEY`: NuGet.org API key
- `GH_PACKAGES_TOKEN`: GitHub token with `write:packages` scope

## SBOM (Software Bill of Materials)

Starting with .NET 8, SBOM generation is built-in during pack:

```bash
dotnet pack --configuration Release /p:GenerateSBOM=true
```

The CI/CD pipeline enables this automatically for all releases.

## Package Signing

**Current status**: Packages are **not signed**.

**Future consideration**: We may add package signing using a code signing certificate for additional trust and verification. This is tracked in the backlog but not required for initial public release.

## Support Policy

- **LTS releases (.NET 8)**: Supported while .NET 8 is in official support
- **Current releases (.NET 9)**: Supported while .NET 9 is in official support
- **Preview releases (.NET 10)**: Best-effort support, may have breaking changes

When a .NET version reaches end-of-life, we may drop support in the next **minor** version with advance notice in CHANGELOG.

---

**Questions or feedback?** Open an issue or discussion at https://github.com/veggerby/shardis.
