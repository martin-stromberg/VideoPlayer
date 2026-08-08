# Staging Branch Workflow Documentation

## Overview

This project implements a two-stage CI/CD process with a dedicated `staging` branch to ensure code quality and stability before releases reach the `main` branch.

## Branch Structure

### Main Branch
- **Purpose**: Contains only stable, release-ready code
- **Protection**: No direct commits allowed
- **Trigger**: Creates final release artifacts
- **Artifacts**: Windows and Linux release.zip files

### Staging Branch
- **Purpose**: Integration and quality assurance stage
- **Protection**: No direct commits allowed (PRs only)
- **Trigger**: Runs comprehensive integration tests and quality gates
- **Artifacts**: Pre-release versions with RC suffix

### Feature Branches
- **Purpose**: Individual feature development
- **Target**: All PRs must target `staging` branch
- **Workflow**: Feature → PR to staging → Validation → Merge to staging

## CI/CD Pipeline Stages

### Stage 1: PR Validation (against staging)

**Trigger**: Pull requests targeting `staging` branch

**Workflow**: `.github/workflows/pr-staging-ci.yml`

**Checks performed**:
- ✅ Build validation (ensures code compiles)
- ✅ Unit tests (all test projects)
- ✅ Integration tests (basic integration scenarios)
- ✅ Security scans (dependency vulnerabilities, CodeQL analysis)
- ✅ Code quality checks (formatting, static analysis)
- ✅ Lint checks (compiler warnings as errors)

**Blocking**: All checks must pass before PR can be merged

### Stage 2: Staging Validation

**Trigger**: Push to `staging` branch

**Workflow**: `.github/workflows/staging-ci.yml`

**Checks performed**:
- ✅ Full integration test suite
- ✅ Code coverage analysis (minimum 70% threshold)
- ✅ Comprehensive security analysis
- ✅ Code quality gates
- ✅ Version bumping (semantisch, Conventional Commits)
- ✅ Pre-release artifact creation
- ✅ Automated PR creation to main (Workflow `staging-to-main-promotion.yml`)

**Artifacts created**:
- Pre-release GitHub release (e.g., v1.2.3-RC.4)
- Windows release.zip
- Linux release.zip
- Automated PR to main branch

**Version format**: `v{major}.{minor}.{patch}-RC.{run_number}`

### Stage 3: Final Release

**Trigger**: Push to `main` branch (via automated PR from staging)

**Workflow**: `.github/workflows/main-release.yml`

**Process**:
- ✅ Extract version from staging RC tag (RC-Suffix entfernt)
- ✅ Skip, falls der stabile Tag bereits existiert
- ✅ Build final release artifacts
- ✅ Run final test suite
- ✅ Create GitHub release (erzeugt den Tag)
- ✅ Verify release tag
- ✅ Back-Merge-PR `main` → `staging`

**Artifacts created**:
- Stable GitHub release (e.g., v1.2.3)
- Windows release.zip
- Linux release.zip
- Git tag

## Developer Workflow

### 1. Create Feature Branch
```bash
git checkout -b feature/your-feature-name
```

### 2. Develop and Test
```bash
# Make your changes
git add .
git commit -m "feat: add your feature"
```

### 3. Create PR to Staging
```bash
git push origin feature/your-feature-name
# Create PR in GitHub targeting staging branch
```

### 4. Wait for CI Validation
- PR checks will run automatically
- All checks must pass before merge
- Address any failures and push fixes

### 5. Merge to Staging
- Once all checks pass, merge the PR
- This triggers the staging CI pipeline

### 6. Monitor Staging Pipeline
- Watch the staging workflow run
- Check integration tests and quality gates
- Review pre-release artifacts

### 7. Merge to Main (Automated)
- Once staging validation passes, an automated PR to main is created
- Review the automated PR
- Merge to trigger final release

## Branch Protection Rules

### Required Branch Protection Settings

#### Staging Branch
- ✅ Require pull request before merging
- ✅ Require status checks to pass before merging
  - Build
  - Unit Tests
  - Integration Tests
  - Security Scan
  - Code Quality
  - Lint Check
- ✅ Require branches to be up to date before merging
- ❌ Do not allow bypassing the above settings

#### Main Branch
- ✅ Require pull request before merging
- ✅ Require status checks to pass before merging
  - Build Release
  - Final Tests
- ✅ Require branches to be up to date before merging
- ✅ Restrict who can push to main (maintainers only)
- ❌ Do not allow bypassing the above settings

## Quality Gates

### Code Coverage
- **Minimum threshold**: 70%
- **Measurement**: Line coverage across all test projects
- **Enforcement**: Fails staging pipeline if below threshold

### Security
- **Dependency vulnerabilities**: Zero high/critical vulnerabilities allowed
- **CodeQL analysis**: Must pass without security alerts
- **Static analysis**: Must pass compiler warnings as errors

### Code Quality
- **Formatting**: Code must match dotnet-format standards
- **Build**: Must compile without errors
- **Tests**: All tests must pass (unit + integration)

