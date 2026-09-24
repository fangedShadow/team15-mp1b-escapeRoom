# The Black Tide — Team 15

Open the project root with **Unity 6000.5.6f1**. Choose **Team 15 → Open game start**, or open `Assets/Scenes/DeckScene.unity`, then start Play Mode.

The route is **Deck → Storage Room → Captain's Cabin → Navigation Room**. Finish each room's required puzzles and unlock its exit, then walk through the doorway. Extra Captain ruby collectibles do not block progress. Navigation retains its final challenge and celebration area.

## Controls

| Action | Keyboard / mouse | Meta controllers |
|---|---|---|
| Move / look | WASD / hold right mouse | Left stick / head tracking |
| Turn | Hold right mouse | Right stick snap turn |
| Interact | Aim + E or left click | Aim + trigger |
| Grab / release | E or click / G | Grip |
| Adjust held item | Mouse wheel; Z/X, V/B, C/N rotate | Move the held controller |
| Use held item | Space | Trigger |
| Empty hand reach through mirror | Hold H; wheel adjusts reach; E/click takes key | Reach through with free hand, then grip |
| Navigation map | E/click selects, F launches ship | Right trigger selects, B launches |
| Navigation light | L | Y |
| Enter celebration area | T while standing in the final area | X while standing in the final area |
| Game menu / restart | Esc | Left menu button |

For desktop mirror play, place the mirror in its intended position before reaching with an empty hand. Both VR hands can carry ordinary grab objects between rooms. The simulator is disabled.

The game uses one shared player prefab. Opening a later scene directly is useful for editing, but the complete game's exit and victory checks require the preceding rooms. Use the voyage menu to restart the whole game. The Captain's Reset Voyage control still resets that room only.

## Builds

The four gameplay scenes are already enabled in order. `TransitionCabin` is an old sample and is not in the game route.

- **Team 15 → Build Windows game** produces `Builds/Desktop/BlackTideTeam15.exe`.
- **Team 15 → Build Quest game** produces `Builds/Quest/BlackTideTeam15.apk`.

The integration branch is `codex/combined-game`. Source branches were preserved. Local builds and verification outputs are excluded from Git.

Windows and Quest ARM64/IL2CPP builds succeeded on September 23, 2026. The Windows executable passed a desktop startup check. To copy the Windows build to another PC, copy the whole `Builds/Desktop` folder together; the Quest install file is the APK alone.

## Validation

The full Unity Editor run on September 23, 2026 passed **133 checks with no failures or logged errors**. It verified scene references and XR bindings, completion gates, the four-room puzzle chain, walking through all three exits, one persistent player, both hands retaining and releasing carried objects after travel, the final celebration entrance and supported landing, and a clean restart. The local report is `Verification/combined-playmode.json`; five rendered screenshots cover the four entries and victory area.

These are scripted checks of gameplay actions and real collision/trigger handling. Some actions use their public gameplay methods, and the cannon collision test positions the projectile against a stationary target. A human playthrough is still needed for clue readability, aiming, match striking, comfort and performance. Physical Meta controller/headset behavior and APK installation are not certified by this Editor run.

## Imported versions

| Room / source branch | Source commit |
|---|---|
| `Deck` | `c0037503ac83ed14daa4035c4ffca686d53fedde` |
| `StorageRoom` | `fb0931cdc73269158766026c3c68467e7ef0879d` |
| `Captain_Cabin` | `89d7b0c773326d4d500dfaaf402d5123833ae9d0` |
| `navigation_room` | `a1395cfae1a53c96d63d0f09dafb44531c47bd4d` |

## Source asset note

The Deck branch's terrain layers refer to three textures that are absent from all four source branches: Sand normal (`8a018d52e01f0d244acfde0ce6328045`), Moss color (`288e4b86fa154884aaea9edef69f862b`), and Moss normal (`e054bdb8c196a4c47adacf226339c100`). Available original terrain layers and textures are included. Restoring those three textures from the Deck author's project would complete their original terrain appearance; they are not puzzle dependencies.
