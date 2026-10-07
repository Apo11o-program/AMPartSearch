# Changelog

## v1.3.1 - hotkeys no longer eat your typing
The game's keybind system polls raw `Input.GetKeyDown` with **zero** text-field
awareness (verified in IL: `KeybindingsPC/Key.IsKeyDown`). Stock dialogs escape
this only by being modal; this mod's window is not. A Harmony prefix now gates
the game's key dispatcher (`SFS.Input.KeysNode.ProcessInput`) while the search
box has focus. Typing "Nuclear" no longer sends the L to the Launchpad.
Click away / press Escape and every hotkey wakes up again.

## v1.3.0 - the lag hunt ends
An icon requires building one real part and photographing it with the game's
icon camera. v1.2.x did that for up to 40 rows in a single frame on every
keystroke. Now rows appear instantly (text first), icons render in the
background **one per frame**, each render is cached per part id, failures are
blacklisted, and the temporary part is destroyed in a `finally` block the
moment its synchronous camera render completes - so a search can never leave
parts sitting in the world.

## v1.2.1 - the ghost parts exorcised
v1.2.0 kept the icon-render temp parts alive at world origin (the stock picker
deactivates its own; that step was missed). Parts appeared in the build area
when merely searching. Also fixed: adaptive parts render with
`updateAdaptation=true`, parts without variant modules use index -1
(stock's "leave default look" flag).

## v1.2.0 - icons, scrolling, drag & drop
- Rows render in a proper `VerticalLayoutGroup`; window scrolls (wheel + scrollbar).
- Each result row shows the part's real icon via the game's own
  `PartIconCreator.CreatePartIcon_PickGrid`.
- Drag & drop: hold a row, drag past 25 px (the stock threshold), and the game's
  own `HoldGrid.TakePart_PickGrid` picks the part up - snapping, collision and
  undo logic come free because it IS the game's code. Quick click = details.

## v1.1.0 - it appears
Scene hooking fixed: `SceneManager.sceneLoaded` subscriptions inside a Mod
never fire for Build_PC; the working hook is
`ModLoader.Helpers.SceneHelper.OnBuildSceneLoaded`.

## v1.0.0 - first light
Search all loaded parts (name / id / description) including modded parts.
