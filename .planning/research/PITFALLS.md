# Pitfalls Research

**Domain:** Windows-first desktop terminal multiplexer for AI coding workflows
**Researched:** 2026-03-06
**Confidence:** MEDIUM

## Critical Pitfalls

### Pitfall 1: Fake terminal integration

**What goes wrong:**  
The app launches shells as child processes but does not preserve real terminal behavior, so cursor movement, alternate screen apps, full-screen TUIs, copy/paste semantics, or shell-specific prompts behave incorrectly.

**Why it happens:**  
Teams optimize for getting text on screen quickly and underestimate how much terminal behavior users expect to "just work."

**How to avoid:**  
Adopt ConPTY-backed session hosting from the beginning and define terminal fidelity tests before building advanced UI.

**Warning signs:**  
`vim`, `less`, `fzf`, `claude`, or `codex` behave differently than they do in Windows Terminal or WezTerm.

**Phase to address:**  
Phase 1 - terminal runtime and pane foundation

---

### Pitfall 2: PTY deadlocks from poor IO threading

**What goes wrong:**  
Terminal sessions freeze, drop output, or hang on shutdown under load.

**Why it happens:**  
ConPTY uses synchronous communication channels, and Microsoft's pseudoconsole guidance explicitly warns that servicing both channels on the same thread can deadlock.

**How to avoid:**  
Use separate read/write servicing paths per session, structured tracing, and shutdown integration tests.

**Warning signs:**  
Large command output stalls a pane, resize events hang, or process exit leaves ghost sessions.

**Phase to address:**  
Phase 1 - terminal runtime and process lifecycle

---

### Pitfall 3: Over-scoping parity before proving the core

**What goes wrong:**  
The roadmap chases browser integration, workspace automation, or a full socket API before the terminal core is trustworthy.

**Why it happens:**  
Upstream `cmux` has a broad product surface and it is tempting to copy all of it immediately.

**How to avoid:**  
Lock v1 around real sessions, splits, tabs, and notifications; treat browser/workspace/API parity as later milestones unless they become required by validation.

**Warning signs:**  
Roadmap discussions spend more time on browser or automation surfaces than on shell fidelity, resize behavior, or focus rules.

**Phase to address:**  
Phase 0 / roadmap definition and every later scoping checkpoint

---

### Pitfall 4: Agent-specific notification semantics

**What goes wrong:**  
Notifications work for one tool but not for the broader class of terminal-based agents and scripts.

**Why it happens:**  
The app is designed around a single integration instead of generic attention signaling.

**How to avoid:**  
Support standard terminal notification patterns first, including OSC-based attention signals and explicit process-state hooks where possible.

**Warning signs:**  
The notification design assumes "Claude waiting" instead of "a pane or session needs attention."

**Phase to address:**  
Phase 2 - notifications and attention UX

---

### Pitfall 5: Shell lock-in too early

**What goes wrong:**  
The product becomes tightly coupled to WinUI-only or Tauri-only assumptions before the native-vs-fallback decision is fully validated.

**Why it happens:**  
UI code and session logic are built together with no clear boundary.

**How to avoid:**  
Keep the layout/session/notification model in a shell-agnostic core and make the outer shell a replaceable adapter.

**Warning signs:**  
PTY process logic directly depends on view classes, or replacing the shell would require rewriting the state model.

