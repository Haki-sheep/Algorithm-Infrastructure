# Runtime commands

Commands served by the Player / dev-build server. Many require a Development build with the runtime Pipeline enabled — see [Runtime setup](../runtime-setup.md). Commands marked `RuntimeOnly` are only registered in a player build.

### `runtime_status`
Get comprehensive runtime application status.

No parameters.

**Returns:** `RuntimeStatusResponse`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `quit`
Gracefully quit the Unity application.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `exitCode` | no | `0` | Exit code for the application |

**Returns:** `string`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `set_target_framerate`
Set the target frame rate for the application.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `frameRate` | yes | `–` | Target frame rate (-1 for platform default, 0 for unlimited) |

**Returns:** `string`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `set_timescale`
Set the time scale for the application.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `scale` | yes | `–` | Time scale multiplier (0.0 to pause, 1.0 for normal speed) |

**Returns:** `string`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `simulate_key`
Simulate a keyboard key event (Input System). Drives the running app.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `key` | yes | `–` | Input System Key name, e.g. Space, W, Enter, LeftArrow. |
| `action` | no | `press` | down \| up \| press (down+up). |

**Returns:** `InputSimulationResponse`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`. Requires the Input System package (`ENABLE_INPUT_SYSTEM`); reports unavailable otherwise.

### `simulate_pointer`
Simulate a mouse/pointer event at screen coordinates (Input System).

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `x` | yes | `–` | Screen X in pixels (origin bottom-left). |
| `y` | yes | `–` | Screen Y in pixels (origin bottom-left). |
| `action` | no | `click` | move \| down \| up \| click (down+up). |
| `button` | no | `left` | left \| right \| middle. |

**Returns:** `InputSimulationResponse`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`. Requires the Input System package (`ENABLE_INPUT_SYSTEM`); reports unavailable otherwise.

### `log`
Write a message to Unity console.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `message` | yes | `–` | Message to log to console |
| `level` | no | `info` | Log level: info, warning, error |

**Returns:** `string`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `console`
Get captured Unity console output (Editor or Player; supports tail, level filtering, and follow via a cursor).

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `tail` | no | `100` | Maximum number of most-recent entries to return |
| `level` | no | `log` | Minimum severity to include: log | warn | error |
| `since` | no | `-1` | Cursor: only return entries newer than this seq. Use the 'cursor' from a previous response to follow. |
| `since_session` | no | `–` | Session the `since` cursor came from: pass the `session` from a previous response. A cursor from another session is refused and the tail returned with `reset=true`. |

**Returns:** `ConsoleLogResponse`
**Notes:** `MainThreadRequired = false`. Available in both the Editor and player builds.

A cursor is only meaningful paired with the `session` that issued it. Sequence numbers restart from
zero when the Editor restarts (the buffer is persisted under `Temp/`, which Unity deletes at every
launch), so a cursor from an earlier session names different entries. Always echo `session` back as
`since_session`; when the pair cannot be honored the response sets `reset=true`, returns the tail
instead of nothing, and gives you a fresh `cursor`/`session` to adopt.

Beyond the entries, the response carries:

- `counts` — errors/warnings/logs the buffer currently retains, before the level filter and `tail`.
- `groundTruth` — what the Editor itself reports: `compilationFailed`, `compiling`, the Editor
  console's own `consoleErrors`/`consoleWarnings`/`consoleLogs`, whether the buffer has been
  `seeded` from the console's store, and `ageMs` for how stale the sample is. Null in a player.

`groundTruth.consoleErrors` exceeding `counts.error` means the Editor console is showing entries the
buffer does not hold. That is expected for output produced before the package was first imported;
otherwise it is the signal that something was missed.

### `console_status`
Console ground truth and buffer counters without pulling entries: compile-failure flag, Editor
console counts, and the buffer's retained counts and cursor.

No parameters.

**Returns:** `ConsoleLogResponse` with an empty `entries` array.
**Notes:** `MainThreadRequired = false`. Cheap enough to poll in a loop while a compile runs; use
`console` when you want the entries themselves.

### `clear_console`
Clear the captured log buffer and the Unity Editor console.

No parameters.

