# How to Fish v1.0.9 — iOS port recovery notes

Recovered directly from the supplied Windows build and its torrent metadata.

## Build

- Unity: `6000.4.4f1` (Unity 6.4)
- Scripting backend in supplied build: Mono
- Render pipeline: URP `17.4.0`
- Input System: `1.19.0`
- FishNet is present, plus both FishySteamworks and FishyUnityTransport/Unity Transport.
- Original build tree was fully reconstructed from torrent metadata: 319/319 files, no missing files.

## Original UI objects confirmed in serialized assets

Settings:
- `GameplayButton`
- `GraphicsButton`
- `AudioButton`
- `GameplayDisplay (To toggle)`
- `GraphicsDisplay (To toggle)`
- `AudioDisplay (To toggle)`
- `SensitivitySlider`
- `DropRebind`, `CrouchRebind`, `SprintRebind`, `PrimaryRebind`, `SecondaryRebind`, etc.

Inventory/hotbar:
- `InventorySlotHolder`
- `InventoryHolder`
- `InventorySlot`
- input actions `Inventory1` ... `Inventory9`, `InventoryScroll`, `InventoryNone`

The mobile port keeps these original hotbar visuals. Touch support is attached as an invisible child hitbox to each active original `InventorySlot`; it is not a replacement hotbar. This naturally handles the game starting with three slots and unlocking more later.

## Input semantics recovered

- Move: WASD / gamepad left stick — continuous
- Look: mouse delta / gamepad right stick — continuous
- Primary: LMB / right trigger — press/hold/release, contextual
- Secondary: RMB / left trigger — press/hold/release, contextual
- Jump: Space / south button — tap
- Sprint: Left Shift / left-stick press — held action; canceled callback exists
- Crouch: Left Ctrl / east button — held action; canceled callback exists
- Interact/Pick up: E / west button — tap
- Drop: Q / north button — tap plus held charge/release. The original game contains `StartDropUI`, `UpdateDropUI`, `StopDropUI`, `_startDropForceDelay`, `_timeUntilMaxDropForce` and canceled handling. Mobile therefore sends the original input rather than reimplementing charge timing/UI.
- Journal/Tab: Tab / select — tap
- Change bait: B / right-stick press — tap
- Reload: R — tap
- Inspect: F / dpad up — tap
- Push to talk: V / dpad left — held action; canceled callback exists
- Pause: Escape / start — tap

## Mobile implementation started

`unity-port/Assets/HowToFishMobile` contains the first port layer:

1. Runtime landscape HUD using Unity Input System `OnScreenButton` / `OnScreenStick`.
2. Action buttons forward to the game's existing bindings, preserving original tap/hold/cancel logic.
3. Original hotbar slots are left visually unchanged and receive transparent touch hitboxes dynamically as they unlock.
4. A separate `Mobile` settings tab is injected by cloning the game's own `GameplayButton` style.
5. Layout edit mode: drag a mobile control to reposition it; select it and use the size slider; reset persists in `Application.persistentDataPath/mobile-controls.json`.
6. Drop and crouch icons have already been replaced in the port layer.
7. iOS project defaults: landscape, iOS 16+, ARM64, IL2CPP.

## Platform blockers still to patch before first game boot

- Steam initialization / `steam_api64.dll` / `SteamFix64.dll` cannot exist on iOS. The non-Steam FishyUnityTransport path must be selected before network initialization.
- `rnnoise.dll` is Windows x64. Voice noise suppression must be disabled for the first boot or RNNoise must be built for iOS ARM64 later.
- Windows DirectStorage/D3D12/winmm plugins are excluded; Unity's iOS player uses Metal and native iOS APIs.
- Windows Burst library is not reused. Unity must regenerate Burst/IL2CPP output for ARM64.

Next stage: reconstruct project assets/scripts, wire the mobile layer into the recovered project, select the non-Steam transport path on iOS, and produce an iOS Xcode export for the first actual boot test.
