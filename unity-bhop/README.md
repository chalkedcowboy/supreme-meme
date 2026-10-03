# Unity Bhop Controller

Source-style first-person movement for Unity: bunny hopping, air strafing, surf-ramp sliding, plus an X-ray vision ability.
One script, `BhopController.cs`. Works with the legacy Input Manager or the new Input System.

## Setup

1. Copy `BhopController.cs` into `Assets/`.
2. Create an empty GameObject `Player`, add **Character Controller** (height 2, radius 0.5) and **BhopController**.
3. Make the Main Camera a child of `Player` at local position `(0, 0.7, 0)`.
4. Add a floor collider and press Play.

## Controls

| Input | Action |
|---|---|
| WASD | Move |
| Mouse | Look |
| Space (hold) | Hop on every landing (`autoBhop`) |
| Esc / click | Unlock / lock cursor |

To gain speed: jump, then in the air hold **A** while turning the mouse left (or **D** + right). Keep holding Space so you re-jump on the landing frame and skip ground friction.

## Tuning

| Field | Effect |
|---|---|
| `airAccel` | Higher = faster speed gain while strafing (surf servers use ~10x) |
| `airSpeedCap` | Per-tick air speed cap; lower makes strafing more about mouse turning |
| `friction` | Ground slowdown; only applies when you don't jump on landing |
| `autoBhop` | Off = press Space per hop (a press in mid-air is buffered until landing) |

Defaults are Source engine values scaled to meters. Read `Velocity`, `HorizontalSpeed`, or `IsGrounded` from other scripts for HUDs or effects.

## X-Ray Vision

Press **X** to see enemies through walls for 5 s, then a 3 s cooldown. Only the parts hidden behind geometry glow; visible parts render normally.

1. Copy `XRay.shader`, `XRayVision.cs`, `XRayTarget.cs` into `Assets/`.
2. Add **XRayVision** to `Player` and assign `XRay.shader` to its `xrayShader` field (required for builds).
3. Add **XRayTarget** to each enemy or object root; set its `color`. All child meshes (static and skinned) are revealed.

| Field | Effect |
|---|---|
| `duration` | Seconds active; 0 = until toggled off |
| `cooldown` | Seconds before reuse |
| `range` | Reveal distance; 0 = unlimited |
| Shader `_ZTest` | `Greater` = only behind walls; `Always` = whole silhouette |

Supports the Built-in pipeline and URP (not HDRP). Read `Active`, `TimeLeft`, `CooldownLeft` for custom UI.
