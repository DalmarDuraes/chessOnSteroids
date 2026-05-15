# Implement Autonomously

This command wraps `/implement-plan` with autonomous behavior. Start by reading `.claude/commands/Development/implement-plan.md` for the base implementation workflow.

---

## Modifications to implement-plan

Apply these modifications when following implement-plan:

### 1. Fully Autonomous Mode

- **Do not ask clarifying questions** during implementation
- **Do not pause for human verification** between phases - implement all phases consecutively
- When you encounter implementation trade-offs or mismatches, analyze them, make a decision, and document your reasoning
- If the plan is ambiguous, interpret it reasonably and document your interpretation

### 2. Review After Implementation (Delegated)

After all phases are complete, run the review process:

Read and follow `.claude/commands/Development/review-current-branch.md` with these settings:
- **Skip PR Comments** (no PR exists yet)
- **Use defaults** for Logical, AI Checklist and Design review
- **Autonomous mode** - fix issues automatically

Iterate on the implementation based on review findings. Make decisions autonomously.

### 3. Present Results

After review completes, present:

```
## Implementation Complete

**Plan:** `[path to plan]`
**Branch:** `[current branch]`

### Summary
[2-3 sentence overview]

### Files Changed
- `path/to/file.ext` - [brief description]
- ...

### Autonomous Decisions

I made these decisions during implementation:

1. **[Decision]:** [What you chose]
   - Plan said: [what the plan specified, or "not specified"]
   - Rationale: [why this choice]

### Review Summary
- Rounds completed: [N]
- Issues fixed: [count by category]

### Manual Verification Needed
[Items from the plan's success criteria that require manual testing]

### Confirmation
- [ ] Do the autonomous decisions align with your intent?
- [ ] Ready to proceed with manual testing?

Let me know if any changes need adjustment.
```

---

## Context Management

- **10-tool limit:** Delegate after ~10 tool calls in main context, except for the very start.
- **Parallel execution:** Launch independent subagents together
- **Phase isolation:** Each phase should be implemented by a separate subagent

---

## Error Handling

If implementation encounters issues:

1. **Compilation errors:** Fix autonomously, document the fix
2. **Plan ambiguity:** Make reasonable interpretation, document it
3. **Missing dependencies:** Check if the plan mentions them; if not, document and proceed with best guess
4. **Conflicting requirements:** Choose the safer option, document the conflict

---

## Git Practices

- **Commit after each phase** with format: `[Phase N] Brief description`
- **Do not push** until review is complete and user confirms
