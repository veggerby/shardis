# First Release Checklist

This document provides a step-by-step guide for publishing the first stable release of Shardis to NuGet.org.

## Pre-Release Verification

### 1. Code Quality
- [ ] All tests pass: `dotnet test Shardis.sln --configuration Release`
- [ ] No compiler warnings: `dotnet build Shardis.sln --configuration Release`
- [ ] Public API approval tests pass (if API changed)
- [ ] Code coverage is acceptable (check codecov.io)

### 2. Documentation
- [ ] README.md is up to date with latest features
- [ ] CHANGELOG.md has all changes documented under `[Unreleased]`
- [ ] All package README files are accurate
- [ ] Documentation examples work and are tested
- [ ] Breaking changes (if any) are clearly documented

### 3. Package Metadata
- [ ] All packages have correct versions
- [ ] All package descriptions are accurate
- [ ] Package tags are relevant and complete
- [ ] Release notes are prepared
- [ ] Icon is included (icon.png)

### 4. Dependencies
- [ ] All dependency versions are compatible
- [ ] No pre-release dependencies (unless intentional)
- [ ] Dependency version ranges are appropriate

## Release Process

### Step 1: Update CHANGELOG

Move items from `[Unreleased]` section to a new version section:

```markdown
## [0.1.0] - 2025-01-15

### Added
- Initial public release
- Core routing with consistent hashing
- Query abstractions and merge primitives
- Migration planning and execution
- Marten and Redis integrations
- ... (all features)
```

Commit the change:
```bash
git add CHANGELOG.md
git commit -m "Update CHANGELOG for v0.1.0 release"
git push origin main
```

### Step 2: Create GitHub Release

1. Go to https://github.com/veggerby/shardis/releases/new
2. Click "Choose a tag" and create a new tag: `v0.1.0`
3. Set release title: `Shardis v0.1.0 - Initial Public Release`
4. Add release description (copy from CHANGELOG)
5. Check "Set as the latest release"
6. Click "Publish release"

Example release notes:

```markdown
# Shardis v0.1.0 - Initial Public Release

First stable release of Shardis, a production-focused .NET sharding framework.

## 🎉 Highlights

- **Deterministic routing** with pluggable strategies (default, consistent hash)
- **Query abstractions** with streaming merge and ordered/unordered execution
- **Migration framework** with planning, execution, and verification
- **Provider support** for Marten, Entity Framework Core (net10+), and in-memory
- **Redis integration** for distributed shard map storage
- **Health monitoring** with configurable probes and resilience strategies
- **Full documentation** including API reference, guides, and examples

## 📦 Packages

All 16 packages are now available on NuGet.org:

- `Shardis` - Core routing and hashing
- `Shardis.All` - Meta-package (recommended)
- `Shardis.Query.*` - Query providers
- `Shardis.Migration.*` - Migration providers
- `Shardis.Redis` - Redis shard map store
- `Shardis.DependencyInjection` - DI extensions
- `Shardis.Logging.*` - Logging adapters

## 🚀 Quick Start

```bash
dotnet add package Shardis.All
```

See [README](https://github.com/veggerby/shardis#readme) for full installation and usage guide.

## 📖 Documentation

- [Getting Started](https://github.com/veggerby/shardis/blob/main/README.md)
- [Packaging & Versioning](https://github.com/veggerby/shardis/blob/main/docs/packaging-and-versioning.md)
- [Migration Guide](https://github.com/veggerby/shardis/blob/main/docs/MIGRATION.md)
- [API Documentation](https://github.com/veggerby/shardis/blob/main/docs/api.md)

## 🙏 Contributors

Special thanks to all contributors and early adopters!

## 📋 Full Changelog

See [CHANGELOG.md](https://github.com/veggerby/shardis/blob/main/CHANGELOG.md) for complete details.
```

### Step 3: Verify CI/CD Pipeline

The GitHub Actions workflow will automatically:
1. Build all packages
2. Run tests
3. Generate SBOM
4. Validate packages
5. Publish to GitHub Packages
6. Publish to NuGet.org

