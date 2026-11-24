# Changelog

## [0.1.0] - 2026-11-24
- Initial version: type-safe `EventBus` (Subscribe / SubscribeOnce / Unsubscribe / UnsubscribeAll /
  Publish) with `Subscription` tokens, owner-bound subscriptions (`this.Subscribe`) that end when the
  owner is destroyed, copy-on-write dispatch (safe changes during publish), exception isolation,
  reset on play (domain reload off).
- ScriptableObject event channels (Void, Int, Float, Bool, String, Vector3, GameObject) and matching
  listener components with UnityEvent responses; generic bases for custom payloads.
- Editor: channel inspector with listener count and Raise button, Event Monitor window.
- 19 PlayMode tests.
