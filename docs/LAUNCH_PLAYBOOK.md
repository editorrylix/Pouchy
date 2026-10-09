# Pouchy launch playbook

Repeatable checklist for each release so messaging, distribution and feedback loops stay consistent.

[← Back to the README](../README.md)

## 1) Core message (use everywhere)

**Pouchy is a Windows drag-and-drop shelf for files, links, text and screenshots.**

Lead with outcomes:
- fewer window switches
- faster attachments
- smoother bug-report workflows

Use this line in README, release pages and social posts.

## 2) Release page checklist

1. Put a short demo near the top (`demo.gif` + optional clip link).
2. Include 3 quick workflows:
   - attachments from multiple folders
   - screenshot collection for bug reports
   - cross-folder file moves/copies
3. Keep download guidance explicit:
   - installer first
   - x64 vs ARM64 clearly labeled
   - portable zip option
4. Keep trust signals visible:
   - privacy/no telemetry
   - known limitations + workaround
5. End with a clear feedback prompt:
   - "What file workflow is most annoying for you right now?"

## 3) Social post loop per release

Publish at least 3 short posts:

1. **Feature post**
   - one feature
   - one GIF/video
   - one concrete user outcome
2. **Workflow post**
   - one real scenario (email, screenshots, file sorting, etc.)
   - short before/after friction statement
3. **Community post**
   - request feedback with a specific question
   - link to issues/discussions

Reuse the same core assets and wording across Reddit/X/Discord/dev communities.

## 4) Channel targeting

Prioritise communities where workflow tools are discussed:
- Windows productivity
- file management
- creators/design workflows
- developer productivity

Avoid generic promotion-only posting. Start from a workflow pain point and ask for feedback.

## 5) Feedback triage and loop-closing

For incoming requests, label and route as:
- **Quick wins**
- **High-value roadmap**
- **Won't do for now**

When shipping community-requested changes, say so in release notes:
- "Requested by users, now added."

## 6) Contribution funnel

Keep these links visible and up to date:
- Good first issues
- Bug report template
- Feature request template
- Docs for extending Pouchy (actions/themes)

## 7) Monthly growth review

Track and review monthly:
- GitHub profile/repo visits
- README → Releases click-through
- release download counts
- star growth
- issue quality (clear repro steps, useful logs, actionable requests)

Decide next month by evidence:
- repeat channels/messages that convert
- drop low-signal channels
- keep testing small messaging iterations
