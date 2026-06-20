# 17 - Event Setup Workflow

This document describes the intended MVP setup workflow for a TempleOSRS-backed event.

## Source Of Truth Boundaries

SwedesEventPlanner is the source of truth for:

```text
event signups
local players
alt/account linking
local event teams before Temple export
event participation
board rules
scoring eligibility
finalized roster/team export to TempleOSRS
```

TempleOSRS is the source of truth for:

```text
XP/KC competition gains
Temple-returned per-player gains
Temple-returned team totals when the Temple competition is team-based
```

The app should not infer, repair, or smooth XP/KC gains locally.

## MVP Workflow

1. Admin creates the TempleOSRS competition.
2. Admin creates the event in SwedesEventPlanner and links the Temple competition ID/key/config as needed.
3. Admin imports Google Forms CSV as event signups.
4. App matches or creates local players and creates event participants.
5. Admin reviews unmatched names/alts and fixes player links.
6. Admin drafts/assigns teams in SwedesEventPlanner if applicable.
7. Admin locks/confirms roster and teams.
8. App pushes the finalized roster to the linked Temple competition:
   - participants for non-team Temple competitions
   - team assignments/structure for team Temple competitions
9. App validates Temple membership/team assignment against the expected local roster.
10. App syncs Temple results.
11. XP/KC tiles score from cached Temple values.

## CSV Event Signup Import

CSV import is part of MVP, but it is an event signup import, not a general global player import.

The import should create or update event-scoped signup/participant data and match to local players where possible.

Google Forms/signup-specific fields belong on event signup or event participant records, not globally on `players`.

Examples:

```text
availability
daily hours
preferred content
notes
team preference
preferred role
signup timestamp
raw form row metadata
```

The global `players` table should keep stable player identity fields only, such as display name and primary RuneScape name.

## Team Draft And Locking

For MVP, admins can manually draft/assign teams in SwedesEventPlanner.

A future team draft feature can support dedicated captains, configurable draft order, and snake-order drafts.

Do not export to TempleOSRS until the roster and teams are confirmed. Export should be treated as an intentional admin action with a preview/dry-run summary.

## Temple Export

After roster/team lock, the app should push the finalized SwedesEventPlanner roster into the linked Temple competition.

For a non-team Temple competition:

```text
export participant RuneScape names
```

For a team Temple competition:

```text
export participant RuneScape names with team assignments
```

Store the Temple competition ID in the database. Store any Temple competition key/secret outside committed config, such as an environment variable, deployment secret file, or secret reference.

Do not store Temple keys in source control, `.env.example`, seed files, raw logs, or public/admin JSON responses.

Export attempts should be logged with:

```text
event
external competition
triggered by
requested time
started/completed time
status
participants intended
participants added
participants removed, when removal is explicitly requested
team mappings intended
validation result
safe error message
safe metadata
```

## Temple Validation

After export, validate the linked Temple competition by reading the cached/remote Temple competition information and comparing it to the local expected roster.

Admin/testing views should surface:

```text
missing Temple participants
unexpected Temple participants
Temple team mismatches
local participants missing teams
unmatched Temple rows
ignored Temple rows
last successful export
last failed export
last successful sync
last failed sync
```

If Temple roster/team assignment is wrong:

```text
fix local roster/team assignment in SwedesEventPlanner when local data is wrong
re-export/update Temple
fix Temple directly only when Temple itself has stale or incorrect state
sync again after Temple is corrected
```

## XP/KC Team Scoring

For MVP XP/KC team tiles:

```text
if linked Temple competition is team-based:
  use cached Temple-returned team totals as the primary scoring input

if linked Temple competition is not team-based:
  use cached Temple-returned per-player gains grouped by local SwedesEventPlanner event teams
```

Always cache per-player gains for audit/debugging.

Always cache Temple team members/totals when Temple returns them.

Surface mismatches between Temple teams and local SwedesEventPlanner teams in admin/testing views.

## Temple Endpoint Caveat

The official TempleOSRS docs show create, add participant, and remove participant endpoints. They do not currently document a full "replace competition roster/team assignment" endpoint.

Before automating destructive roster reconciliation, verify the Temple add/remove behavior against a test competition. In particular, verify whether adding an already-present participant with a `teams` mapping updates that participant's team assignment.
