# Change controls

Open **Settings → Controls → Show/Change controls**. Three tabs configure **Overlay**, **Search keyboard** and **Search results**. Each shows the diagram matching your PlayStation, Xbox or Nintendo button style. Click a label to choose the action for that physical button. Choose **Unassigned** to clear it; the overlay also offers the shared **Dismiss (tap) / Exit app (hold)** action.

One action can use multiple buttons. Assigning an action keeps its existing buttons and replaces the clicked button’s previous action. Only labels whose action is also assigned to another button are yellow; ordinary assignments are mint. Removing a duplicate restores the normal colour. The diagram background uses the arrow cursor, while clickable labels use a hand.

Each tab has a **Keyboard shortcuts…** button. Click a shortcut and press the combination, including Ctrl/Alt/Shift if needed; click it again to cancel capture, or × to clear it. Keyboard Apply stages the edits. Duplicate shortcuts within a context block Apply with a conflict explanation. Search shortcuts take priority over literal typing. Keyboard Exit is immediate and has no default shortcut.

The main **Save changes** commits controller and keyboard edits together; Cancel discards them. **Restore defaults** resets both input types and clears additional controller assignments, but still needs Save changes. An action shared between buttons is valid. Assigning the same physical button to different actions remains a conflict, except for the supported overlay Dismiss/Exit tap-and-hold pairing. Tray access remains available even if actions are unassigned.

Mappings use physical inputs, so changing button style only changes labels and the illustration. Stick axes and speed settings remain separate; D-pad buttons are remappable. Set the Jump action’s destination in the main Controls settings. The supplied transparent controller images are embedded in the executable and decoded once per style, on demand, at reduced display resolution.

Settings stores primary mappings in `ControlButtons`, additional buttons for an action in `ControlButtonAlternates`, and shortcuts in `ControlKeys`, keyed by context/action. Missing or invalid primary entries use defaults; `none` is unassigned. Contexts remain independent. Defaults include R3 favourites, Square for Grid/carousel and R2 for next monitor on the normal overlay; search typing uses Triangle for space, Circle to exit, and Square to backspace. From search results, Triangle returns to the keyboard and Circle exits. Up above the keyboard focuses games; Down below the final game row returns to the keyboard.

**Remember layout changes made with controls** is unchecked by default. When enabled, the layout action saves Grid ↔ Angled/Rectangular changes for the active list across restarts and remembers its previous carousel layout. This applies to remapped controller and keyboard actions, including search. Other per-list sizing and artwork settings are preserved.

Settings and Change controls fit the current monitor’s usable area with Windows DPI scaling. Limits update after DPI, display and work-area changes and moves between monitors. Forms scroll and diagrams fit the available space.

Regression checks cover persisted multiple bindings and runtime dispatch, duplicate label colours, cursor behavior, action selection, keyboard capture and conflicts, save/cancel/restore, tap/hold sharing, independent contexts, trigger hysteresis, aliases, and diagram layout.

Change controls is a non-modal owned window: main Settings remains usable and can be minimized while it is open. Opening it again focuses the existing editor and preserves pending edits. Closing main Settings also closes its controls editor.
