TapLauncher (optional companion)

Download TapLauncher.exe separately. No ZIP, installation or resident helper
process is required. Place it beside SteamPadLauncher.exe to preselect that
application on first setup. Run TapLauncher.exe once to save the target.
Use TapLauncher.exe --configure to change its target later.

Enable DS4Windows UDP Server at 127.0.0.1:26760. Configure your existing
button-binding application to start TapLauncher.exe on PS button PRESS.
Keep your DS4Windows controller shutdown action; TapLauncher never turns
off hardware itself. A PS release launches the target; a hold of 700 ms,
disconnect or stalled feed cancels. No XInput fallback is used by this helper.

Multiple controllers
The helper observes DS4Windows' four DSU slots and selects the controller
holding PS. Idle controllers do not block it. Once selected, that controller
owns the gesture until release, timeout or disconnect. Simultaneous presses
use the first observed PS press. Only one helper instance runs at a time.
With one controller, an already-finished tap can launch immediately after a
brief discovery window. With multiple controllers, a tap that ends before
the helper observes PS cannot be attributed safely and does not launch.

Settings and logs are stored beside TapLauncher.exe; use a writable folder.
--dry-run checks a gesture without launching the target.
--diagnose logs the physical feed for 15 seconds.
--self-test runs twelve simulated UDP scenarios and a settings round-trip.

Controller limits
Steam Pad Launcher polls all four XInput slots and registered supported
native Sony controllers. These are platform/protocol limits, not unlimited
controller support. Hardware behavior should be checked with real pads.
