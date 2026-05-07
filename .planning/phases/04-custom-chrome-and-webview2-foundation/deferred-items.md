# Deferred Items - Phase 04

## Pre-existing Build Error

**File:** `src/Wcmux.App/Terminal/WebViewEnvironmentCache.cs` (line 28)
**Error:** CS1739 - The best overload for 'CreateAsync' does not have a parameter named 'browserExecutableFolder'
**Impact:** Prevents `dotnet build src/Wcmux.App` from succeeding. This is NOT caused by Phase 04 changes.
**Note:** This file is untracked (not committed yet), along with its test file. Likely from an incomplete prior phase or work-in-progress.
