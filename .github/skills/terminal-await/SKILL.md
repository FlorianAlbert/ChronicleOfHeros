---
name: terminal-await
description: Await terminal commands that run long, time out, move to the background, or prompt for input. Use when choosing a terminal execution mode or handling a command completion notification.
---

# Await Terminal Commands

For one-shot commands, call `run_in_terminal` in `sync` mode and omit
`timeout`. The completed tool response contains the final output; treat it as
the command's completion.

## Deferred Completion

When the terminal tool reports that execution moved to the background, timed
out, or needs input, retain its terminal ID, then return control to the
harness immediately. The next terminal action is the harness notification; it
reports completion, exit code, and output on a subsequent turn.

Process that notification as the final result. Call `get_terminal_output` only
when the notification explicitly requires an additional retrieval. It is never
a completion check.

## Persistent Processes

Use `async` only for intentionally persistent processes such as servers,
watchers, or daemons. Retain the terminal ID to send input or stop the process
when the task requires it.