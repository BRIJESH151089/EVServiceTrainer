# Changelog

All notable changes to this project are documented in this file.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) and the project uses
[Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-11-15

First release: EV Battery Pack Assembly training for Meta Quest 2, 3 and 3S.

### Added

- **Assembly procedure**: cooling plate, four battery modules (orientation checked: + terminal to the front), three
  busbars, BMS board, HV connector and pack cover, on a pack tray held by a fixture.
- **Step validation**: prerequisite graph per part; parts offered in the wrong order are rejected with the reason.
- **Snap assist**: ghost targets, position and angle tolerances, per-mode capture zones; parts lock in place with
  sound and haptics.
- **Two-hand cover lift** for the heavy pack cover.
- **Cover bolts**: drop-in, hand-start by wrist rotation, cross-thread check on tilted starts.
- **Torque wrench**: ratchet driven by swing angle, live gauge and torque ring, click at 8 ± 0.4 N·m, cross-pattern
  validation, over-torque detection.
- **Learn and Test modes**: Learn guides with ghosts, hints and pattern numbers and blocks wrong actions; Test gives
  no guidance and logs every mistake.
- **Scoring and report**: penalties per mistake type, critical mistakes, time adjustment, PASS / FAIL (practice
  summary in Learn), report panel with steps in order, wrong-order attempts, first-time fits, torque in spec,
  pattern followed and total time.
- **JSON export** of every completed run to the app's storage (newest 20 kept); the last result is shown on the start
  panel.
- **Retry Test and Practice** buttons that start a new run straight in the chosen mode.
- **Exploded view and disassembly review** after completion, with labelled layers.
- **Input**: controllers and hand tracking through one input model; self-made glove hands for both, posed by tracked
  joints or by controller grip and trigger; poke-operated push buttons.
- **Workshop environment** with baked lighting, procedural audio and haptic feedback.
- **Release tooling**: `EV Trainer → Release` menu for release and development APK builds.
- 280 NUnit Edit Mode tests for the Core logic.
