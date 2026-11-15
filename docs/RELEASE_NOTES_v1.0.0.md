# EV Service Trainer v1.0.0

**EV Battery Pack Assembly training for Meta Quest 2, 3 and 3S.**

EV Service Trainer is a VR assembly trainer. You build an electric-vehicle battery pack by hand at a workbench:
cooling plate, four battery modules, busbars, BMS board, HV connector and a heavy pack cover lifted with both hands,
then eight cover bolts hand-started and torqued in a cross pattern to 8 ± 0.4 N·m. Every step is validated.

- **Learn mode** guides you with ghost targets, hints and pattern numbers, and blocks wrong actions with an
  explanation.
- **Test mode** gives no guidance, logs every mistake and ends with a scored **PASS / FAIL** report.
- Each completed run is saved as a JSON report on the headset.
- After completion, review the pack in an **exploded view** or a **disassembly** replay.
- Works with **controllers** and **hand tracking**.

## Requirements

- Meta Quest 2, Quest 3 or Quest 3S, with developer mode enabled to sideload apps.
- A play space where you can stand at a virtual workbench (about 1.5 × 1.5 m).
- A computer with [adb](https://developer.android.com/tools/adb) or [SideQuest](https://sidequestvr.com/) and a USB
  cable.

## Install

Download `EVServiceTrainer-1.0.0.apk` from the assets of this release.

### With adb

1. Connect the headset over USB and accept the **Allow USB debugging** prompt inside the headset.
2. Check that the device is listed:
   ```bash
   adb devices
   ```
3. Install (or update) the app:
   ```bash
   adb install -r EVServiceTrainer-1.0.0.apk
   ```

### With SideQuest

1. Connect the headset and open SideQuest. The status dot at the top left should be green.
2. Click **Install APK file from folder on computer** (the box-with-arrow icon) and choose the APK, or drag the APK
   onto the SideQuest window.

### Launch

In the headset open **Library**, filter by **Unknown Sources**, and start **EV Service Trainer**.
Choose **Learn** or **Test** on the console with a poke of your finger.

### Training reports

Reports are stored on the headset in
`/sdcard/Android/data/com.brijeshyadav.evservicetrainer/files/reports/`. Copy them with:

```bash
adb pull /sdcard/Android/data/com.brijeshyadav.evservicetrainer/files/reports ./reports
```

## Known limitations

- One training procedure: EV Battery Pack Assembly.
- The procedure, parts and torque values are simplified for demonstration; this is **not certified safety
  training** and not affiliated with any vehicle manufacturer.
- Parts snap and lock by tolerance rules rather than full physics simulation; fasteners and tools never fall.
- Hand-tracking quality depends on lighting and on the hands staying in view. Controllers are more precise for
  the torque wrench and for fine placement.
- The workbench has a fixed height, designed for standing use.
- Reports are saved on the headset only (no account, cloud sync or LMS integration) and the newest 20 are kept.
- English only.
- Distributed as a sideloaded APK; not available on the Meta Horizon Store.
