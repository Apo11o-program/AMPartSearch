# IL archaeology notes

Everything below was extracted from `Assembly-CSharp.dll` (SFS 1.5.10) with
`monodis` while building this mod. None of it is documented anywhere official.
It is published here so the next modder does not have to read 12 MB of IL at 3 am.

## The only working scene hook

```
ModLoader.Helpers.SceneHelper.OnBuildSceneLoaded += () => { ... };
```

Subscribing to `SceneManager.sceneLoaded` from inside a `Mod` subclass never
fires for `Build_PC`. Use the helper's `On{Home,Hub,Build,World}Scene{Loaded,Unloaded}`
optional delegates.

## Keyboard hotkeys have no text-field guard

`SFS.Input.KeybindingsPC/Key.IsKeyDown()` is literally:

```
Input.GetKeyDown(key) && (ctrl ? Input.GetKey(306) : true)
```

`KeysNode.ProcessInput` walks its dictionaries and invokes actions - nothing
checks whether a `TMP_InputField` is focused. Stock menus dodge this by being
modal (screens are swapped); non-modal windows get raw keystrokes leaked to
hotkeys (`,`/`.` = timewarp, `space` = ignition, `L` = launchpad).
Fix used here: Harmony prefix on `KeysNode.ProcessInput` returning false while
any focused `TMP_InputField` exists.

## Drag & drop = the stock pick grid, stolen wholesale

`PickGridUI.CreateParts` per part (`PickGridIcon` prefab = RawImage + Button):

- icon: `PartsLoader.CreatePart(variant, true)` -> park under
  `createdPartsHolder` -> `SetActive(true)` ->
  `PartIconCreator.main.CreatePartIcon_PickGrid(part, out size)` -> assign
  `RawImage.texture` -> **`SetActive(false)`** (miss this and the part
  photobombs the build area) -> stock then keeps the part for reuse; this mod
  destroys it instead (the camera render is synchronous).
- drag: `button.onDown` records `position.pixel`; `onHold` waits for
  |start - pixel| > 25 px, then calls
  `BuildManager.main.holdGrid.TakePart_PickGrid(variant, position.World(0f))`.
  HoldGrid then owns the part: snapping, collision, undo - all free.

## PartIconCreator

`PartIconCreator.main` (MonoBehaviour, camera child). All methods are instance
methods returning `RenderTexture`; the camera `Render()` inside is synchronous,
so the temp part can be destroyed immediately afterwards.

| method | signature |
|---|---|
| `CreatePartIcon_PickGrid` | `(Part part, out Vector2 size)` |
| `CreatePartIcon_TechTree` | `(VariantRef[] parts, int height, bool center, float maxAspectRatio)` |
| `CreatePartIcon_Staging` | `(PartSave a, int texWidth)` |
| `CreatePartIcon_Sharing` | `(Blueprint b, int width, int height)` |

`VariantRef` is `new VariantRef(Part part, int variantIndex_A, int variantIndex_B)`;
index `-1` means "do not apply a variant" (use for parts without a Variants module,
otherwise `0,0` applies the first variant).

## UITools window recipe (learned from PartEditor)

```
holder = Builder.CreateHolder(Builder.SceneToAttach.CurrentScene, name);
window = UIToolsBuilder.CreateClosableWindow(holder.transform, Builder.GetRandomID(),
          w, h, x, y, true, true, 0.95f, "Title");
window.CreateLayoutGroup(ModType.Vertical, TextAnchor.UpperCenter, 4f);
window.EnableScrolling(ModType.Vertical);   // wheel + scrollbar
```

Rebuild pattern: destroy `window.ChildrenHolder` children you don't need, then
re-add. `ModGUI.GUIElement` has `op_Implicit -> Transform` and a public
`rectTransform` field (the widgets are not Components themselves).

## Misc traps

- `SFS.Base.partsLoader.parts` = `Dictionary<string, Part>` - every loaded part,
  mods included. `displayName.Field` -> `string` via `op_Implicit`.
- mcs: `using SFS.Base;` is CS0138 (it is a class, not a namespace); pass the
  full reference set incl. `Unity.TextMeshPro.dll` or widget code won't compile.
- `ModGUI.Button._button` (private) is a `ButtonPC` - it inherits the
  `OptionalDelegate<T>` fields `onDown / onHold / onUp / onClick / onRightClick /
  onLongClick / onScroll` from `SFS.UI.Button`; subscribe with `+=`.
