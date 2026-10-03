# Unity Bhop Controller

Source-style first-person movement for Unity: bunny hopping, air strafing, and surf-ramp sliding.
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
