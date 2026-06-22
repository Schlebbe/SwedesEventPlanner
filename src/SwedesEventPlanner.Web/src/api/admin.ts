export type CsvSignupImportResponse = {
  eventSlug: string
  rowsRead: number
  signupsCreated: number
  signupsUpdated: number
  playersCreated: number
  participantsCreated: number
  participantsUpdated: number
  invalidRows: number
  issues: CsvSignupImportRowIssue[]
}

export type CsvSignupImportRowIssue = {
  rowNumber: number
  reason: string
}

export type AdminEventSetupSummary = {
  id: number
  slug: string
  name: string
  status: string
  eventType: string
  startsAt: string
  endsAt: string | null
  timeZone: string
}

export type AdminEventSignupListResponse = {
  event: AdminEventSetupSummary
  signups: AdminEventSignup[]
}

export type AdminEventSignup = {
  id: number
  playerId: number | null
  runeScapeName: string
  displayName: string | null
  availabilityText: string | null
  dailyHours: number | null
  preferredContent: string | null
  teamPreference: string | null
  notes: string | null
  status: string
  sourceSystem: string
  submittedAt: string | null
  importedAt: string
}

export type AdminEventParticipantListResponse = {
  event: AdminEventSetupSummary
  teams: AdminEventTeam[]
  participants: AdminEventParticipant[]
  unassignedCount: number
}

export type AdminEventTeam = {
  id: number
  name: string
  participantCount: number
}

export type AdminEventParticipant = {
  id: number
  playerId: number
  displayName: string
  runeScapeName: string
  teamId: number | null
  teamName: string | null
  status: string
  joinedAt: string
  isUnassigned: boolean
}

export type AdminExternalCompetition = {
  id: number
  provider: string
  externalId: string
  name: string
  metricType: string
  metricKey: string
  competitionMode: string
  status: string
  lastSyncedAt: string | null
  lastSuccessfulSyncAt: string | null
  lastSyncStatus: string | null
  lastSyncError: string | null
}

export type AdminExternalCompetitionListResponse = {
  event: AdminEventSetupSummary
  competitions: AdminExternalCompetition[]
}

export type AdminExternalCompetitionSyncRun = {
  id: number
  externalCompetitionId: number
  status: string
  triggerType: string
  requestedAt: string | null
  startedAt: string
  completedAt: string | null
  rowsRead: number | null
  rowsChanged: number | null
  errorMessage: string | null
}

export type AdminExternalCompetitionSyncRunListResponse = {
  runs: AdminExternalCompetitionSyncRun[]
}

export type AdminExternalCompetitionPlayerMetric = {
  id: number
  runeScapeName: string
  playerId: number | null
  localPlayerName: string | null
  metricType: string
  metricKey: string
  startValue: number | null
  currentValue: number | null
  gainedValue: number
  rank: number | null
  lastSyncedAt: string
}

export type AdminExternalCompetitionPlayerMetricListResponse = {
  metrics: AdminExternalCompetitionPlayerMetric[]
}

export type AdminExternalCompetitionTeamMetric = {
  id: number
  templeTeamKey: string
  teamName: string
  localTeamId: number | null
  localTeamName: string | null
  metricType: string
  metricKey: string
  startValue: number | null
  currentValue: number | null
  gainedValue: number
  rank: number | null
  mvpRuneScapeName: string | null
  members: string[]
  lastSyncedAt: string
  hasLocalTeamMismatch: boolean
}

export type AdminExternalCompetitionTeamMetricListResponse = {
  metrics: AdminExternalCompetitionTeamMetric[]
}

export type AdminExternalCompetitionUnmatchedIdentity = {
  id: number
  runeScapeName: string
  displayName: string
  firstSeenAt: string
  lastSeenAt: string
}

export type AdminExternalCompetitionUnmatchedIdentityListResponse = {
  identities: AdminExternalCompetitionUnmatchedIdentity[]
}

export async function importCsvSignups(
  eventSlug: string,
  csvText: string,
  adminToken: string,
): Promise<CsvSignupImportResponse> {
  return fetchAdminJson<CsvSignupImportResponse>(
    `/api/admin/events/${eventSlug}/signups/import-csv`,
    adminToken,
    {
      method: "POST",
      body: JSON.stringify({ csvText }),
    },
  )
}