## Version Management

### Automatic Version Bumping
Die Staging-Pipeline berechnet die naechste Version mit `.github/scripts/compute-version.sh`:
- Basis ist der letzte **stabile** Tag (`vX.Y.Z` ohne Suffix)
- Der Bump ergibt sich aus den Conventional Commits zwischen diesem Tag und `HEAD`:
  - `BREAKING CHANGE` oder `<type>!:` → major
  - `feat:` / `feat(scope):` → minor
  - alles andere → patch
- RC-Suffix mit GitHub-Run-Number
- Beispiel: stabil `v1.2.3` + `feat:` → `v1.3.0-RC.42`

### Release Version
Die Main-Pipeline liest den letzten von `main` aus erreichbaren RC-Tag und entfernt das RC-Suffix:
- Beispiel: `v1.2.4-RC.42` → `v1.2.4`
- Der Release-Tag wird ausschliesslich durch die Release-Action erzeugt (kein zusaetzliches `git tag`), daher keine "tag already exists"-Fehler
- Existiert der stabile Tag bereits, wird der Release-Lauf mit einer Warnung uebersprungen statt zu scheitern

### Back-Merge main → staging
Ein reiner Back-Merge (Tree identisch zu `main`) wird in `staging-ci.yml` im Job `detect-backmerge` erkannt;
Tests, Quality-Gates und Prerelease werden dann uebersprungen, und `staging-to-main-promotion.yml` erstellt
keinen Promotion-PR. Die PR-Checks des Back-Merge-PRs (`pr-staging-ci.yml`) laufen weiterhin, damit die
Branch-Protection auf `staging` erfuellt ist.

Nach einem erfolgreichen Release erstellt `main-release.yml` automatisch einen PR von `main` nach `staging`
(Label `automated-backmerge`). Erst dadurch kennt `staging` den released Stand inkl. Release-Tag, sodass der
naechste Push auf `staging` die Version semantisch weiterzaehlt und nicht nur den RC-Zaehler erhoeht.

### Manual Version Override
If manual version control is needed:
1. Update version in project files manually
2. Add `[skip ci]` to commit message to skip automated versioning
3. Pipeline will use your specified version

## Troubleshooting

### PR Checks Failing
1. Check the specific failing job in the Actions tab
2. Review logs for error details
3. Fix the issue locally
4. Push to your feature branch
5. PR checks will re-run automatically

### Staging Pipeline Failing
1. Identify the failing stage (integration tests, coverage, security)
2. Fix the issue in a new feature branch
3. Create PR to staging
4. After merge, staging pipeline will re-run

### Version Conflicts
If version bumping fails:
1. Check git tags for version history
2. Ensure project files have valid version elements
3. Manual version override may be needed

### Main Release Failing
1. Ensure staging PR was properly validated
2. Check that version format is correct
3. Verify all artifacts were created in staging
4. Manual intervention may be required

## CI Workflow Files

- `.github/scripts/compute-version.sh` - Semantische Versionsberechnung (von staging & main genutzt)
- `.github/workflows/pr-staging-ci.yml` - PR validation for staging
- `.github/workflows/staging-to-main-promotion.yml` - Automatischer PR staging -> main
- `.github/workflows/staging-ci.yml` - Staging branch validation and prerelease
- `.github/workflows/main-release.yml` - Main branch release process
- `.github/workflows/codereviewagent.yml` - Code review automation (updated)
- `.github/workflows/dark-pattern-check.yml` - Dark pattern detection (updated)

## Migration from Old Workflow

### For Existing Features
1. Current PRs against main should be closed
2. Create new PRs targeting staging
3. Follow the new workflow

### For Release Process
1. Current main branch remains stable
2. Next release should go through staging
3. Automated PR will handle main merge

### For Team Processes
1. Update documentation to reference staging branch
2. Update developer onboarding materials
3. Configure branch protection rules
4. Monitor first few staging cycles

## Best Practices

### Feature Development
- Keep feature branches focused and small
- Write tests alongside features
- Run tests locally before pushing
- Review PR check results promptly

### Staging Management
- Monitor staging pipeline regularly
- Review pre-release artifacts before main merge
- Keep staging branch relatively stable
- Address integration issues quickly

### Release Management
- Review automated PR to main carefully
- Ensure release notes are updated
- Test pre-release artifacts when possible
- Plan release timing around staging validation

## Support and Maintenance

### CI Pipeline Issues
- Check workflow logs in GitHub Actions
- Review workflow files for syntax errors
- Ensure GitHub Actions secrets are configured
- Verify .NET SDK version compatibility

### Workflow Updates
- Test workflow changes in feature branches first
- Use staging for CI workflow validation
- Document any workflow changes
- Communicate changes to the team

## Additional Resources

- [GitHub Actions Documentation](https://docs.github.com/en/actions)
- [Branch Protection Rules](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches)
- [Semantic Release](https://github.com/semantic-release/semantic-release)
- [.NET CLI Tools](https://docs.microsoft.com/en-us/dotnet/core/tools/)
