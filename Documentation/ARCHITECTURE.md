# PortalNes Architecture

PortalNes runs an independently implemented NTSC NES core and projects PPU state into ordinary Unity world space. Portalgraph remains responsible for cameras, tracking, and stereo projection. Commercial ROM data is never stored in this repository.

## Dependency direction

`PortalNes.Emulator` is pure C# with no Unity reference. `PortalNes.UnityBridge` drives it and presents 2D output. `PortalNes.Rendering3D` consumes reusable PPU snapshots. Neither references Portalgraph; the existing Portalgraph rig views a movable `NesWorldRoot`.

```text
iNES -> Cartridge/Mapper -> CPU Bus <-> CPU
                            |          |
                            +-> PPU <--+
                                |
                    framebuffer + scene snapshot
                                |
                     Unity 2D + Unity 3D world
                                |
                     Portalgraph camera rig
```

## Assemblies

- `PortalNes.Emulator`: cartridge, mapper, CPU, bus, PPU, controller, machine; no Unity APIs.
- `PortalNes.UnityBridge`: lifecycle, timing, input adapters, and 2D texture upload.
- `PortalNes.Rendering3D`: pooled/instanced visualization and profiles.
- `PortalNes.Editor`: editor-only ROM inspection and profile tools.
- `PortalNes.Emulator.Tests`: EditMode tests for the independent core.

Milestone 1 implements strict iNES 1.0 parsing and NROM mapping. Later components are intentional API skeletons; unimplemented execution APIs throw instead of returning incorrect state. CPU, PPU/2D, input, snapshots/3D, and SMB1 profiles follow in that order. Hot paths must avoid Unity APIs and per-frame allocation. Portalgraph source and prefabs remain unchanged.
