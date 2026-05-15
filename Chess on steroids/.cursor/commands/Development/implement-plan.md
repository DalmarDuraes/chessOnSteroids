# Implement Plan

You are tasked with implementing an approved technical plan from `Assets/Documentation/WIP/[feature-name]/plans/`. These plans contain phases with specific changes and success criteria.

## Getting Started

When given a plan path:
- Read the plan completely and check for any existing checkmarks (- [x])
- Read the research documents in the same feature folder (e.g., `WIP/[feature]/research/`)
- Read the original ticket and all files mentioned in the plan
- **Read files fully** - never use limit/offset parameters, you need complete context
- Think deeply about how the pieces fit together
- Create a todo list to track your progress
- Select expert agents (see below)
- Start implementing if you understand what needs to be done

If no plan path provided, ask for one.

## Select Expert Agents

Before implementing, launch a subagent to identify relevant domain expert agents:

```
Read the implementation plan and research documents. Then scan `.claude/agents/` to find domain expert agents relevant to this implementation.

## Plan
[Include the plan content or path]

## Instructions
1. Read the plan to understand what systems/domains are involved
2. Scan `.claude/agents/` subdirectories (skip Development/ folder - those are utility agents)
3. For each domain expert, check if it's relevant to this implementation
4. Return recommended expert agent types (subagent_type values) to use

Focus on domain experts in:
- Character/, Combat/, Data Management/, High-Level Multiplayer/
- Hologram Architecture/, Interaction System/, Logging/, NPC/
- Plugins/, Simulation/, UI/

Return format:
- Agent type: `[subagent_type]` - Why it's relevant
- ...

If no domain experts are relevant, return "general-purpose" as the default.
```

Store these for use during implementation.

## Implementation Philosophy

Plans are carefully designed, but reality can be messy. Your job is to:
- Follow the plan's intent while adapting to what you find
- Implement each phase fully before moving to the next
- Verify your work makes sense in the broader codebase context
- Update checkboxes in the plan as you complete sections

When things don't match the plan exactly, think about why and communicate clearly. The plan is your guide, but your judgment matters too.

If you encounter a mismatch:
- STOP and think deeply about why the plan can't be followed
- Present the issue clearly:
  ```
  Issue in Phase [N]:
  Expected: [what the plan says]
  Found: [actual situation]
  Why this matters: [explanation]

  How should I proceed?
  ```

## Delegating Implementation

For each phase, delegate implementation work to expert subagents based on what that phase touches. Use the agent types identified in the "Select Expert Agents" step above. If no domain expert matches this phase, use `general-purpose`.

**Subagent prompts should include:**
```
Implement Phase [N]: [Phase Name]

## Context
[Relevant plan section]

## Research Context
[Relevant research that informed this phase]

## Files to Modify
[List from plan]

## Expected Changes
[What the plan says to do]

## Verification
After making changes, verify:
- Code compiles (if applicable)
- Changes match the plan specification
- No unintended side effects

Report: files changed, what was done, any deviations from plan with reasoning.
```

## Verification Approach

After implementing a phase:
- Update your progress in both the plan and your todos
- Check off completed items in the plan file itself using Edit
- **Pause for human verification**: After completing implementation for a phase, pause and inform the human. Use this format:
  ```
  Phase [N] Implementation Complete

  Implementation is complete. You can now proceed with testing, or instruct me if there is any way I can assist with testing.
  ```

If instructed to execute multiple phases consecutively, skip the pause until the last phase. Otherwise, assume you are just doing one phase.

Do not check off items in the manual testing steps until confirmed by the user.


## If You Get Stuck

When something isn't working as expected:
- First, make sure you've read and understood all the relevant code
- Consider if the codebase has evolved since the plan was written
- Present the mismatch clearly and ask for guidance

Use sub-tasks sparingly - mainly for targeted debugging or exploring unfamiliar territory.

## Resuming Work

If the plan has existing checkmarks:
- Trust that completed work is done
- Pick up from the first unchecked item
- Verify previous work only if something seems off

Remember: You're implementing a solution, not just checking boxes. Keep the end goal in mind and maintain forward momentum.

## After Implementation Complete

Once all phases are implemented and tested, remind the user:

```
🎉 Implementation complete!

📚 **Don't forget to update documentation**: Run `/learn` to review and update the knowledge base based on what was implemented. This ensures future development benefits from the patterns and decisions made during this implementation.
```