# Branch Protection Rules Setup Guide

This guide provides step-by-step instructions for configuring branch protection rules to support the staging branch workflow.

## Overview

Branch protection rules are essential for enforcing the new CI/CD workflow and preventing direct commits to protected branches. This guide covers the setup for both `staging` and `main` branches.

## Prerequisites

- Repository admin access
- GitHub repository with staging branch created
- CI workflows deployed and tested

## Step 1: Create Staging Branch

If the staging branch doesn't exist yet, create it:

```bash
git checkout main
git pull origin main
git checkout -b staging
git push origin staging
```

## Step 2: Configure Staging Branch Protection

### Navigate to Branch Protection Settings

1. Go to your repository on GitHub
2. Click **Settings** tab
3. Click **Branches** in the left sidebar
4. Click **Add branch protection rule**

### Configure Staging Branch Rule

**Branch name pattern**: `staging`

#### Basic Settings

✅ **Require a pull request before merging**
- Require approvals: `1` (optional, based on team preference)
- Dismiss stale PR approvals when new commits are pushed: ✅ (recommended)
- Require review from CODEOWNERS: ❌ (unless you have CODEOWNERS file)
- Require approvals from: (select appropriate reviewers)
- Allow auto-merge: ❌ (recommended for safety)

✅ **Require status checks to pass before merging**
- Require branches to be up to date before merging: ✅ (critical)
- **Required status checks**:
  - `Build` (from pr-staging-ci.yml)
  - `Unit Tests` (from pr-staging-ci.yml)
  - `Integration Tests` (from pr-staging-ci.yml)
  - `Security Scan` (from pr-staging-ci.yml)
  - `Code Quality` (from pr-staging-ci.yml)
  - `Lint Check` (from pr-staging-ci.yml)

#### Additional Settings

❌ **Do not allow bypassing the above settings** (uncheck this box)
✅ **Require signed commits** (optional, based on security requirements)
❌ **Require linear history** (optional, but recommended for cleaner history)
✅ **Allow force pushes** (optional, may be needed for cleanup)
❌ **Allow deletions** (critical - prevent branch deletion)

### Save the Rule

Click **Create** or **Save changes** to apply the staging branch protection.

## Step 3: Configure Main Branch Protection

### Add Main Branch Rule

1. Still in **Branches** settings
2. Click **Add branch protection rule**

**Branch name pattern**: `main`

#### Basic Settings

✅ **Require a pull request before merging**
- Require approvals: `1` (recommended)
- Dismiss stale PR approvals when new commits are pushed: ✅ (recommended)
- Require review from CODEOWNERS: ❌ (unless you have CODEOWNERS file)
- Require approvals from: (select maintainers/leads)
- Allow auto-merge: ❌ (recommended for safety)

✅ **Require status checks to pass before merging**
- Require branches to be up to date before merging: ✅ (critical)
- **Required status checks**:
  - `Build Release` (from main-release.yml)
  - `Final Tests` (from main-release.yml)

#### Additional Settings

❌ **Do not allow bypassing the above settings** (uncheck this box)
✅ **Require signed commits** (optional, based on security requirements)
❌ **Require linear history** (optional, but recommended)
❌ **Allow force pushes** (critical - prevent history rewrite)
❌ **Allow deletions** (critical - prevent branch deletion)

#### Restrict Who Can Push

1. In the **Restrict who can push to this branch** section
2. Click **Add people/teams**
3. Add only maintainers/leads who should have direct push access
4. ✅ **Restrict who can push to matching branches**

### Save the Rule

Click **Create** or **Save changes** to apply the main branch protection.

## Step 4: Verify Workflow Status Checks

After setting up branch protection, verify that the workflow status checks are available:

1. Create a test PR to staging
2. Check the PR checks section
3. Ensure all required status checks appear:
   - Build
   - Unit Tests
   - Integration Tests
   - Security Scan
   - Code Quality
   - Lint Check

If any checks are missing:
1. Check that the workflow files are properly configured
2. Ensure workflows have run at least once
3. Verify workflow names match the required status checks

## Step 5: Test the Protection Rules

### Test Staging Protection

1. Create a feature branch: `git checkout -b test-protection`
2. Make a small change
3. Try to push directly to staging (should fail):
   ```bash
   git push origin staging
   ```
4. Create a PR to staging instead
5. Verify that you cannot merge without all checks passing

