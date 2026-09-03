# Implementation Status

## Milestone 1: ROM load — implemented

- Strict iNES magic/length validation; iNES 1.0 Mapper 0 only
- 16KB/32KB PRG, 8KB CHR, trainer, battery flag, horizontal/vertical mirroring
- NROM-128 mirroring and NROM-256 mapping
- `PortalNes > ROM Inspector` editor window
- EditMode loader and mapper tests

## Milestone 2: CPU — complete

- All documented NMOS 6502 opcodes used by the Ricoh 2A03
- Addressing modes, status flags, stack, reset, NMI, IRQ, BRK, RTI, and RTS
- Branch and indexed-read page-crossing cycle penalties
- 6502 indirect JMP page-boundary hardware bug
- CPU RAM mirroring and Mapper 0 program reads through `CpuBus`
- Unsupported unofficial opcodes fail with opcode and address information

## Milestone 3: PPU and 2D output — complete

- CPU-visible PPU registers and mirrors, buffered PPUDATA reads, address/scroll latch behavior
- Pattern, nametable, attribute, palette, and OAM memory
- Horizontal/vertical nametable mirroring and palette aliases
- 262-scanline/341-dot NTSC timing, VBlank, and NMI request
- Dot-timed background and 8x8 sprite rasterization, priority, flipping, sprite-zero hit, and eight-sprites-per-scanline evaluation
- OAM DMA transfer with 513/514 CPU-cycle stall accounting
- 256x240 RGBA framebuffer and point-filtered Unity `Texture2D` upload
- 60.0988 Hz runner with catch-up limit

## Milestone 4: Input — implemented, awaiting Unity Test Runner confirmation

- NES controller A/B/Select/Start/directional bit layout
- `$4016` strobe and serial reads; controller 2 reads through `$4017`
- Keyboard controls: arrows/WASD, Z/J, X/K, Enter, Right Shift/Backspace
- Gamepad D-pad, south/west buttons, Start, and Select
- Opposite direction filtering
- Local ROM path picker on the `NesRunner` inspector
- `PortalNes > Create 2D Demo Rig` menu for a pre-wired point-filtered screen, runner, and input provider

## Not implemented

Cycle-exact pixel pipeline quirks, 8x16 sprites, scene snapshots, 3D rendering, profiles, mesh/instancing optimization, and audio.
