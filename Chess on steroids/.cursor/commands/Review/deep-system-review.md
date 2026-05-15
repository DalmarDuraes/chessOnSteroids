# Deep System Review (Multi-Agent)

Iterative review of existing systems/files using parallel subagents across multiple dimensions.
Used for Heavy subagent reviews on plans/systems that are not a part of the current diff, and so review-current-branch wouldnt catch
---

## Step 0: Discover Expert Agents

Launch a subagent to scan `.claude/agents/` and return a summary of available domain experts:

```
Scan `.claude/agents/` directory. For each agent with a domain focus (not Development tools), return:
- Agent name (from frontmatter)
- Domain it covers
- When to use it

Skip: codebase-locator, codebase-analyzer, codebase-pattern-finder, code-simplifier, logical-reviewer,

Return a concise list I can reference when selecting experts for system review.
```

Store this list for use in Step 3.

---

## Step 1: Get Target Files

Ask the user to specify what to review:

**Q1: Review Target**
> What should I review?
> - **System** - A system/feature name (I'll find relevant files)
> - **Files** - Specific file paths (comma-separated or glob patterns)
> - **Directory** - All code files in a directory

If "System" selected, use `codebase-locator` to find relevant files and confirm the file list with the user before proceeding.

---

## Step 2: Gather Context

Based on the target files:

1. Read all target files thoroughly
2. Identify related systems and dependencies
3. Use `codebase-pattern-finder` and `codebase-locator` and  to find similar implementations for comparison
4. Note existing conventions and patterns

---

## Step 3: Configuration

Ask these questions:

**Q2: Review Dimensions**
> Which dimensions? (default: Logical + Design)
> - **Logical** - Bugs, edge cases, correctness (uses `logical-reviewer` agent)
> - **Design** - Architecture, patterns, domain correctness (uses domain expert agents)
> - **AI Checklist** - Style guide and AI checklist (uses `code-simplifier` agent)
> - **PR Comments** - Address unresolved reviewer comments (uses `pr-comment-reviewer` agent)

**Q3: Rounds**
> How many rounds? (default: 5)

**Q4: Mode** (default Autonomous)
> 1. **Autonomous** - Fix issues automatically between rounds (default)
> 2. **Approval** - Pause after each round for approval
> 3. **Report Only** - Surface issues without modifying code

---

## Step 4: Run Review Rounds

**Before starting:** Use TodoWrite to add each review round to your todo list (e.g., "Round 1", "Round 2", etc.). Mark each as in_progress when starting and completed when done.

For each round, launch parallel subagents per selected dimension:

### Logical Review (if selected)

Use `subagent_type: "logical-reviewer"` - launch 2 agents.

```
Review these files for logical correctness:

[File list with contents]

Check for:
- Bugs and edge cases
- Error handling gaps
- Null/undefined risks
- Race conditions or timing issues
- Resource leaks
- Incorrect assumptions

Report findings only. Structure by severity: CRITICAL, IMPORTANT, MINOR.
```

### Design Review (if selected)

1. Analyze which domains the files touch
2. Select 1-3 relevant expert agents from the Step 0 list
3. Launch each expert with:

```
Review these files from your domain's perspective:

[File list with contents]

Check for:
- Violations of established patterns in your domain
- Architectural concerns
- Better approaches based on existing conventions
- Misuse of domain-specific APIs or patterns

Report findings only. Structure by severity: CRITICAL, IMPORTANT, MINOR.
```

### PR Comments Review (if selected)

Use `subagent_type: "pr-comment-reviewer"` for both agents.

### AI Checklist Review (if selected)

Use `subagent_type: "code-simplifier"` - launch 2 agents.

```
Review these files against the style guide and AI checklist:

[File list with contents]

Check for violations of coding standards, naming conventions, and best practices.

Report findings by severity: CRITICAL, IMPORTANT, MINOR.
```

---

## Step 5: Process Findings

After each round:

1. Collect findings from all agents
2. Deduplicate overlapping issues
3. Categorize: critical, important, minor, nitpicky
4. **For PR Comments:** Present the resolution table from pr-comment-reviewer

**Autonomous mode:** Fix critical/important issues, skip minor/nitpicky, continue.

**Approval mode:** Present findings, wait for approval, apply approved fixes, continue.

**Report Only mode:** Present all findings without making changes.

**Optimization:** Skip files where all agents found no issues in subsequent rounds.

---

## Step 6: Stop Conditions

Stop when:
- Max rounds reached
- Only nitpicky suggestions remain
- No new issues found

---

## Step 7: Final Summary

Present:
- **Rounds Completed** - How many iterations ran
- **Issues Fixed** - Count per dimension (if not Report Only mode)
- **Remaining Items** - Minor issues not addressed
- **System Health** - Overall assessment (Healthy / Needs Attention / Significant Issues)