Monitor the workflow at: https://github.com/veggerby/shardis/actions

### Step 4: Verify NuGet Publication

After the workflow completes (usually 5-10 minutes), verify packages are published:

1. Check NuGet.org: https://www.nuget.org/packages/Shardis
2. Verify all 16 packages are published with correct version
3. Check package metadata (icon, readme, release notes)
4. Test installation: `dotnet new console -n test && cd test && dotnet add package Shardis`

### Step 5: Announce Release

Once packages are verified on NuGet.org:

1. **GitHub Discussions**: Create a discussion in "Announcements"
2. **Twitter/Social**: Announce the release with key features
3. **Dev Communities**: Share on relevant forums (Reddit r/dotnet, etc.)
4. **Documentation site**: Update if applicable

Example announcement:

```
🎉 Shardis v0.1.0 is now available on NuGet!

A production-focused .NET sharding framework with:
✅ Deterministic routing & consistent hashing
✅ Query abstractions with merge primitives
✅ Migration framework with verification
✅ Marten & EF Core support
✅ Redis integration
✅ Full documentation

Get started: dotnet add package Shardis.All

📖 Docs: https://github.com/veggerby/shardis
🚀 NuGet: https://nuget.org/packages/Shardis

#dotnet #csharp #sharding #opensource
```

## Post-Release Tasks

### Immediate (within 24 hours)

- [ ] Monitor GitHub Issues for bug reports
- [ ] Watch NuGet download stats
- [ ] Respond to community feedback
- [ ] Check for any package metadata issues on NuGet.org
- [ ] Update GitHub README badges (if they weren't showing before release)

### Short-term (within 1 week)

- [ ] Write blog post or detailed announcement
- [ ] Create tutorial videos or demos (optional)
- [ ] Update documentation based on user feedback
- [ ] Triage any reported issues
- [ ] Plan next release (collect feature requests)

### Ongoing

- [ ] Maintain CHANGELOG.md with all changes
- [ ] Keep documentation up to date
- [ ] Release patch versions for critical bugs
- [ ] Follow semantic versioning strictly
- [ ] Communicate breaking changes clearly

## Emergency Rollback

If a critical issue is discovered after release:

### Option 1: Unlist package on NuGet.org

1. Go to package page on NuGet.org
2. Click "Manage Package"
3. Click "Unlist" (package stays installed for existing users but won't appear in search)
4. Fix the issue
5. Release a patch version (e.g., v0.1.1)

### Option 2: Release hotfix immediately

1. Create a hotfix branch from the tag: `git checkout -b hotfix/0.1.1 v0.1.0`
2. Fix the critical issue
3. Update CHANGELOG.md
4. Tag and release: `git tag v0.1.1 && git push --tags`
5. CI/CD will automatically publish the hotfix
6. Merge hotfix back to main

## Troubleshooting

### Build fails in CI

- Check build logs in GitHub Actions
- Verify all dependencies are compatible
- Ensure no test failures
- Check for .NET SDK version compatibility

### Package upload fails

- Verify NuGet API key is valid
- Check package size limits (10MB for free NuGet.org)
- Ensure unique version number (can't republish same version)
- Verify no malformed metadata

### Package doesn't appear on NuGet.org

- Allow 15-30 minutes for indexing
- Check if package was published to GitHub Packages only
- Verify publish step ran successfully in CI logs
- Check NuGet.org account for any package moderation holds

## Version Planning

### Patch (0.1.x)
- Bug fixes
- Documentation improvements
- Performance improvements (no API changes)

### Minor (0.x.0)
- New features (backward compatible)
- New packages or integrations
- Deprecations (with grace period)

### Major (x.0.0)
- Breaking API changes
- Removal of deprecated features
- Target framework changes

---

## Contact

Questions about the release process? Open an issue or discussion on GitHub.

**Repository**: https://github.com/veggerby/shardis
**Discussions**: https://github.com/veggerby/shardis/discussions