**Phase to address:**  
Phase 1 - project scaffolding and architecture

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hard-code a single shell profile | Faster first demo | Breaks PowerShell / cmd / WSL expectations and makes testing shallow | Only for a throwaway prototype, not for the planned v1 |
| Persist layout with ad hoc JSON blobs and no schema version | Quick save/restore | Painful migrations and brittle recovery later | Acceptable only if schema versioning is added before release |
| Fire desktop notifications without in-app unread state | Easier implementation | Users lose context once the toast disappears | Never for the intended workflow |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| ConPTY | Mixing all PTY traffic on one blocking thread | Isolate PTY IO per session and test shutdown / resize explicitly |
| Windows notifications | Assuming every dev-time notification behavior matches installed-app behavior | Test packaged and installed paths, especially if Tauri fallback is used |
| Shell current working directory | Ignoring shell integration / OSC cwd updates | Capture cwd updates explicitly so duplicate-tab and split behavior can inherit directory correctly later |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Redraw on every byte of PTY output | High CPU, laggy typing, pane jitter | Buffer and coalesce screen updates | As soon as several active agents stream output at once |
| Re-layout full tree on every focus or resize | Noticeable pane lag | Use a deterministic split tree and incremental layout math | With nested splits and many tabs |
| Unbounded scrollback / logs in memory | Memory growth and sluggish tab switches | Cap buffers and persist selectively | Long-running coding sessions or agent logs |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| Exposing a future automation API with no command restrictions | Arbitrary local execution surface | Ship no public automation surface in v1, or gate it behind explicit local permissions |
| Treating notification payloads as trusted content | UI spoofing or bad links | Sanitize titles/bodies and treat external payloads as untrusted |
| Running everything under one implicit trust model | Confusing behavior across PowerShell, cmd, WSL, and future remote sessions | Model session metadata explicitly and log what was launched and why |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Tabs without enough metadata | Users lose track of which agent/session is where | Show title, cwd, shell/tool identity, and unread state |
| Notifications that fire while the relevant pane is already focused | Feels noisy and broken | Suppress desktop alerts when the active tab/pane already has focus |
| Pane splitting with inconsistent orientation terms | Users cannot build muscle memory | Match clear horizontal/vertical semantics and keep shortcuts consistent |

## "Looks Done But Isn't" Checklist

- [ ] **Terminal host:** Verify full-screen TUIs, resize, copy/paste, and process exit all behave correctly.
- [ ] **Pane management:** Verify split, close, move focus, and tab switch all preserve session state.
- [ ] **Notifications:** Verify in-app unread markers and Windows desktop toasts stay in sync.
- [ ] **Fallback shell path:** Verify the core still works if the shell implementation changes from native to Tauri.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| Fake terminal integration | HIGH | Rebuild the session layer around ConPTY, then retest every terminal behavior path |
| PTY deadlocks | HIGH | Add tracing around IO paths, reproduce with stress fixtures, split blocking flows, and rework shutdown |
| Over-scoped roadmap | MEDIUM | Cut v1 back to core table stakes and move parity work into later phases |
| Agent-specific notifications | MEDIUM | Replace tool-specific hooks with generic attention events and OSC support |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| Fake terminal integration | Phase 1 | Run TUIs and agent CLIs side by side with parity checks against Windows Terminal |
| PTY deadlocks | Phase 1 | Stress-test concurrent output, resize, and shutdown |
| Over-scoping parity | Phase 0 / Phase 1 planning | Confirm roadmap keeps v1 focused on terminal-first requirements |
| Agent-specific notifications | Phase 2 | Validate notifications using more than one terminal-based tool and a generic OSC trigger |
| Shell lock-in too early | Phase 1 | Prove the shell can call into the core without owning session logic |

## Sources

- https://learn.microsoft.com/en-us/windows/console/creating-a-pseudoconsole-session - Primary source for ConPTY behavior and deadlock warnings.
- https://learn.microsoft.com/en-us/windows/apps/develop/notifications/app-notifications/ - Primary source for Windows desktop notification behavior.
- https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/notifications/app-notifications/app-notifications-quickstart - Primary source for app notification implementation constraints.
- https://www.cmux.dev/docs/notifications - Upstream reference for attention-state lifecycle and suppression behavior.
- https://wezterm.org/config/lua/window/toast_notification.html - Reference for multiplexer-style desktop notification behavior.
- https://wezterm.org/config/lua/config/notification_handling.html - Reference for notification suppression modes.
- https://wezterm.org/shell-integration.html - Reference for cwd and prompt-state shell integration.

---
*Pitfalls research for: Windows-first desktop terminal multiplexer for AI coding workflows*
*Researched: 2026-03-06*
