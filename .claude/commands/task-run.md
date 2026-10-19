---
description: Fetch a Jira ticket and hand it straight to the task-runner skill for implementation
argument-hint: <TICKET-ID>
---

Ticket ID: $ARGUMENTS

1. Launch the `jira-assistant-ai-runway` agent to fetch the full details of ticket `$ARGUMENTS` (summary, description, acceptance criteria, comments). Read-only — do not update the ticket.
2. If the agent reports the Jira integration is unavailable, stop and tell the user instead of guessing at ticket content.
3. Once you have the ticket details, invoke the `task-runner` skill, passing the ticket ID and its full description/acceptance criteria as the task to implement. Follow task-runner's normal plan → confirm → implement → test → review workflow — do not skip the plan-confirmation step just because the ticket came from Jira.
