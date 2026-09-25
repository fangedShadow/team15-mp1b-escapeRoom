# Actual-rubric audit: 35-point recording target

Checked against the supplied assignment rubric. This is an implementation/evidence plan, not a guaranteed grade: signifier quality and discoverability are judged by the grader. Every claim must appear in BOTH the video and Individual Contributions statement. Cap: 33 points for 3CR, 42 for 4CR.

| Story | Points | Evidence to record |
| --- | ---: | --- |
| Key props / grabbing | 1 | Close-hand grabs of match, mirror and real key. The modern Starter Assets rig uses near/far interactors for near grabbing; rubric wording says Direct Interactors. |
| Solid objects | 1 | Keys have Rigidbodies; drop a supply prop and show it tumble and land on the floor. |
| Three accepting locks | 3 | Lit match into lantern, hand mirror into MirrorLock, real key into ExitLockZone. |
| Escape | 3 | Show all three interactions before the final doors open; walk through. Include the team's final win condition in an integrated walkthrough. |
| Grab signifiers | 2 | Match stem, mirror handle, key shape: explain familiar graspable shapes. |
| Escape signifiers | 2 | Darkness warning, blocked passage, reflection clue and locked exit direct attention toward progression. |
| Lock signifiers | 4 | Show local strike/light/carry and reflect/reach/take instructions; explain the familiar key/keyhole relationship. |
| Repetition and variety | 2 | Common rule: bring the appropriate object to a receptive location to remove an obstacle. Variations: fire/state change, reflection and reaching, physical key insertion. Quality is subjective. |
| Eased state changes | 2 | Lantern fade-in, full barrier retreat/fade and door rotation. |
| Reveals | 2 | Lit lantern removes physical shadow obstruction and reveals access to mirror area. |
| Loss timer | 1 | Visible 90-second countdown applies until lantern ignition; show failure at zero. |
| Restart | 1 | Controller-click world-space Restart; show scene and timer reset. |
| Puzzle system | 2 | Strike -> light -> carry -> reveal, and mirror -> front-side reach -> retrieve key. |
| Two puzzle sequences | 1 | First releases access to hand mirror; second releases real exit key. |
| Puzzle discoverability | 3 | Show the two local instruction clues and original mirror riddle. Headset readability must be checked. |
| Eight unique red herrings | 4 | Bottle, can, pills, stool, picture frame, battery, tape roll, radio. Grab/drop all eight. Duplicate cans/bottles/pills do NOT count again. |
| Handmirror | 1 | Grabbable prop + child camera + render texture; assigned material flips X (-1 tiling, +1 offset). Show recognizable asymmetric content in reflection. |
| TOTAL | 35 | 20 critical + 15 sidequest points; no scoreboard or win-celebration bonus claimed. |

## Changes after reading the actual rubric

- Added three unique grabbable/Rigidbody prop models: battery, tape and radio, near the starting-room supplies. Their box colliders fit the imported mesh bounds on Awake. Added URP material copies for these props.
- Added fixed world-space clues near the match and mirror; ordinary-supplies label distinguishes non-key objects.
- MirrorLock now requires lantern ignition AND completed barrier reveal. ExitLockZone requires lantern + mirror lock + taken key.
- Set timer to 90 seconds instead of the 1000-second test value.
- Configured mirror camera to clear its image and render a mono texture in XR. Existing material already provides left-right reversal.
- Earlier fixes cover restart callback, XR UI wiring, hidden-key initialization, two-hand reaching, script filenames and eased barrier transparency.

## Before leaving: short rehearsal

1. Exit Play Mode, let Unity import, and reload Storage_Room from disk when prompted. Do not overwrite the updated file with an older open scene. Confirm no red Console errors.
2. Enter Play Mode. Confirm battery/tape/radio are visible, textured, reachable and rest on the floor. Check new clue readability. Their appearance and placement have not been visually verified here.
3. Complete match -> lantern -> barrier -> mirror -> key -> exit. Show close-hand grabbing, not just distance rays. Try the wrong objects at locks.
4. Start a fresh take for failure: let the 90-second timer expire, click Restart with the controller, confirm the timer and locks reset. Trim waiting time if desired, but retain the visible countdown-to-zero and restart transition.
5. Check the mirror image updates and is horizontally reversed in the headset. If any rehearsal step fails, do not treat the 35-point total as secured.

## Suggested recording order

- Intro: show room, timer, match clue and eight unique supply props (brief grab/drop each).
- Failure/restart take.
- Successful take: strike match, insert into lantern, show light easing and countdown stopping, carry lantern to shadow, show entire reveal.
- Show mirror riddle/instructions, grab mirror, demonstrate reversed reflection, reach through from the front and retrieve real key.
- Insert real key, show doors easing open, walk through. Include final team win if recording integrated game.

## Contribution statement draft (adapt to actual authorship)

My storage-room sequence uses three Key Props: match, hand mirror and real key. Their Locks are LanternLock, MirrorLock and ExitLockZone. The first puzzle requires striking a match, lighting the lantern and carrying it to a shadow barrier to release access to the hand mirror. The second requires bringing the mirror to the reflection area and reaching through from the front to retrieve the real key. Progression requires the lantern/barrier before the mirror lock and the mirror lock before exit unlocking.

Familiar graspable shapes signify picking up the props. Darkness and the blocked passage indicate the first obstacle; the reflection clue identifies the next challenge; the door and keyhole indicate the final obstacle. Local clues describe each puzzle's sequence. The common lock rule is bringing the appropriate object into a receptive location, with variations involving fire/state change, reflection/reaching and physical key insertion. Lantern lighting, barrier retreat and door opening use eased transitions.

The room-scoped countdown runs until lantern ignition. Failure provides a VR restart button that reloads this scene. Eight distinct non-key models can be grabbed and dropped: bottle, can, pills, stool, frame, battery, tape and radio. The hand mirror displays a horizontally reversed camera texture. Add video timestamps and distinguish your work from teammates and imported assets; follow course rules for acknowledging assistance.

## Validation and limitations

Billy scripts compile against installed Unity/XR assemblies with Roslyn (exit 0). Static checks cover scene references, lock dependencies, callback wiring, UI setup and unique added mesh references. No agent-run Unity Play Mode, visual or headset test has been completed. The Starter Assets rig uses near/far interactors, not a component literally named XRDirectInteractor; demonstrate close-hand interaction and confirm any instructor requirement for the exact component. Storage_Room is not in the enabled APK scene list, which does not block the requested Play Mode/headset-link recording. Teammate scenes and final-game integration were not altered.