**Returns:** `object`
**Notes:** Available in both the Editor and player builds; in a player only the captured buffer is
cleared, since there is no Editor console. Editor compile errors are logged as sticky entries, which
Unity's own clear skips, so they remain in the Console window until the next compile completes.
Clearing keeps the cursor, so a client following `console` sees `dropped=true` on its next poll
rather than silently skipping the window that was cleared.

### `eval`
Evaluate C# code dynamically using Roslyn compiler.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `code` | yes | `–` | C# code to evaluate |
| `timeout` | no | `5000` | Timeout in milliseconds |

**Returns:** `EvalResponse`
**Notes:** `MainThreadRequired = true`. Available on both editor and runtime.

### `eval_file`
Evaluate C# code read from a `.cs` file on disk. A convenience alternative to `eval` for code
too long to pass inline — edit the file, then evaluate it. The resolved source is run through the
same evaluation path as `eval`.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `file` | yes | `–` | Path to a `.cs` file to evaluate |
| `timeout` | no | `5000` | Timeout in milliseconds |

The `file` is read on the Unity side (relative paths resolve against the Unity process working
directory) and must end in `.cs`. A missing file, a non-`.cs` extension, or an empty file returns
a `Bad Request`.

**Returns:** `EvalResponse`
**Notes:** `MainThreadRequired = true`. Available on both editor and runtime.

> For anything beyond an ad-hoc one-liner — bulk construction, builder scripts — prefer the
> Editor-side [`run_script`](scripts.md#run_script) command, which compiles a versioned project
> `.cs` file in memory and runs a named entry point instead of carrying code through the protocol.

### `reload_file`
Compile and apply [CodeReload] edits from a source file.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `filename` | yes | `–` | Source file containing [CodeReload] methods (e.g. Assets/Scripts/Player.cs) |
| `timeout` | no | `30000` | Compilation timeout in milliseconds |
| `assemblyDir` | no | `–` | Directory to save compiled assemblies to disk (optional, default is in-memory only) |
| `pdb` | no | `false` | Emit debug symbols (portable PDB) mapped to the original source so breakpoints bind in your editor. Compiles unoptimized. |

**Returns:** `CodeReloadResponse`
**Notes:** `MainThreadRequired = true`. See [Reloading code](../code-reload.md).

### `reload_file_editor_interpreter`
Compile [CodeReload] edits and run them through the IlInterpreter VM in this process instead of `Assembly.Load` (IL2CPP-safe; only a minimal host API + the target type are available).

Same parameters as [`reload_file`](#reload_file).

**Returns:** `CodeReloadResponse`
**Notes:** `MainThreadRequired = true`. See [Reloading code](../code-reload.md).

### `reload_file_player_interpreter`
Compile a file's (or folder's) [CodeReload] methods in the Editor and push the IL to connected development players over PlayerConnection (IL2CPP-safe). Folders are walked recursively; files that do not mention `CodeReload` are skipped.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `filename` | yes | `–` | Source `.cs` file or a folder containing [CodeReload] methods (e.g. Assets/pong.cs or Assets/Scripts) |
| `player` | no | `-1` | Target connected player id; `-1` broadcasts to all connected players |

**Returns:** `{ success, message, diagnostics }` for a file; `{ success, message, pushed, failed }` for a folder.
**Notes:** `MainThreadRequired = true`. Served by the **Editor** server (it needs Roslyn and PlayerConnection), not the player; the player only needs the runtime Pipeline driver, not its HTTP server. See [Reloading code](../code-reload.md#reload_file_player_interpreter).

### `codereload_status`
Show current code reload registry status and statistics.

No parameters.

**Returns:** `CodeReloadResponse`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

### `cleanup_codereload`
Remove old code reload DLL versions and clear registry.

| Parameter | Required | Default | Description |
|-----------|----------|---------|-------------|
| `assemblyDir` | yes | `–` | Directory containing assemblies to cleanup |
| `force_domain_reload` | no | `true` | Force Unity domain reload after cleanup |

**Returns:** `CodeReloadResponse`
**Notes:** `MainThreadRequired = true`, `RuntimeOnly`.

See [Creating commands](../creating-commands.md) and [Connectivity](../connectivity.md).
