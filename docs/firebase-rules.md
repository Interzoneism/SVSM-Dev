# Firebase Realtime Database Rules

Anonymous authentication is required for every request. The first anonymous account that writes to `/users/{playerUid}` claims that Vintage Story UID; future reads and writes for the same `playerUid` must come from the same anonymous account. Modlist data lives directly under the player node as exactly five slots named `slot1` through `slot5`, each containing an object with a mandatory `content` child that holds the exported modlist JSON and an optional `registryId` that links the slot to the public registry entry.

Every successful save mirrors the content into the global `/registry/{entryId}` node so that all players can browse public modlists. Ownership of each registry entry is tracked privately in `/registryOwners/{entryId}` to ensure only the uploading account can mutate or delete its record. Deleting a slot removes both the private slot and its associated registry entry (if one exists).

A representative set of rules is shown below. Adjust paths to match your deployment, but keep the same ownership semantics, slot validation, and registry mirroring:

```json
{
  "rules": {
    ".read": false,
    ".write": false,

    "owners": {
      "$playerUid": {
        ".read": "auth != null && data.val() == auth.uid",
        ".write": "auth != null && (!data.exists() || data.val() == auth.uid) && newData.val() == auth.uid"
      }
    },

    "users": {
      "$playerUid": {
        ".read": "auth != null && root.child('owners').child($playerUid).val() == auth.uid",
        "$slot": {
          ".write": "auth != null && ($slot == 'slot1' || $slot == 'slot2' || $slot == 'slot3' || $slot == 'slot4' || $slot == 'slot5') && root.child('owners').child($playerUid).val() == auth.uid",
          ".validate": "!newData.exists() || (newData.hasChildren(['content']) && newData.child('content').isObject())"
        }
      }
    },

    "registry": {
      ".read": true,
      "$entryId": {
        ".write": "auth != null && newData.parent().parent().child('registryOwners').child($entryId).val() == auth.uid",
        ".validate": "!newData.exists() || !newData.child('owner').exists()"
      }
    },

    "registryOwners": {
      "$entryId": {
        ".read": false,
        ".write": "auth != null && (!data.exists() || data.val() == auth.uid) && newData.val() == auth.uid"
      }
    }
  }
}
```

These rules enforce one-to-one ownership between Firebase anonymous accounts and Vintage Story player UIDs while guaranteeing that only the owner can mutate their private modlist slots. They also ensure that the public registry always reflects the latest slot content and remains discoverable by every player.

## Client configuration

The desktop client authenticates with Firebase using an anonymous account. Provide the Realtime Database API key through one of the following mechanisms before attempting any cloud operation:

- Set the `SIMPLE_VS_MANAGER_FIREBASE_API_KEY` environment variable.
- Or create a `firebase-api-key.txt` file inside the `Simple VS Manager` folder in your Documents directory and place the API key on the first line.

When either option is configured, the client persists the Firebase refresh token in the same `Simple VS Manager` folder so the anonymous identity is reused across sessions.
