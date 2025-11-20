# Firebase Migration Testing Scenarios

This document outlines test scenarios to validate the Firebase project migration functionality.

## Test Setup

### Prerequisites
1. Two Firebase projects (old and new) with the same database structure
2. Test user with existing data in the old Firebase project
3. Application configured to connect to the old Firebase project initially

### Test Data Requirements
- Test user's `playerUid` from clientsettings.json (e.g., "test-player-123")
- Test user's old Firebase `userId` (from firebase-auth.json)
- At least one modlist saved in the old Firebase project
- At least one compatibility vote in the old Firebase project

## Test Scenario 1: Successful Automatic Migration

**Objective**: Verify that users can automatically migrate to a new Firebase project

### Steps:
1. Start with the app connected to the old Firebase project
2. User has existing data (modlists and votes) in the old project
3. Update DevConfig to point to the new Firebase project
4. Delete the user's `firebase-auth.json` file
5. Launch the application
6. Attempt to access cloud modlists

### Expected Results:
- ✅ Application generates new Firebase authentication
- ✅ Status log shows: "Attempting to migrate cloud modlist ownership for player UID: {playerUid}"
- ✅ Status log shows: "Successfully migrated cloud modlist ownership for player UID: {playerUid}"
- ✅ User can access all their existing modlists
- ✅ User can load modlists successfully
- ✅ User can save new modlists
- ✅ User's compatibility votes are preserved
- ✅ `/ownershipMigration/{sanitizedPlayerUid}/{newFirebaseUid}` is set to `true` in database
- ✅ `/owners/{sanitizedPlayerUid}` is updated to the new Firebase UID

## Test Scenario 2: Multiple Users Migration

**Objective**: Verify that multiple users can migrate independently

### Steps:
1. Set up 3 test users with different playerUids
2. Each user has data in the old Firebase project
3. Switch to new Firebase project
4. Delete all users' firebase-auth.json files
5. Have each user access cloud features sequentially

### Expected Results:
- ✅ Each user successfully migrates independently
- ✅ Each user retains access only to their own data
- ✅ No cross-contamination of data between users
- ✅ All three users can access their respective modlists
- ✅ Database shows three separate ownership entries

## Test Scenario 3: Fresh User (No Migration Needed)

**Objective**: Verify that new users work normally without migration

### Steps:
1. Configure app with new Firebase project
2. Start with a fresh user (never used cloud features before)
3. Access cloud modlists for the first time

### Expected Results:
- ✅ No migration messages appear
- ✅ User can claim ownership normally
- ✅ User can save and load modlists
- ✅ No `/ownershipMigration` entry is created

## Test Scenario 4: Migration Failure Handling

**Objective**: Verify graceful handling when migration fails

### Steps:
1. Modify Firebase rules to deny migration flag writes temporarily
2. Attempt migration as in Scenario 1

### Expected Results:
- ✅ Status log shows: "Failed to set migration flag. Cannot proceed with ownership migration."
- ✅ User gets clear error message about access being denied
- ✅ Application doesn't crash
- ✅ After restoring rules, migration succeeds on retry

## Test Scenario 5: Already Migrated User

**Objective**: Verify that already-migrated users work normally

### Steps:
1. Complete Scenario 1 successfully
2. Close and restart the application
3. Access cloud modlists again

### Expected Results:
- ✅ No migration messages appear (already migrated)
- ✅ User has immediate access to their data
- ✅ All operations work normally
- ✅ No duplicate migration flag entries

## Test Scenario 6: Compatibility Votes Migration

**Objective**: Verify that compatibility votes work after migration

### Steps:
1. User has compatibility votes in old Firebase project
2. Complete migration as in Scenario 1
3. View compatibility votes for mods
4. Submit new compatibility votes

### Expected Results:
- ✅ Old votes are visible
- ✅ New votes can be submitted successfully
- ✅ Vote data is correctly attributed to the new Firebase UID
- ✅ No duplicate votes appear

## Test Scenario 7: Registry Entries Migration

**Objective**: Verify that public registry entries work after migration

### Steps:
1. User has public modlist entries in the registry
2. Complete migration as in Scenario 1
3. View the public registry
4. Attempt to update/delete registry entries

### Expected Results:
- ✅ User's public entries are visible in registry
- ✅ User can update their own registry entries
- ✅ User can delete their own registry entries
- ✅ Updated rules correctly verify ownership via ownershipMigration flag

## Test Scenario 8: Network Interruption During Migration

**Objective**: Verify resilience to network issues

### Steps:
1. Start migration process
2. Disconnect network after migration flag is set but before ownership update
3. Reconnect and retry

### Expected Results:
- ✅ Application handles network errors gracefully
- ✅ Status log shows: "Migration failed with exception: {error}"
- ✅ User can retry by accessing cloud features again
- ✅ Migration completes successfully on retry

## Test Scenario 9: Concurrent Migration Attempts

**Objective**: Verify thread safety of migration logic

### Steps:
1. Simulate multiple concurrent requests to access cloud features
2. All requests should trigger ownership check simultaneously

### Expected Results:
- ✅ Only one migration attempt proceeds
- ✅ No race conditions or duplicate migrations
- ✅ All requests eventually succeed
- ✅ Database shows single consistent ownership state

## Test Scenario 10: Sanitized PlayerUid Handling

**Objective**: Verify that special characters in playerUid are handled correctly

### Steps:
1. Use test users with playerUids containing Firebase-forbidden characters: `.`, `$`, `#`, `[`, `]`, `/`
2. Complete migration for each user

### Expected Results:
- ✅ Characters are sanitized to underscores in Firebase paths
- ✅ Original playerUid is logged correctly in status messages
- ✅ Migration succeeds for all test cases
- ✅ Users can access their data using sanitized paths

## Validation Checklist

After running all scenarios, verify:

- [ ] No data loss occurred
- [ ] No unauthorized access between users
- [ ] Firebase rules are correctly enforced
- [ ] All error cases are handled gracefully
- [ ] Status logging provides useful information
- [ ] Documentation matches actual behavior
- [ ] Performance is acceptable (migration completes in < 5 seconds)
- [ ] Database structure is clean and consistent

## Performance Benchmarks

Expected timing for migration operations:

- Setting ownershipMigration flag: < 500ms
- Updating ownership entry: < 500ms
- Total migration time: < 2 seconds
- First data access after migration: < 1 second

## Security Validation

Verify that:

- [ ] Users cannot claim ownership of other players' data
- [ ] Users cannot set migration flags for other players
- [ ] Unauthorized users cannot read ownership entries
- [ ] Firebase rules prevent malicious manipulation
- [ ] Auth tokens are validated correctly
- [ ] No sensitive data is exposed in logs

## Rollback Plan

If migration issues are discovered:

1. Revert to old Firebase rules (remove ownershipMigration path)
2. Restore old DevConfig settings
3. Users keep their old firebase-auth.json files
4. System operates as before migration feature

## Sign-off

Test scenarios completed by: _______________
Date: _______________
Issues found: _______________
Resolution: _______________
Ready for production: [ ] Yes [ ] No
