<#
.SYNOPSIS
    Phase 1 terminal fidelity smoke harness for wcmux.

.DESCRIPTION
    Exercises the manual-only TUI fidelity matrix from the phase validation
    strategy. This script sets up repeatable scenarios that a human observer
    can verify in the running wcmux app.

    Scenarios covered:
    - Launch vim and verify alternate screen, input, and clean exit
    - Launch fzf (or a fallback alternate-screen selector) and verify
      keyboard navigation and selection
    - Paste input into the terminal
    - Aggressive resize during a live TUI session

.NOTES
    Prerequisites:
    - wcmux must be built and runnable
    - vim must be available on PATH (or neovim as nvim)
    - fzf is recommended but the script falls back to a dir listing selector

.EXAMPLE
    .\tools\Run-Phase01TerminalSmoke.ps1
#>

param(
    [string]$WcmuxExe = "",
    [switch]$SkipBuild,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"
$script:TestsPassed = 0
$script:TestsFailed = 0
$script:TestsSkipped = 0

function Write-TestHeader([string]$Name) {
    Write-Host ""
    Write-Host "======================================" -ForegroundColor Cyan
    Write-Host "  SMOKE TEST: $Name" -ForegroundColor Cyan
    Write-Host "======================================" -ForegroundColor Cyan
}

function Write-TestResult([string]$Name, [string]$Status, [string]$Detail = "") {
    switch ($Status) {
        "PASS" {
            Write-Host "  [PASS] $Name" -ForegroundColor Green
            $script:TestsPassed++
        }
        "FAIL" {
            Write-Host "  [FAIL] $Name - $Detail" -ForegroundColor Red
            $script:TestsFailed++
        }
        "SKIP" {
            Write-Host "  [SKIP] $Name - $Detail" -ForegroundColor Yellow
            $script:TestsSkipped++
        }
        "MANUAL" {
            Write-Host "  [MANUAL] $Name - $Detail" -ForegroundColor Magenta
        }
    }
}

function Test-CommandExists([string]$Command) {
    $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# ---------------------------------------------------------------
# Build check
# ---------------------------------------------------------------
Write-Host "wcmux Phase 1 Terminal Smoke Harness" -ForegroundColor White
Write-Host "====================================" -ForegroundColor White

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
if (-not $repoRoot) { $repoRoot = (Get-Location).Path }

if (-not $SkipBuild) {
    Write-Host "`nBuilding wcmux..." -ForegroundColor Gray
    Push-Location $repoRoot
    try {
        $buildOutput = dotnet build Wcmux.sln --configuration Debug 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Build failed!" -ForegroundColor Red
            $buildOutput | Write-Host
            exit 1
        }
        Write-Host "Build succeeded." -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

# ---------------------------------------------------------------
# Prerequisite checks
# ---------------------------------------------------------------
Write-TestHeader "Prerequisites"

# Check for vim
$vimCmd = $null
if (Test-CommandExists "vim") { $vimCmd = "vim" }
elseif (Test-CommandExists "nvim") { $vimCmd = "nvim" }

if ($vimCmd) {
    Write-TestResult "vim available" "PASS" "Found: $vimCmd"
} else {
    Write-TestResult "vim available" "SKIP" "Neither vim nor nvim found on PATH"
}

# Check for fzf
$hasFzf = Test-CommandExists "fzf"
if ($hasFzf) {
    Write-TestResult "fzf available" "PASS"
} else {
    Write-TestResult "fzf available" "SKIP" "fzf not found; will use fallback selector"
}

# ---------------------------------------------------------------
# Test 1: vim launch, input, resize, and exit
# ---------------------------------------------------------------
Write-TestHeader "vim Launch, Input, Resize, Exit"

if ($vimCmd) {
    Write-Host @"

  MANUAL VERIFICATION STEPS:
  1. In the wcmux terminal, type: $vimCmd
  2. Press 'i' to enter insert mode
  3. Type some text (e.g., "Hello from wcmux smoke test")
  4. Press Escape to return to normal mode
  5. Resize the wcmux window aggressively (drag corners rapidly)
  6. Verify the vim display redraws correctly after resize
  7. Type ':q!' and press Enter to exit vim
  8. Verify the terminal returns to the shell prompt cleanly

"@ -ForegroundColor Gray

    Write-TestResult "vim alternate screen + input + resize" "MANUAL" "Follow steps above in wcmux"
} else {
    Write-TestResult "vim alternate screen + input + resize" "SKIP" "vim not available"
}

# ---------------------------------------------------------------
# Test 2: fzf or alternate-screen selector
# ---------------------------------------------------------------
Write-TestHeader "Alternate-Screen Selector (fzf)"

if ($hasFzf) {
    Write-Host @"

  MANUAL VERIFICATION STEPS:
  1. In the wcmux terminal, run: Get-ChildItem | fzf
  2. Use arrow keys to navigate the list
  3. Type to filter entries
  4. Resize the window while fzf is running
  5. Press Enter to select an item (or Escape to cancel)
  6. Verify the terminal returns to the shell cleanly
  7. Verify the selected item is printed to stdout

"@ -ForegroundColor Gray

    Write-TestResult "fzf alternate screen + keyboard input" "MANUAL" "Follow steps above in wcmux"
} else {
    Write-Host @"

  FALLBACK MANUAL VERIFICATION:
  1. In the wcmux terminal, run: Select-String -Path *.* -Pattern "test" | Out-Host -Paging
  2. Use space/enter to page through output
  3. Press 'q' to quit
  4. Verify the terminal remains usable after exit

"@ -ForegroundColor Gray

    Write-TestResult "alternate screen selector" "MANUAL" "Using fallback (fzf not available)"
}

# ---------------------------------------------------------------
# Test 3: Paste input
# ---------------------------------------------------------------
Write-TestHeader "Paste Input"

Write-Host @"

  MANUAL VERIFICATION STEPS:
  1. Copy a multi-line text block to the clipboard, e.g.:
     echo "line 1"
     echo "line 2"
     echo "line 3"
  2. In the wcmux terminal, press Ctrl+V (or right-click) to paste
  3. Verify all lines appear correctly in the terminal
  4. Verify the commands execute when Enter is pressed
  5. Try pasting while vim is open (in insert mode)
  6. Verify pasted text appears in the vim buffer

"@ -ForegroundColor Gray

Write-TestResult "paste input" "MANUAL" "Follow steps above in wcmux"

# ---------------------------------------------------------------
# Test 4: Aggressive resize
# ---------------------------------------------------------------
Write-TestHeader "Aggressive Resize During Live Session"

Write-Host @"

  MANUAL VERIFICATION STEPS:
  1. Start a long-running command in wcmux: ping localhost -t
     (or on PowerShell: while(`$true) { Get-Date; Start-Sleep 1 })
  2. Rapidly resize the wcmux window by dragging corners and edges
  3. Try making the window very small, then very large
  4. Try double-clicking the title bar to maximize/restore
  5. Verify:
     - No crashes or hangs
     - Output continues to flow
     - Terminal content redraws correctly after resize stops
     - No visual corruption or misaligned text
  6. Press Ctrl+C to stop the command
  7. Verify the shell prompt returns and is usable

"@ -ForegroundColor Gray

Write-TestResult "aggressive resize" "MANUAL" "Follow steps above in wcmux"

# ---------------------------------------------------------------
# Test 5: Session exit behavior
# ---------------------------------------------------------------
Write-TestHeader "Session Exit Behavior"

Write-Host @"

  MANUAL VERIFICATION STEPS:
  1. In the wcmux terminal, type: exit
  2. Verify the session shows [Session ended] indicator
  3. Verify no crash or hang occurs
  4. Close and reopen wcmux to verify it starts cleanly again

"@ -ForegroundColor Gray

Write-TestResult "session exit behavior" "MANUAL" "Follow steps above in wcmux"

# ---------------------------------------------------------------
# Summary
# ---------------------------------------------------------------
Write-Host ""
Write-Host "====================================" -ForegroundColor White
Write-Host "  SMOKE TEST SUMMARY" -ForegroundColor White
Write-Host "====================================" -ForegroundColor White
Write-Host "  Passed:  $script:TestsPassed" -ForegroundColor Green
Write-Host "  Failed:  $script:TestsFailed" -ForegroundColor Red
Write-Host "  Skipped: $script:TestsSkipped" -ForegroundColor Yellow
Write-Host "  Manual:  5 scenarios require human observation" -ForegroundColor Magenta
Write-Host ""

if ($script:TestsFailed -gt 0) {
    Write-Host "Some prerequisite checks failed." -ForegroundColor Red
    exit 1
}

Write-Host "All prerequisite checks passed. Run manual scenarios in wcmux." -ForegroundColor Green
exit 0
