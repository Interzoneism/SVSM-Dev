# Firebase Realtime Database Rules

Anonymous authentication is now required for every request. The first anonymous account that writes to `/users/{playerUid}` claims that Vintage Story UID; future reads and writes for the same `playerUid` must come from the same anonymous account. Modlist data lives directly under the player node as exactly five slots named `slot1` through `slot5`, each containing an object with a single `content` child that holds the exported modlist JSON. The `content` object may also include helper fields such as `uploaderId` and `uploaderName`.

The registry remains publicly readable at `/registry`, but the client never writes to it. A Cloud Function (or other trusted process) should mirror data from `/users` into `/registry` for discovery if desired.

A representative set of rules is shown below. Adjust paths to match your deployment, but keep the same ownership semantics and slot validation:

```json
{
  "rules": {
    ".read": false,
    ".write": false,
    "users": {
      "$playerUid": {
        ".read": "auth != null && root.child('owners').child($playerUid).val() === auth.uid",
        ".write": "auth != null && (!root.child('owners').child($playerUid).exists() || root.child('owners').child($playerUid).val() === auth.uid)",
        "$slot": {
          ".validate": "$slot.matches(/^slot[1-5]$/) && newData.hasChildren(['content'])"
        }
      }
    },
    "owners": {
      "$playerUid": {
        ".read": "auth != null && data.val() === auth.uid",
        ".write": "auth != null && (!data.exists() || data.val() === auth.uid)"
      }
    },
    "registry": {
      ".read": true,
      ".write": false
    }
  }
}
```

These rules enforce one-to-one ownership between Firebase anonymous accounts and Vintage Story player UIDs while guaranteeing that only the owner can mutate their private modlist slots.

## Client configuration

The desktop client now authenticates with Firebase using an anonymous account. Provide the Realtime Database API key through one of the following mechanisms before attempting any cloud operation:

- Set the `SIMPLE_VS_MANAGER_FIREBASE_API_KEY` environment variable.
- Or create a `firebase-api-key.txt` file inside the `Simple VS Manager` folder in your Documents directory and place the API key on the first line.

When either option is configured, the client persists the Firebase refresh token in the same `Simple VS Manager` folder so the anonymous identity is reused across sessions.
