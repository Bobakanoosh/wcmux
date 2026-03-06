# Pitfalls Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Shipping a fake terminal instead of a PTY host

**What goes wrong:**
The app can launch commands, but TUIs, resize behavior, cursor handling, and shell fidelity are broken or inconsistent.

**Why it happens:**
Teams confuse process spawning with terminal hosting and try to wire stdout/stderr into a generic text control.

**How to avoid:**
Treat ConPTY as non-negotiable for the terminal core. Prove shell, `vim`, and agent workflows against the same host early.

**Warning signs:**
Apps work for `echo` and logs, but fail on full-screen tools, alternate screen buffers, or keyboard shortcuts.

**Phase to address:**
Phase 1

---

### Pitfall 2: Deadlocks and teardown bugs in ConPTY I/O

**What goes wrong:**
Sessions hang on startup, resize, or shutdown; shells survive after the pane closes; the app leaks handles or blocks the UI.

**Why it happens:**
Microsoft's ConPTY guidance is explicit that synchronous channels need dedicated servicing and careful lifetime handling.

**How to avoid:**
Run input and output pumps independently, test teardown explicitly, and make resize/close behavior part of the first integration suite.

**Warning signs:**
Intermittent hangs, orphaned shell processes, or shutdown paths that depend on timing.

**Phase to address:**
Phase 1

---

### Pitfall 3: Coupling layout state to specific UI widgets

**What goes wrong:**
Splits, tabs, restore, and later automation become brittle because state only exists inside view objects.

**Why it happens:**
It is tempting to let pane controls directly own process and layout behavior in a desktop UI framework.

**How to avoid:**
Keep layout trees and session ownership in domain state, not in the visual tree.

**Warning signs:**
Moving or duplicating panes requires special-case UI code, and restore logic starts reading visual state instead of persisted state.

**Phase to address:**
Phase 2

---

### Pitfall 4: Designing notifications before install identity is real

**What goes wrong:**
Notifications seem fine in a narrow dev path, then fail or behave differently after distribution.

**Why it happens:**
Windows desktop notifications depend on packaging, AUMID/activator setup, and have documented limitations for elevated apps.

**How to avoid:**
Decide early how the app will be installed and tested, and validate notifications in an installed build, not only a dev session.

**Warning signs:**
Notifications do not persist, clicks do not reactivate the app, or behavior changes between packaged and unpackaged runs.

**Phase to address:**
Phase 3

---

### Pitfall 5: Trying to reach full cmux parity before the Windows core feels good

**What goes wrong:**
The project stalls across browser, automation, restore, and metadata work before the base terminal experience is trustworthy.

**Why it happens:**
Upstream parity is seductive, especially when the reference product already demonstrates many desirable features.

**How to avoid:**
Enforce a terminal-first roadmap: real sessions, panes, tabs, notifications, then expand.

**Warning signs:**
Roadmap items multiply before a single reliable daily-driver build exists.

**Phase to address:**
Phase 0 / roadmap definition

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Store layout only in UI widgets | Faster first prototype | Makes restore, automation, and testing painful | Never beyond a throwaway spike |
| Skip profile abstraction and hard-code PowerShell | Faster demo | Blocks WSL/cmd/custom shell support and user trust | Only in the first ConPTY spike |
| Ignore packaging until release time | Faster local iteration | Notifications and activation paths break late | Never for the notification phase |
| Treat alert parsing as vendor-specific | Faster Claude/Codex demo | Locks the product to one tool and violates the primitive model | Only if wrapped behind a generic attention-event adapter |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| ConPTY | Drive everything from one blocking thread | Separate pump responsibilities and test teardown paths |
| Windows notifications | Test only in an unpackaged debug loop | Validate installed app behavior and activation wiring |
| Tauri fallback | Assume shell plugin equals PTY terminal | Keep ConPTY as a dedicated backend layer |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Re-render whole terminal surface on every chunk | Laggy typing and high CPU | Batch output and use renderer-level diffing | Usually visible with active agent output or large scrollback |
| Unlimited scrollback in memory | RAM growth and sluggish tab switching | Cap scrollback and persist selectively | Noticeable with long coding sessions |
| Resize spam directly to shell | Flicker and unstable TUIs | Debounce physical resize and commit character-grid changes deliberately | Visible during aggressive pane resizing |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Over-broad shell execution permissions in Tauri | Arbitrary command execution surface becomes too open | Restrict plugin capabilities and keep command routing explicit |
| Unsanitized CLI/API automation surface | Local privilege surprises or destructive commands | Treat automation as a typed command layer with validation |
| Running elevated and expecting notifications to still work | Attention flow silently breaks | Document and test the non-elevated path as the supported mode |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Pane actions feel inconsistent by direction | Users lose trust in splits quickly | Match Windows Terminal directional split/focus vocabulary |
| Notifications are noisy instead of actionable | Users disable them | Deduplicate and route to the exact pane/tab needing attention |
| Tab model is too abstract too early | Users cannot tell where work lives | Start with simple tabs and clear titles before inventing richer workspace metaphors |

## "Looks Done But Isn't" Checklist

- [ ] **Terminal host:** Often missing resize and teardown correctness - verify with `vim`, `less`, and shell exit paths
- [ ] **Pane splitting:** Often missing focus traversal and close semantics - verify keyboard navigation in uneven pane trees
- [ ] **Notifications:** Often missing installed-build validation - verify click activation and persistence outside the dev loop
- [ ] **Tabs:** Often missing independent session ownership - verify closing one tab does not leak or kill unrelated sessions

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Fake terminal backend | HIGH | Replace the backend with ConPTY and re-test all TUI cases |
| Layout/UI coupling | MEDIUM | Extract domain state, migrate panes to reference sessions, then rework restore |
| Broken notification activation | MEDIUM | Fix packaging/AUMID/activator path and validate installed builds |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Fake terminal backend | Phase 1 | `pwsh`, `cmd`, `wsl`, `vim`, and resize all work through ConPTY |
| ConPTY deadlocks | Phase 1 | repeated open/close/resize cycles do not hang or leak |
| Layout coupled to UI | Phase 2 | pane trees can be serialized/restored without view state hacks |
| Notification identity issues | Phase 3 | installed builds raise and activate notifications correctly |
| Premature parity scope | Roadmap creation | roadmap keeps browser/API work after terminal core proof |

## Sources

- Microsoft Learn: Creating a Pseudoconsole session
- Microsoft Learn: App notifications overview and desktop notification guides
- Microsoft Learn: Windows Terminal panes and shell integration docs
- Tauri official shell and notification plugin docs
- `cmux` upstream README and scope

---
*Pitfalls research for: Windows-first desktop terminal multiplexer*
*Researched: 2026-03-06*
