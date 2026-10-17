---
description: Run backend tests + frontend lint and build, report results and rule coverage. Fixes nothing.
---

# /verify – read-only verification

You are running a verification pass on the ExpenseFlow repo (root: the folder containing CLAUDE.md).

## Hard rules
- **Fix nothing.** Do not edit, create, delete, format or install anything in the source tree. No `--fix`, no `npm install`, no `dotnet format`, no migrations, no git commands that change state. Build output (`bin/`, `obj/`, `dist/`) produced by the commands below is allowed.
- **Run every check, even if an earlier one fails.** Never stop early.
- **NOT RUN is never a pass.** A check that was skipped, could not start, timed out before producing a result, or whose folder/config is missing is NOT RUN (or FAILED for a timeout), with the reason.
- **Never say "all good"** (or similar: "everything passes", "all green") unless every check is PASSED.
- Report only what the command output actually shows. Do not guess numbers; write `unknown` when the output does not show them.
- Use Windows PowerShell syntax. Read the exit code with `$LASTEXITCODE` right after each command.
- Before the frontend commands, set `$env:CI = "1"` so no tool prompts or auto-configures anything.

## Checks (run in this order, each from its own folder)

| # | Check | Folder | Command | Run only if |
|---|---|---|---|---|
| 1 | Backend tests (.NET) | `api/` | `dotnet test` | `api/` contains a `.sln` or test `.csproj` |
| 2 | Backend tests (Node) | `notifications/` | `npm test` | `notifications/package.json` exists and defines a `test` script, and `node_modules/` exists |
| 3 | Frontend lint | `app/` | `npx expo lint` | `app/package.json` exists, `node_modules/` exists, and an ESLint config exists (`eslint.config.*` or `.eslintrc*`) |
| 4 | Frontend build (web) | `app/` | `npx expo export --platform web` | `app/package.json` exists and `node_modules/` exists |

If a "Run only if" condition is false → status **NOT RUN**, exit code `—`, and the reason (e.g. "folder `api/` not found", "no ESLint config – lint not set up yet", "node_modules missing – run npm install").

Status:
- **PASSED** = command ran and exit code is 0 (and, for tests, 0 failed).
- **FAILED** = command ran and exit code ≠ 0, or any test failed, or it timed out (allow up to 10 minutes per check).

Number of tests:
- .NET: add up `Passed / Failed / Skipped / Total` from every test project's summary line.
- Node: read the test runner's summary; `unknown` if not shown.
- Lint and build: `n/a`.

## Report format

### 1. Results

| Check | Command | Status | Exit code | Tests (passed / failed / skipped / total) |
|---|---|---|---|---|

Then one summary line: `X PASSED, Y FAILED, Z NOT RUN`. If Y or Z is above 0, say plainly that the verification did **not** pass.

### 2. Failures
For each FAILED check:
- **Check:** name
- **Exact error:** the relevant error lines copied verbatim from the output (failing test names + assertion messages, compiler errors with file:line, lint errors with file:line:rule). Keep it to the essential lines; do not paraphrase.
- **Likely cause (guess):** one line. Always label it as a guess.

For each NOT RUN check: one line with the reason and what would make it runnable.

### 3. Rule coverage
Read CLAUDE.md now and list every testable rule in it: business rules, data rules, security rules and the architecture rule. For example:
- managers approve or reject only their own team's expenses;
- rejection requires a reason;
- status flow Submitted → InReview → Approved/Rejected;
- Amount decimal(12,2);
- dates stored in UTC;
- JWT required on protected endpoints;
- no secrets in code or Git;
- Clean Architecture dependency direction.

Do not limit yourself to these examples: take the rules from the current CLAUDE.md.

For each rule, search the test projects (test names and test bodies) for a test that checks it.

| Rule (from CLAUDE.md) | Covering test (project › class › method) | Result in this run |
|---|---|---|

- If no test checks the rule → `not covered yet`.
- If a test exists but its check was FAILED or NOT RUN → name the test and show FAILED / NOT RUN. Never count it as covered-and-passing.
- Only name tests you actually found in the code. Never invent a test name.

After the table, add one line listing the process/working-style rules from CLAUDE.md (how to explain, PowerShell commands, ask before…) as "not testable automatically".
