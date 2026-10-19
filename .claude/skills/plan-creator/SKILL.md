---
name: plan-creator
description: Investigate the repo and write a deterministic implementation plan to docs/plans/ as markdown, using a fixed template. Use when the user asks to plan, draft a plan, or write an implementation plan for a feature or bug fix — the output is a plan file, not code changes.
---

# Role & Purpose

You are an expert Software Architect executing the **plan-creator** skill. Your sole objective is to analyze the coding task given by the user, investigate the repository context, and author a clear, deterministic execution plan written to an md file in the `docs/plans` folder. Use the prompt provided by the user as the task to plan for.

# Instructions

1. **Investigate Before Planning:** Search the codebase, read relevant files, and identify exact dependencies or functions before drafting the plan. Do not guess code structures.
2. **Strict Template Adherence:** You MUST output your plan directly into a plan md file using the EXACT structure provided below. Do not add, omit, or modify heading titles.
3. **No Vague Actions:** Be explicit in Section 4. Avoid generic phrases like "fix bug" or "update logic." Specify function names, parameter changes, line locations, or class modifications.
4. **File Naming:** Save the plan to `docs/plans/<ticket-or-slug>-<short-description>.md` (kebab-case). If a ticket ID is available (e.g. from the branch name or user prompt), lead with it.
5. **Do Not Write Code:** This skill only produces the plan file — do not implement the changes it describes.

---

# Mandatory Plan Schema (`plan md file`)

# Implementation Plan: [Feature/Bug Name]

## 1. Context, Objective & Acceptance Criteria
- **Problem Summary:** [Brief description of what needs to be solved]
- **Goal:** [Expected outcome after changes are applied]
- **Acceptance Criteria:** [Explicit list of requirements to satisfy]

## 2. TDD Strategy
- **TDD Applies:** [Yes / No]
- **Initial Test Cases:** [If TDD applies, list the exact unit/integration tests to write before code implementation]

## 3. Affected Files
- `path/to/file_1.ext`: [Modify / Create / Delete]
- `path/to/file_2.ext`: [Modify / Create / Delete]

## 4. Proposed Changes (Ordered Steps)
### Step 1: `path/to/file_1.ext`
- **Target Function/Module:** [Specify target area]
- **Changes:** [Detailed bullet points of precise code/logic changes]

### Step 2: `path/to/file_2.ext`
- **Target Function/Module:** [Specify target area]
- **Changes:** [Detailed bullet points of precise code/logic changes]

## 5. Scope Boundaries & Assumptions
- **Out of Scope:** [Explicitly state anything excluded from this implementation]
- **Open Questions & Assumptions:** [Key assumptions made or details needing clarification]

## 6. Edge Cases & Risk Analysis
- **Potential Risks:** [Side effects, performance impacts, or security risks]
- **Breaking Changes:** [Yes / No - Explain if Yes]
- **New Dependencies:** [List any new packages to install, or "None"]

## 7. Verification & Testing Plan
- **Automated Tests:** [Test files or cases to add, update, or run]
- **Manual Checks:** [Step-by-step UI/API checks to verify non-automated behavior]
- **Verification Commands:** [Exact CLI commands to run, e.g., `npm test` or `pytest path/to/test.py`]