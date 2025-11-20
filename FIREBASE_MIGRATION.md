# Firebase Project Migration Guide

This document explains how to migrate users from an old Firebase project to a new one while preserving access to their cloud modlists and compatibility votes.

## Overview

When migrating to a new Firebase project (with a new database URL and API key), users need to maintain access to their existing data. This is achieved through an automatic migration system that uses Firebase security rules and client-side logic.

## How It Works

### The Problem
- Each Firebase anonymous user has a unique `userId` (Firebase UID)
- User data is keyed by the Vintage Story `playerUid` from `clientsettings.json`
- The `/owners/{sanitizedPlayerUid}` path maps which Firebase UID owns which VS player's data
- When switching Firebase projects, users get a new Firebase UID
- The old ownership mapping prevents access with the new UID

### The Solution
The migration system uses a two-step approach:

1. **ownershipMigration Flag**: A temporary flag at `/ownershipMigration/{sanitizedPlayerUid}/{newFirebaseUid}` grants the new UID access to existing data
2. **Ownership Update**: Once granted access, the `/owners/{sanitizedPlayerUid}` entry is updated to the new Firebase UID

## Migration Process

### For Administrators

1. **Set up the new Firebase project**:
   - Create a new Firebase Realtime Database
   - Update `DevConfig.cs` with the new database URL and API key
   - Deploy the updated Firebase security rules from `firebase.rules.json`

2. **Migrate existing data**:
   - Export data from old Firebase project
   - Import data into new Firebase project
   - This includes: `owners`, `users`, `registry`, `registryOwners`, `compatVotes`, and `adminRegistry` paths

3. **Deploy the updated application**:
   - The new version includes automatic migration support
   - Users will seamlessly migrate when they next use cloud features

### For Users

The migration is **completely automatic**. Users simply:

1. Delete their existing `firebase-auth.json` file (or let it expire naturally)
2. Launch the application
3. Use cloud modlists or compatibility voting features
4. The app automatically:
   - Detects the ownership mismatch
   - Sets the migration flag
   - Updates ownership to the new Firebase UID
   - Displays a success message in the status log

**No manual intervention required!**

## Firebase Security Rules

The updated rules in `firebase.rules.json` include:

### ownershipMigration Path
```json
"ownershipMigration": {
  "$playerUid": {
    "$newFirebaseUid": {
      ".read": "auth != null && auth.uid == $newFirebaseUid",
      ".write": "auth != null && auth.uid == $newFirebaseUid && (!data.exists() || data.val() == true) && (!newData.exists() || newData.val() == true)"
    }
  }
}
```

### Updated Access Rules
All data access rules now check **both** conditions:
- `root.child('owners').child($playerUid).val() == auth.uid` (original owner)
- `root.child('ownershipMigration').child($playerUid).child(auth.uid).val() == true` (migration flag)

This allows seamless access during and after migration.

## Testing Migration

To test the migration process:

1. Set up a test Firebase project with sample data
2. Note a test user's `playerUid` and Firebase `userId`
3. Switch to a new Firebase project (update DevConfig)
4. Delete the test user's `firebase-auth.json`
5. Launch the app and access cloud modlists
6. Verify:
   - Migration message appears in status log
   - User can access their existing modlists
   - Ownership is updated in the new database

## Security Considerations

- The migration flag can only be set by the authenticated user
- The flag only grants access to data already owned by the user's VS playerUid
- Once ownership is updated, the old Firebase UID loses access
- The migration is permanent and cannot be reversed without manual intervention

## Troubleshooting

### User Cannot Access Data After Migration

1. **Check Firebase Rules**: Ensure the updated rules are deployed
2. **Check Data Import**: Verify all data was imported correctly, especially the `/owners` path
3. **Check Authentication**: Ensure the user has a valid Firebase auth token
4. **Check Logs**: Look for error messages in the app's status log

### Migration Fails Silently

1. **Check Firebase Permissions**: Ensure anonymous auth is enabled
2. **Check API Key**: Verify the new API key has correct permissions
3. **Check Network**: Ensure the user has internet access
4. **Check Database URL**: Verify the correct database URL is configured

## Manual Migration (Fallback)

If automatic migration fails, administrators can manually update ownership:

1. Get the user's `sanitizedPlayerUid` from the database
2. Get the user's new Firebase `userId` (from their firebase-auth.json or adminRegistry)
3. Update `/owners/{sanitizedPlayerUid}` to the new `userId`
4. Optionally set `/ownershipMigration/{sanitizedPlayerUid}/{newUserId}` to `true`

## Code Changes

### Key Files Modified

1. **firebase.rules.json**: Added ownershipMigration path and updated all access rules
2. **FirebaseModlistStore.cs**: Added `TryMigrateOwnershipAsync()` method to handle migration

### Migration Logic Flow

```
EnsureOwnershipAsync()
  ├─ Check if ownership exists
  ├─ If owner != current user
  │    └─ TryMigrateOwnershipAsync()
  │         ├─ Set ownershipMigration flag
  │         ├─ Update owners entry
  │         └─ Log success message
  └─ Claim ownership if new user
```

## Future Enhancements

Possible improvements to the migration system:

1. **Migration UI**: Show users a migration status dialog
2. **Batch Migration**: Tool to migrate multiple users at once
3. **Migration Report**: Admin dashboard showing migration statistics
4. **Rollback Support**: Ability to revert to old Firebase project
5. **Data Validation**: Verify data integrity after migration

## Contact

For questions or issues with Firebase migration, please contact the development team or open an issue on the GitHub repository.