### Test Main Protection

1. Try to push directly to main (should fail):
   ```bash
   git push origin main
   ```
2. Try to create a PR to main
3. Verify that required checks are enforced
4. Test that only authorized users can push (if restricted)

## Step 6: Configure Additional Settings (Optional)

### CODEOWNERS File

Create a `.github/CODEOWNERS` file to specify required reviewers:

```
# Require team review for staging
* @your-team-name

# Require specific maintainers for main
/ @maintainer1 @maintainer2
```

### Required Reviewers

In branch protection settings, you can specify:
- Specific users who must review
- Teams that must review
- Number of required approvals

### Lock Branches

For additional security, you can temporarily lock branches:
- Useful during critical periods
- Prevents all pushes and PRs
- Can be done through GitHub UI or API

## Step 7: Document and Communicate

### Update Team Documentation

1. Update developer onboarding materials
2. Document the new branch protection rules
3. Share this guide with the team
4. Conduct a team walkthrough if needed

### Create Runbook

Create a quick reference guide for common scenarios:
- How to create a PR to staging
- What to do when checks fail
- Emergency procedures (if any)

## Troubleshooting

### Status Checks Not Appearing

**Problem**: Required status checks don't show up in PR checks

**Solutions**:
1. Ensure workflows have run successfully at least once
2. Check workflow syntax for errors
3. Verify workflow names match required checks exactly
4. Check GitHub Actions logs for workflow failures

### Can't Merge Despite Passing Checks

**Problem**: All checks pass but merge button is disabled

**Solutions**:
1. Verify branch is up to date with target branch
2. Check if additional approvals are required
3. Ensure branch protection rules are correctly configured
4. Check for any conflicting rules

### Force Push Protection

**Problem**: Need to force push but protection prevents it

**Solutions**:
1. Temporarily disable force push protection (not recommended)
2. Create a new branch instead
3. Contact repository admin for assistance
4. Use revert instead of force push when possible

### Emergency Access

**Problem**: Urgent fix needed but protection blocks it

**Solutions**:
1. Temporarily disable specific protection rules
2. Use admin override (if available)
3. Create emergency procedure with approval process
4. Plan ahead: add emergency contacts to branch protection

## Maintenance

### Regular Review

Review branch protection settings periodically:
- Update required reviewers as team changes
- Adjust status checks as workflows evolve
- Review and update CODEOWNERS file
- Audit protection rule effectiveness

### Audit Trail

Monitor branch protection effectiveness:
- Check GitHub audit logs for protection bypasses
- Review failed merge attempts
- Analyze protection rule violations
- Adjust rules based on team feedback

## Security Considerations

### Best Practices

1. **Never disable protection permanently** for production branches
2. **Use admin privileges sparingly** and document reasons
3. **Regularly review** who has push access to main
4. **Enable audit logging** for security monitoring
5. **Require 2FA** for repository admins

### Compliance

If you have compliance requirements:
- Document all protection rule changes
- Maintain audit logs of protection bypasses
- Regular security reviews of branch protection
- Incident response procedures for protection violations

## API Configuration

For automated setup, you can use GitHub API:

```bash
# Set staging branch protection via API
curl -X PUT \
  -H "Authorization: token $GITHUB_TOKEN" \
  -H "Accept: application/vnd.github.v3+json" \
  https://api.github.com/repos/OWNER/REPO/branches/staging/protection \
  -d '{
    "required_status_checks": {
      "strict": true,
      "contexts": ["Build", "Unit Tests", "Integration Tests", "Security Scan", "Code Quality", "Lint Check"]
    },
    "enforce_admins": true,
    "required_pull_request_reviews": {
      "dismiss_stale_reviews": true,
      "require_code_owner_reviews": false
    },
    "restrictions": null,
    "allow_force_pushes": false,
    "allow_deletions": false
  }'
```

## Summary

With these branch protection rules in place:
- ✅ All code changes go through proper review
- ✅ CI checks must pass before merging
- ✅ Main branch remains stable and release-ready
- ✅ Staging branch serves as proper integration point
- ✅ Team workflow is enforced and consistent
- ✅ Security and quality gates are maintained

## Next Steps

1. Configure the branch protection rules as described
2. Test the setup with a sample PR
3. Train team members on the new workflow
4. Monitor the first few staging cycles
5. Adjust rules based on team feedback and experience