export async function listAdminSignups(
  eventSlug: string,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminEventSignupListResponse> {
  return fetchAdminJson<AdminEventSignupListResponse>(
    `/api/admin/events/${eventSlug}/signups`,
    adminToken,
    { signal },
  )
}

export async function listAdminParticipants(
  eventSlug: string,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminEventParticipantListResponse> {
  return fetchAdminJson<AdminEventParticipantListResponse>(
    `/api/admin/events/${eventSlug}/participants`,
    adminToken,
    { signal },
  )
}

export async function createEventTeam(
  eventSlug: string,
  name: string,
  adminToken: string,
): Promise<AdminEventTeam> {
  return fetchAdminJson<AdminEventTeam>(
    `/api/admin/events/${eventSlug}/teams`,
    adminToken,
    {
      method: "POST",
      body: JSON.stringify({ name }),
    },
  )
}

export async function assignParticipantTeam(
  eventSlug: string,
  participantId: number,
  teamId: number | null,
  adminToken: string,
): Promise<AdminEventParticipant> {
  return fetchAdminJson<AdminEventParticipant>(
    `/api/admin/events/${eventSlug}/participants/${participantId}/assign-team`,
    adminToken,
    {
      method: "POST",
      body: JSON.stringify({ teamId }),
    },
  )
}

export async function linkTempleCompetition(
  eventSlug: string,
  externalId: string,
  adminToken: string,
): Promise<AdminExternalCompetition> {
  return fetchAdminJson<AdminExternalCompetition>(
    `/api/admin/events/${eventSlug}/external-competitions/templeosrs`,
    adminToken,
    {
      method: "POST",
      body: JSON.stringify({
        externalId,
        metricType: "xp",
        metricKey: "overall",
      }),
    },
  )
}

export async function listExternalCompetitions(
  eventSlug: string,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminExternalCompetitionListResponse> {
  return fetchAdminJson<AdminExternalCompetitionListResponse>(
    `/api/admin/events/${eventSlug}/external-competitions`,
    adminToken,
    { signal },
  )
}

export async function syncExternalCompetition(
  eventSlug: string,
  externalCompetitionId: number,
  adminToken: string,
): Promise<AdminExternalCompetitionSyncRun> {
  return fetchAdminJson<AdminExternalCompetitionSyncRun>(
    `/api/admin/events/${eventSlug}/external-competitions/${externalCompetitionId}/sync`,
    adminToken,
    { method: "POST" },
  )
}

export async function listExternalCompetitionSyncRuns(
  externalCompetitionId: number,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminExternalCompetitionSyncRunListResponse> {
  return fetchAdminJson<AdminExternalCompetitionSyncRunListResponse>(
    `/api/admin/external-competitions/${externalCompetitionId}/sync-runs`,
    adminToken,
    { signal },
  )
}

export async function listExternalCompetitionPlayerMetrics(
  externalCompetitionId: number,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminExternalCompetitionPlayerMetricListResponse> {
  return fetchAdminJson<AdminExternalCompetitionPlayerMetricListResponse>(
    `/api/admin/external-competitions/${externalCompetitionId}/player-metrics`,
    adminToken,
    { signal },
  )
}

export async function listExternalCompetitionTeamMetrics(
  externalCompetitionId: number,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminExternalCompetitionTeamMetricListResponse> {
  return fetchAdminJson<AdminExternalCompetitionTeamMetricListResponse>(
    `/api/admin/external-competitions/${externalCompetitionId}/team-metrics`,
    adminToken,
    { signal },
  )
}

export async function listExternalCompetitionUnmatchedIdentities(
  externalCompetitionId: number,
  adminToken: string,
  signal?: AbortSignal,
): Promise<AdminExternalCompetitionUnmatchedIdentityListResponse> {
  return fetchAdminJson<AdminExternalCompetitionUnmatchedIdentityListResponse>(
    `/api/admin/external-competitions/${externalCompetitionId}/unmatched-identities`,
    adminToken,
    { signal },
  )
}

async function fetchAdminJson<T>(
  url: string,
  adminToken: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers)
  headers.set("Content-Type", "application/json")
  headers.set("X-Admin-Token", adminToken)

  const response = await fetch(url, {
    ...init,
    headers,
  })

  if (!response.ok) {
    const problem = await readProblem(response)
    throw new Error(problem ?? `${url} request failed with ${response.status}`)
  }

  return (await response.json()) as T
}

async function readProblem(response: Response) {
  try {
    const body = (await response.json()) as { title?: string; detail?: string }
    return body.title ?? body.detail
  } catch {
    return null
  }
}
