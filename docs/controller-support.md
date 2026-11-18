# Controller input

Pad Launcher reads Xbox/XInput controllers through Windows XInput, DualShock 4 and DualSense through native Raw Input, and original Nintendo Switch Pro Controllers through SDL 3.4.20's HIDAPI driver. Input is routed from the active controller rather than an idle first-connected pad. The Nintendo button-label setting remains independent of controller detection.

## PlayStation held input

DualShock 4 and DualSense reports retain their latest input state until another report updates it or a Windows device-removal notification clears it. An unchanged held stick no longer expires after one second without a new report. Stopping native input clears all cached states. Regression tests cover held-state retention and disconnect cleanup; physical-controller confirmation of the reported intermittent cycling issue is still needed.

## Switch Pro

The native reader targets Nintendo vendor `057e`, product `2009`, over USB or Bluetooth. SDL performs the controller handshake and stick calibration; Pad Launcher consumes its physical-position gamepad buttons and axes. South/B plays, east/A dismisses, plus shows hints, minus refreshes the library, ZR changes monitor, and right-stick click toggles favourites, matching the existing Nintendo prompts. No rumble or motion input is requested.

Windows Raw Input device enumeration checks for a connected Pro controller once per second while the overlay is active. Only then is SDL extracted, checksum-verified and loaded. SDL's Raw Input, DirectInput, Windows Gaming Input, GameInput, XInput, Sony and Joy-Con drivers are disabled for this backend, and only original Pro devices are opened. This preserves the existing readers and avoids double-reading Xbox or PlayStation input. Closing the overlay closes SDL controller handles and stops its gamepad subsystem.

The compressed driver adds about 1.2 MB to the single executable. Its uncompressed DLL and zlib license are stored in `PadLauncher.Data/runtime/SDL3/3.4.20-x64` when required. See [the bundled component record](../third_party/SDL3/README.md) for the source, version and checksums.

Joy-Cons and Switch 2 controllers are not enabled. Third-party devices that use different IDs are not claimed as supported by this initial implementation; XInput-compatible modes still use the existing XInput reader. These changes add overlay navigation, not a background Home-button launcher or a virtual controller for games.

## Validation and sources

Automated checks cover face buttons, D-pad, shoulders, stick clicks, Home/plus/minus mapping, axis signs and limits, lazy initialization, multiple-device routing, reconnects, runtime extraction, checksum repair and actual SDL loading/polling/reinitialization on Windows. Physical USB and Bluetooth Switch Pro hardware still require testing; no connected Pro controller was available during implementation.

Primary references checked on 2026-11-17:

- [SDL Switch HIDAPI driver](https://github.com/libsdl-org/SDL/blob/release-3.4.20/src/joystick/hidapi/SDL_hidapi_switch.c): USB initialization, Bluetooth reports and calibration.
- [SDL gamepad API header](https://github.com/libsdl-org/SDL/blob/release-3.4.20/include/SDL3/SDL_gamepad.h): physical button positions, gamepad type values and axis ranges.
- [SDL initialization](https://wiki.libsdl.org/SDL3/SDL_InitSubSystem) and [event pumping](https://wiki.libsdl.org/SDL3/SDL_PumpEvents): native calls run on the WPF UI thread.
- [SDL release](https://github.com/libsdl-org/SDL/releases/tag/release-3.4.20): pinned official x64 runtime.
