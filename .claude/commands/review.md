---
description: Strict read-only code review (security, data integrity, error handling). Fixes nothing.
argument-hint: "[path | changes]  (default: whole repo)"
---

# /review – strict read-only review

Scope: `$ARGUMENTS`
- empty → the whole repo (api/, notifications/, app/, pipelines/)
- a path → only that folder/file
- `changes` → only files in `git diff` and `git diff --staged`

## Hard rules
- **Read-only.** Do not edit, create, delete or format files. Do not install packages, run migrations, start servers, or run builds/tests (that is /verify's job). Read-only commands are allowed: reading files, searching, `git diff`, `git log`, `git status`.
- **Evidence only.** Every finding must point to a real `file:line` you read. Never invent code, lines or test names. If you are not sure, say "suspected" and explain what would confirm it.
- **Not checked is not clean.** If a category could not be checked (code missing, out of scope), mark it NOT CHECKED. Never count it as clean.
- Be strict: report anything that a careful senior reviewer would block or question. Do not pad with style nitpicks.

## What to look for

1. **Permission leaks to employees**
   - An endpoint that returns or changes an expense, receipt or history without checking the caller owns it or is that employee's manager.
   - An approve/reject action allowed for a non-manager or for a manager of another team.
   - IDs taken from the request body/route and trusted instead of the JWT user.
   - SAS URLs that are too broad (container-level, write+read, long expiry).
   - The Node `/notify` endpoint without an internal key check.
   - Role checks done only in the app UI.
2. **Duplicate records**
   - No idempotency on submit (double tap/retry creates two expenses).
   - Push token saved again on every login with no unique constraint.
   - A workflow started twice for the same expense.
   - History rows written twice.
   - Missing unique indexes where the domain needs uniqueness.
3. **Failures stored as valid data**
   - Exceptions swallowed and replaced by defaults (0, empty list, "Approved", null receipt) that are then saved or returned as if real.
   - A failed upload still creates the expense.
   - A failed push marked as sent.
   - A report returning 0 totals on error.
   - A status changed before the action that justifies it has succeeded.
4. **Unvalidated external output**
   - Client input reaching Domain/DB without validation: amount ≤ 0, missing rejection reason, invalid category/date, oversized strings.
   - Expo push tickets/receipts not checked (DeviceNotRegistered ignored).
   - Elsa workflow results or bookmark payloads trusted blindly.
   - Uploaded files: type and size not checked.
   - JSON from other services deserialized without checks.
5. **Injection**
   - Raw SQL built with string concatenation/interpolation (`FromSqlRaw`, `ExecuteSqlRaw` with user input).
   - Blob names built from user input (path traversal, overwriting another user's receipt).
   - User text placed in notification payloads, headers or logs without limits/encoding.
   - Rendering user HTML on web.
6. **Mocks presented as real**
   - Hardcoded or fake data, stub services, `TODO: replace`, always-true auth, fake tokens, or simulated push "success" in **non-test** code.
   - Any UI or log that shows success while nothing real happened.
   - Test doubles registered in production dependency injection.
7. **500 instead of a clean 4xx**
   - Requests that throw (null reference, `First()` on empty, invalid enum/GUID parse, DB unique-constraint violation, workflow not found, invalid state transition) instead of returning 400 / 401 / 403 / 404 / 409 / 422.
   - Missing global exception handler or ProblemDetails mapping.
   - Validation done by catching exceptions.
   - Node routes without error handling (unhandled promise rejection).

## Severity
- **Critical:** a data leak, privilege escalation, or anyone able to approve/forge expenses; secrets exposed.
- **High:** corrupted or duplicated business data, or a failure saved as success.
- **Medium:** a crash (500) on bad input, missing validation with limited impact, or a mock that could reach the demo.
- **Low:** hardening or defense-in-depth gaps.

## Finding format (one block per finding, sorted by severity)

**[Severity] Short title**
- **Where:** `path/to/file.ext:line` (several if needed)
- **Category:** one of the 7 above
- **Scenario:** a concrete step-by-step example of how it goes wrong (who does what, what request, what happens)
- **Rule broken:** quote the CLAUDE.md rule, or write "no explicit rule in CLAUDE.md — suggest adding: <one-line rule>"
- **Fix direction:** one line (do not write the fix)

If there are no findings in a category, say so in the coverage table only.

## Coverage table (always at the end)

| # | Category | Files/areas reviewed | Findings (C/H/M/L) | Status |
|---|---|---|---|---|

Status = `Checked – findings`, `Checked – clean`, or `NOT CHECKED – <reason>`.

## Verdict (last line, exactly one of these)
- `VERDICT: BLOCK – <n> critical/high finding(s)`: at least one Critical or High.
- `VERDICT: CHANGES REQUESTED – <n> medium finding(s)`: Medium findings but no Critical/High.
- `VERDICT: INCOMPLETE – <n> category(ies) not checked`: no Critical/High, but a category was NOT CHECKED.
- `VERDICT: APPROVE`: only if all 7 categories were checked and there are no findings above Low.
