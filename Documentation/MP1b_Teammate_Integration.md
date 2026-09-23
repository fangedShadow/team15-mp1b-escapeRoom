# MP1b — Captain's Cabin integration

Open the repository root in Unity Hub with Unity `6000.5.6f1`. Double-click `Assets/Pirate/Scenes/BlackTideCabin.unity` and press Play. The transition sample is `Assets/MP1B/Scenes/TransitionCabin.unity`. Keyboard/mouse: WASD to move, hold right mouse to look, E or left click to interact, G to drop. Other rooms and the final team challenge still need gameplay integration.

## Project and scope

- Unity project: this repository root; there is no nested `Captain_Cabin` Unity project.
- Shared packages and TextMesh Pro resources are reused from the root project.
- Gameplay scenes: `BlackTideCabin` and `TransitionCabin`.
- Runtime namespace: `BlackTide.MP1B`.
- Components: `CabinRoom`, `CabinPuzzle`, `CabinItem`, `CabinSocket`, `CabinTransition`, `CabinSpawn`, and `CabinDesktop`.

This contribution covers the captain's room, its completion state, and reusable scene transitions. The transition cabin is an independent integration sample. The final team challenge, global victory, and full-team testing remain separate responsibilities.

## Meta headset connected to a PC

Start Quest Link / Air Link in the headset before running the scene. In the Meta Link desktop application, select Meta Link as the active OpenXR runtime, then restart Unity if the runtime changed. Use the Windows build target for a PC executable; an Android build produces a headset APK.

The project enables Oculus Touch, Touch Plus, and Touch Pro interaction profiles for PC and Android. `MP1b > Configure Meta controller input` reapplies the saved configuration when importing these assets into another project.

- Left thumbstick: walk and strafe.
- Right thumbstick left/right: turn in 30-degree steps; release to center before the next turn.
- Look around by turning your head.
- Trigger: point at a puzzle button to press it.
- Grip: hold a nearby item; release to drop it.

If tracking works but controls do not, hold a thumbstick and press F8 on the PC keyboard. Copy the `[Captain VR input]` Console entry: it includes XR mode, device layouts, raw stick values, and action values. Alternatively, use the PiratePlayer component context menu `Log VR controller input on next game frame`. The diagnostic reads gameplay input on a game frame, rather than the separate editor input buffer. Offline injected-controller checks do not certify the physical headset/runtime connection.

Meta Link setup reference: https://developers.meta.com/horizon/documentation/unity/unity-link/

## Connect a door

1. Include the source and destination gameplay scenes in the build's scene list.
2. Place a `CabinSpawn` in the destination at a clear, walkable arrival point. The supplied passage uses `FromCaptain`; the cabin return point uses `FromPassage`.
3. Add/configure the door's `CabinTransition`:

   | Field | Example | Meaning |
   |---|---|---|
   | `targetScene` | `TransitionCabin` | Destination scene name; use unique scene names. |
   | `targetSpawn` | `FromCaptain` | Exact destination `CabinSpawn.spawnId`. |
   | `requireCabinClear` | `true` | The captain's exit must stay gated until its completion condition is met. |

4. Route the door-handle interaction to `CabinTransition.Travel()`. Unlocking the door and using the handle are separate actions.
5. Configure a return door with `targetScene = BlackTideCabin` and the matching cabin arrival ID. Normally the return door does not need `requireCabinClear`.
6. Verify both directions in a build, including missing-scene/missing-spawn handling before sharing the component.

Use `Assets/MP1B/Generated/SceneDoor.prefab` as a clickable door-handle control. It contains a collider, an XR simple interactable, `CabinButton`, and `CabinTransition`. Set its scene and spawn fields; adding `CabinTransition` alone to an arbitrary mesh does not make that mesh clickable. Keep `requireCabinClear` off for unrelated teammates' gates and use their own unlock rules.

## Player and scene lifetime

The small two-scene sample uses additive scene caching: visited scenes remain loaded, and their room state is retained. The player rig is persistent. Integrate one active player rig, camera/audio listener, input owner, and XR interaction setup; a destination scene must not introduce a second player.

Held objects must travel with both hands. On return, solved puzzles, deposited coins, the opened chest, and the unlocked exit must keep their state. Objects already in the world or held by the player must not be recreated simply because they are absent from the currently active scene.

The current caching approach does not establish save/load across application restarts or arbitrary scene unloading. Before replacing it with unloading, add explicit room-state serialization and item restoration. Check memory and inactive-room simulation when expanding from the sample to all team rooms.

## Completion and shared items

Captain's room completion requires all three unique coins in their matching chest sockets, followed by the gold key unlocking the exit. `Cabin Cleared` is a room result, not `You Win` for the whole team game.

The final challenge owner must consume `CabinRoom.Instance.IsCleared` and make global victory depend on it alongside the other members' required locks. `CompletionChanged` is an `Action<bool>` event; the Inspector also exposes `onCabinCleared` and `onCabinReset` UnityEvents. Reset emits false, so a shared win controller must revoke this room's completion when it resets. `IsCleared` becomes true only after the three coin locks and golden-key exit have completed. In this cached-scene design, the original room instance survives while inactive.

Keep bell/quill/seal coin identities distinct from other rooms' keys. Coins remain fixed in their sockets until a confirmed cabin reset. The golden key can be removed after unlocking the exit. Agree on unique item IDs and ownership with the inventory contributor before supporting inventory storage; scene transport alone is not an inventory system.

`Recover Items` returns earned, loose cabin keys to their recovery locations. It must not grant unsolved rewards, duplicate items, or move held/slotted items. Confirmed `Reset Voyage` resets only the captain's room and its owned items; it must not reset teammates' progress.

## Acceptance before handoff

- Keyboard/mouse completes the room with real grab/release and socket interactions. The XR simulator is optional and is not required for ordinary development.
- Quest supports direct grabbing with either hand. Input details must match the finished controls guide.
- Correct and incorrect keys behave consistently; all three lights exist and start off.
- Travel works empty-handed, left-hand-only, right-hand-only, and with both hands occupied, without losing or duplicating items.
- Returning preserves room state; recovery/reset remain correct after a return trip.
- The sample contains no duplicate rigs or missing references; the Android build contains both scenes.

Record which checks passed, the build/version tested, and which headset checks remain pending. Do not describe the sample as a verified full-team integration.
