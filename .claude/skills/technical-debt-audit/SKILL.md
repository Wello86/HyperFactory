---
name: technical-debt-audit
description: "Audits full-stack codebase for performance, security, and quality issues, outputting docs/tech-debt-audit.md."
---

# Technical Debt Audit Skill

## Objective
Thoroughly scan backend and frontend directories to identify actionable quality issues and save the results to `docs/tech-debt-audit.md`.

## Execution Workflow
1. Scan all core source directories across frontend and backend.
2. Search for issues across these categories:
   * **Security:** Hardcoded secrets, unvalidated inputs, SQL/XSS risks.
   * **Performance:** Unnecessary re-renders, unindexed queries, heavy synchronous loops.
   * **Correctness:** Unhandled promises, race conditions, missing null checks.
   * **Maintainability:** Dead code, duplicate logic, improper typing/coupling.
   * **Configuration:** Environment variable leaks, brittle hardcoded paths.

## Output Rules
* Write output strictly to `docs/tech-debt-audit.md`.
* Each issue MUST include:
  * **Location:** `filepath:line_number`
  * **Category:** [Security | Performance | Correctness | Maintainability | Configuration]
  * **Severity:** [High | Medium | Low]
  * **Description:** What is wrong and why it matters.
  * **Suggested Fix:** Specific step-by-step resolution.

## Output Format Template
### [Category] Issue Title
* **Location:** `src/path/file.ts:42`
* **Severity:** High / Medium / Low
* **Impact:** Brief explanation of risk.
* **Suggested Fix:** Concrete code update or refactoring step.
