# Known Issues

- Only iNES 1.0 Mapper 0 with 16KB/32KB PRG and 8KB CHR ROM is accepted.
- NES 2.0, CHR RAM, four-screen mirroring, and other mappers are rejected.
- Unofficial 6502 opcodes are intentionally rejected.
- The PPU renders at scanline/dot timing, but internal fetch/open-bus quirks are not fully cycle-exact.
- If an opaque, visible sprite zero never overlaps because of remaining fetch-pipeline differences, a late-scanline timing fallback prevents split-screen wait loops from deadlocking.
- Scroll and nametable selection are latched per scanline; sub-scanline raster effects remain approximate.
- 8x16 sprites and sprite-overflow behavior are not implemented yet.
- Controller, APU register behavior, 3D rendering, and profiles are not operational yet.
- ROMs that intentionally execute unofficial 6502 opcodes are rejected. The runner pauses on the first fault and reports CPU state once.
- User-owned ROM files are never imported into `Assets`.
