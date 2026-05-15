# Interview

Interview the user to build a comprehensive technical specification. Use subagents to research the codebase and ground questions in reality.

---

## Process

### 1. Initial Setup

- Read any provided files completely
- If systems are mentioned, spawn research subagents in parallel:
  - **codebase-locator** - Find related files
  - **codebase-analyzer** - Understand current implementations

### 2. Start the Interview

```
I'll interview you to build a technical specification.

[If research done: "I found [brief relevant context]."]

NOTE: All Questions are UseAskUserQuestion
**Starting AskUserQuestion questions:**
1. What problem are you solving?
2. What does success look like?
3. What systems does this touch?
```

### 3. Interview Loop

Ask 3-5 questions per round across these areas:

**Technical Implementation**
- Integration with existing systems
- Data structures and state management
- Performance constraints
- Existing patterns to follow
- Error handling

**Edge Cases & Boundaries**
- Min/max values and limits
- Concurrent access handling
- Behavior when prerequisites aren't met
- What's explicitly out of scope

**Tradeoffs**
- What are you willing to sacrifice for simplicity?
- Alternative approaches considered
- Technical debt implications

**Validation**
- How will you know it works?
- Testing requirements (automated and manual)
- Acceptance criteria

### 4. Research During Interview

Spawn subagents when the user:
- Mentions a specific system or component
- Makes claims about how something works
- Describes integration points



Use findings to ask informed follow-ups:
```
"I found [system] uses [pattern]. Should we follow that?"
"The current implementation handles [case] by [method]. Is that acceptable?"
```

### 5. Progress Check (every 8-10 questions)

```
**Progress:**
- [Key point 1]
- [Key point 2]

**Research findings:**
- [Relevant discovery]

**Still need to cover:**
- [Remaining area]

Continue, or address something specific?
```

### 6. Completion

When the user says done:

1. Final verification research on key integration points
2. Present summary for confirmation
---

## Spec Template

```markdown
---
date: [ISO format with timezone]
author: [LLM Name]
topic: "[Feature Name]"
tags: [spec, feature-name]
status: draft
last_updated: [YYYY-MM-DD]
last_updated_by: [LLM Name]
interview_questions_asked: [count]
---

# [Feature Name] Specification

## Summary
[2-3 sentences: what it does and why]

## Problem
[What problem this solves]

## Goals
- [Primary goal]
- [Secondary goals]

## Non-Goals
- [Explicitly out of scope]

## Technical Specification

### Architecture
[High-level approach]

### Data Model
[Key data structures]

### Integration Points
- [Existing systems this connects to]
- [Relevant files: path/to/file.cs:line]

### Dependencies
- [Required systems/components]

## Requirements

### Functional
1. [Requirement with acceptance criteria]

### Non-Functional
- **Performance:** [Requirements]

## Edge Cases

| Scenario | Behavior |
|----------|----------|
| [Case] | [Behavior] |

## Decisions

| Decision | Options | Choice | Why |
|----------|---------|--------|-----|
| [Decision] | [Options] | [Choice] | [Rationale] |

## Testing

### Automated
- [Test requirements]

### Manual
- [Manual verification needed]

## Codebase References
- `path/to/file.cs:line` - [Description]

## Open Questions
[Should be minimal]
```

---

## Guidelines

1. **Research before assuming** - Verify systems exist as described
2. **No vague answers** - "It should just work" needs specifics
3. **Challenge assumptions** - Obvious cases hide complexity
4. **Ground in reality** - Questions based on actual code, not hypotheticals
5. **Spawn agents in parallel** when researching multiple systems
