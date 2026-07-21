---
name: gitnexus-intelligence
description: Preferred first step for code search, exploration, debugging, or refactoring when GitNexus MCP is available. Prefer graph tools; gracefully fall back to text search if MCP is unavailable or the repo is not indexed.
---

# GitNexus — Code Intelligence (Graceful)

Use GitNexus MCP tools to understand code, assess impact, and navigate safely **when the graph is available**. This skill does **not** hard-block work if GitNexus is missing.

> If any GitNexus tool warns the index is stale, prefer `npx gitnexus analyze` when the user can run it — then continue.

## Availability protocol (do this first)

1. **Probe once** (e.g. `list_repos` / `mcp_gitnexus_list_repos` or open `gitnexus://repo/.../context`).
2. **Classify mode:**

| Mode | Condition | Behavior |
| ---- | --------- | -------- |
| **GRAPH** | MCP responds and current project (or known alias) is indexed | Prefer GitNexus for search, impact, rename |
| **DEGRADED** | MCP missing, timeout, 0 repos, or project not indexed | Use normal read/grep/glob tools; **one short warn** to the user; continue work |
| **STALE** | Graph works but index is stale | Warn; offer `npx gitnexus analyze`; continue with graph + extra caution, or text search |

3. **Never STOP the entire task** solely because GitNexus is unavailable. Greenfield EAs, small edits, and log analysis must still proceed in DEGRADED mode.

```
❌ WRONG: "GitNexus not responding — I cannot proceed until you fix MCP."
✅ RIGHT: "GitNexus unavailable — continuing with file/text search. Impact analysis will be manual."
```

## When in GRAPH mode

- **Prefer impact analysis before editing** non-trivial symbols: `gitnexus_impact({target, direction: "upstream"})` and report blast radius.
- **Prefer `gitnexus_detect_changes()`** before committing when the tool exists.
- **Warn** on HIGH/CRITICAL impact before large edits.
- Explore with `gitnexus_query` / `gitnexus_context` instead of blind grep for architecture questions.
- Renames: prefer `gitnexus_rename` with `dry_run: true` first when available.

## When in DEGRADED mode

- Use the host's normal tools: read, grep, glob, terminal search.
- For edits: manual impact check — find callers/includes by name, list `#include` / `using`, note shared modules (e.g. RWCommon).
- For renames: careful multi-file search/replace; list files touched.
- Do **not** invent fake GitNexus results.
- Optionally suggest: enable MCP from `.agents/mcp_config.json` and run `npx gitnexus analyze`.

## When Debugging

**GRAPH:**

1. `gitnexus_query({query: "<error or symptom>"})`
2. `gitnexus_context({name: "<suspect function>"})`
3. Process resources if useful
4. Regressions: `gitnexus_detect_changes` when available

**DEGRADED:**

1. Grep error strings / function names in journals and source
2. Read suspect modules and call sites
3. Diff against main/base branch if needed

## When Refactoring

| Action | GRAPH | DEGRADED |
| ------ | ----- | -------- |
| Rename | `gitnexus_rename` dry-run then apply | Grep + careful replace; confirm with user if wide |
| Extract/split | `context` + `impact` first | Read callers manually via search |
| Pre-finish | `detect_changes` | `git status` / `git diff` review |

## Never do (both modes)

- NEVER ignore HIGH/CRITICAL risk **when impact was actually computed**.
- NEVER claim "zero blast radius" without evidence (graph or manual search).
- NEVER block trivial fixes (typos, comments, single-file pure locals) waiting for GitNexus.

## Tools quick reference (GRAPH)

| Tool | When to use |
|------|-------------|
| `query` | Find code by concept |
| `context` | 360° view of one symbol |
| `impact` | Blast radius before editing |
| `detect_changes` | Pre-commit scope check |
| `rename` | Safe multi-file rename |
| `cypher` | Custom graph queries |

## Impact risk levels (when impact ran)

| Depth | Meaning | Action |
|-------|---------|--------|
| d=1 | WILL BREAK — direct callers | MUST update |
| d=2 | LIKELY AFFECTED | Should test |
| d=3 | MAY NEED TESTING | Test if critical path |

## Self-check before finishing code edits

1. Impact considered (graph **or** manual caller search)
2. No known HIGH/CRITICAL risks left unaddressed
3. Changes match expected scope (`detect_changes` or `git diff`)
4. Direct dependents updated when signatures change

## Keeping the index fresh (optional, GRAPH projects)

After substantial commits:

```bash
npx gitnexus analyze
# preserve embeddings if used:
npx gitnexus analyze --embeddings
```

Check `.gitnexus/meta.json` → `stats.embeddings` before dropping embeddings.

## Related skills

| Task | Skill |
|------|-------|
| Architecture / how X works | `.agents/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius | `.agents/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Debugging | `.agents/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Refactoring | `.agents/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools reference | `.agents/skills/gitnexus/gitnexus-guide/SKILL.md` |
| CLI index/status | `.agents/skills/gitnexus/gitnexus-cli/SKILL.md` |
