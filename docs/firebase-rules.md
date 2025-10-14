# Firebase Realtime Database Rules

The client now stores player modlists under their Vintage Story `playeruid`. Each Firebase anonymous account is bound to a single `playeruid` via the `/uidBindings` node. Update the Realtime Database rules accordingly:

```json
{
  "rules": {
    ".read": false,
    ".write": false,
    "users": {
      "$playerUid": {
        ".read": "auth != null && root.child('uidBindings').child($playerUid).val() === auth.uid",
        ".write": "auth != null && root.child('uidBindings').child($playerUid).val() === auth.uid"
      }
    },
    "registry": {
      "$playerUid": {
        ".read": "auth != null",
        "$slot": {
          ".write": "auth != null && root.child('uidBindings').child($playerUid).val() === auth.uid"
        }
      }
    },
    "uidBindings": {
      "$playerUid": {
        ".read": "auth != null && root.child('uidBindings').child($playerUid).val() === auth.uid",
        ".write": "auth != null && (!root.child('uidBindings').child($playerUid).exists() || root.child('uidBindings').child($playerUid).val() === auth.uid)"
      }
    }
  }
}
```

These rules ensure that only the Firebase identity originally paired with a Vintage Story `playeruid` can read or modify that player's modlists, while still allowing authenticated users to discover public registry entries.
