---
name: terminal-await
description: Await terminal commands that run long, time out, move to the background, or prompt for input. Use when choosing a terminal execution mode or handling a command completion notification.
---

# Await Terminal Commands

For one-shot commands, use the available terminal tool's synchronous mode.
Choose a wait long enough for builds and tests when the tool exposes one.
With the Bash tool, use `mode="sync"` and `initial_wait` of at least 120 seconds
for builds or tests. A completed response is the command's final result.

## Deferred Completion

When execution moves to the background, times out, or needs input, retain its
execution or terminal ID. For tools that notify on completion, return control
and resume from that notification instead of polling or rerunning the command.

Retrieve remaining output with the tool paired with the original execution,
using the same ID. With the Bash tool, use `read_bash` after completion.
Surface nonzero exit codes and requests for input; do not report success before
the command completes.

## Persistent Processes

Use asynchronous execution only for intentionally persistent processes such as
servers, watchers, or daemons. With the Bash tool, use `mode="async"` and
`detach: true`. Retain the ID, verify the process is responsive, and stop only
that specific execution when cleanup is required.
