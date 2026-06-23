export type EventSummary = {
  id: number
  slug: string
  name: string
  eventType: string
  status: string
  startsAt: string
  endsAt: string | null
  timeZone: string
}

export type EventListResponse = {
  events: EventSummary[]
}

export type EventBoard = {
  event: EventSummary
  board: Board
  teams: EventBoardTeam[]
  externalCompetitionFreshness: EventExternalCompetitionFreshness[]
}

export type EventTeamBoard = {
  event: EventSummary
  team: EventBoardTeam
  board: Board
  externalCompetitionFreshness: EventExternalCompetitionFreshness[]
}

export type Board = {
  id: number
  name: string
  rows: number | null
  columns: number | null
  tiles: BoardTile[]
}

export type BoardTile = {
  id: number
  title: string
  description: string | null
  positionX: number | null
  positionY: number | null
  sortOrder: number
  teamProgress: BoardTileTeamProgress[]
  tiers: BoardTileTier[]
}

export type BoardTileTeamProgress = {
  teamId: number
  teamName: string
  currentValue: number
  currentTier: number
  isCompleted: boolean
  completedAt: string | null
}

export type BoardTileTier = {
  id: number
  tierNumber: number
  title: string | null
  description: string | null
  scoreValue: number
  isRequiredForBoardCompletion: boolean
  ruleType: string | null
  requiredValue: number | null
  teamProgress: BoardTileTierTeamProgress[]
}

export type BoardTileTierTeamProgress = {
  teamId: number
  teamName: string
  currentValue: number
  isAchieved: boolean
  achievedAt: string | null
  isScored: boolean
  scoredAt: string | null
  scoreAwarded: number
}

export type EventBoardTeam = {
  id: number
  name: string
  score: number
  scoredTiers: number
  completedTiles: number
  currentValue: number
}

export type EventExternalCompetitionFreshness = {
  id: number
  provider: string
  name: string
  metricType: string
  metricKey: string
  lastSuccessfulSyncAt: string | null
  lastSyncStatus: string | null
  nextPublicSyncAvailableAt: string | null
}

export type EventTempleRefreshResponse = {
  event: EventSummary
  competitions: EventTempleRefreshCompetition[]
}

export type EventTempleRefreshCompetition = {
  id: number
  name: string
  externalId: string
  status: string
  refreshRequested: boolean
  lastSuccessfulSyncAt: string | null
  nextRefreshAvailableAt: string | null
  message: string
}

export type EventTeamListResponse = {
  event: EventSummary
  teams: EventTeamSummary[]
}

export type EventTeamSummary = {
  id: number
  name: string
  score: number
  scoredTiers: number
  completedTiles: number
  currentValue: number
  contributionCount: number
}

export type EventContributionListResponse = {
  event: EventSummary
  contributions: EventContribution[]
}

export type EventContribution = {
  id: number
  playerName: string
  teamId: number | null
  teamName: string | null
  tileTitle: string
  tierTitle: string | null
  valueAdded: number
  description: string | null
  createdAt: string
}

export async function listEvents(signal?: AbortSignal): Promise<EventSummary[]> {
  const data = await fetchJson<EventListResponse>("/api/events", signal)
  return data.events
}

export async function getEvent(
  slug: string,
  signal?: AbortSignal,
): Promise<EventSummary> {
  return fetchJson<EventSummary>(`/api/events/${slug}`, signal)
}

export async function getEventBoard(
  slug: string,
  signal?: AbortSignal,
): Promise<EventBoard> {
  return fetchJson<EventBoard>(`/api/events/${slug}/board`, signal)
}

export async function getEventTeams(
  slug: string,
  signal?: AbortSignal,
): Promise<EventTeamListResponse> {
  return fetchJson<EventTeamListResponse>(`/api/events/${slug}/teams`, signal)
}

export async function getEventTeamBoard(
  slug: string,
  teamId: number,
  signal?: AbortSignal,
): Promise<EventTeamBoard> {
  return fetchJson<EventTeamBoard>(`/api/events/${slug}/teams/${teamId}/board`, signal)
}

export async function getEventContributions(
  slug: string,
  signal?: AbortSignal,
): Promise<EventContributionListResponse> {
  return fetchJson<EventContributionListResponse>(
    `/api/events/${slug}/contributions`,
    signal,
  )
}

export async function requestTempleRefresh(
  slug: string,
): Promise<EventTempleRefreshResponse> {
  const response = await fetch(`/api/events/${slug}/templeosrs/refresh`, {
    method: "POST",
  })

  if (!response.ok) {
    throw new Error(`/api/events/${slug}/templeosrs/refresh request failed with ${response.status}`)
  }

  return (await response.json()) as EventTempleRefreshResponse
}

async function fetchJson<T>(url: string, signal?: AbortSignal): Promise<T> {
  const response = await fetch(url, { signal })

  if (!response.ok) {
    throw new Error(`${url} request failed with ${response.status}`)
  }

  return (await response.json()) as T
}
