# SevenDays – a 7 Days to Die–style survival game in C#

A turn-based survival game in Godot 4 (C#, .NET 8) with the core 7 Days to Die loop:

- **Scavenge** – harvest trees and rocks, loot crates in ruined buildings, drink from water.
- **Craft** – clubs, spears, stone axes, bandages, wood/stone walls and spike traps.
- **Survive** – food, water and health; zombies roam at night (Walkers and faster, tougher Ferals).
- **Fortify** – place walls and spikes. Zombies path toward you and chew through anything in the way.
- **Blood moon** – every 7th night (22:00 → 04:00) a horde of Ferals hunts you down, growing each cycle.

## Run

Install [Godot 4.3+ .NET edition](https://godotengine.org/download), then import
`src/SevenDays.Godot/project.godot` and press F5 (or `godot --path src/SevenDays.Godot`).
Pass `-- --seed=7` after the engine args for a fixed world.

Run the tests with `dotnet test`.

## Controls

| Key | Action |
| --- | --- |
| WASD / arrows | Move; bump to harvest, loot, drink or attack |
| C | Craft | 
| B | Build (pick item, then direction) |
| X | Dismantle one of your blocks |
| U | Eat / drink / bandage |
| `.` / Space | Wait a turn (10 in-game minutes) |
| Esc | Quit |
| R | Restart (after death) |

In menus, press the number of an entry; Esc cancels.

## Layout

- `src/SevenDays.Core` – game logic (world gen, clock, zombies, crafting). No I/O, fully testable.
- `src/SevenDays.Godot` – Godot 4 front-end (`Main.cs` draws the map with `_Draw`, handles input, menus and HUD).
- `tests/SevenDays.Tests` – xUnit tests.
