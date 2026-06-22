import { useEffect, useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { useParams } from "react-router-dom"
import {
  getEvent,
  getEventBoard,
  getEventContributions,
  getEventTeams,
  requestTempleRefresh,
  type EventExternalCompetitionFreshness,
  type EventTempleRefreshResponse,
} from "@/api/events"
import {
  AppFrame,
  BoardTileCard,
  BrandBlock,
  ContributionFeed,
  EventHero,
  SectionHeading,
  StateCard,
  StatusRail,
  TopNav,
} from "@/components/event/EventUi"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { formatEventWindow, formatTimestamp } from "@/components/event/event-format"

export function EventDetailPage() {
  const { eventSlug } = useParams()
  const slug = eventSlug ?? ""
  const queryClient = useQueryClient()

  const eventQuery = useQuery({
    queryKey: ["event", slug],
    queryFn: ({ signal }) => getEvent(slug, signal),
    enabled: slug.length > 0,
    retry: false,
  })
  const boardQuery = useQuery({
    queryKey: ["event-board", slug],
    queryFn: ({ signal }) => getEventBoard(slug, signal),
    enabled: slug.length > 0,
    retry: false,
  })
  const teamsQuery = useQuery({
    queryKey: ["event-teams", slug],
    queryFn: ({ signal }) => getEventTeams(slug, signal),
    enabled: slug.length > 0,
    retry: false,
  })
  const contributionsQuery = useQuery({
    queryKey: ["event-contributions", slug],
    queryFn: ({ signal }) => getEventContributions(slug, signal),
    enabled: slug.length > 0,
    retry: false,
  })

  const event = eventQuery.data ?? boardQuery.data?.event ?? teamsQuery.data?.event
  const teams = useMemo(
    () =>
      teamsQuery.data?.teams ??
      boardQuery.data?.teams.map((team) => ({
        ...team,
        contributionCount: 0,
      })) ??
      [],
    [boardQuery.data?.teams, teamsQuery.data?.teams],
  )
  const board = boardQuery.data?.board
  const externalFreshness = boardQuery.data?.externalCompetitionFreshness ?? []
  const contributions = contributionsQuery.data?.contributions ?? []
  const templeRefreshMutation = useMutation({
    mutationFn: () => requestTempleRefresh(slug),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["event-board", slug] }),
        queryClient.invalidateQueries({ queryKey: ["event-teams", slug] }),
        queryClient.invalidateQueries({ queryKey: ["event-contributions", slug] }),
      ])
    },
  })

  if (eventQuery.isError) {
    return (
      <AppFrame>
        <StateCard title="Event unavailable" detail="The requested event was not found." />
      </AppFrame>
    )
  }

  if (!event) {
    return (
      <AppFrame>
        <StateCard title="Loading event" detail="Fetching board progress." />
      </AppFrame>
    )
  }

  return (
    <AppFrame>
      <header className="flex flex-col gap-4 border-b pb-5 md:flex-row md:items-center md:justify-between">
        <BrandBlock title={event.name} subtitle={formatEventWindow(event)} />
        <TopNav activeSlug={event.slug} />
      </header>

      <section className="grid gap-4 lg:grid-cols-[1fr_0.36fr]">
        <EventHero event={event} teams={teams} />
        <StatusRail event={event} />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_0.42fr]">
        <div className="flex flex-col gap-4">
          <SectionHeading
            title={board?.name ?? "Board"}
            description="Team progress across tiles and tiers"
          />
          {boardQuery.isLoading ? (
            <StateCard title="Loading board" detail="Reading current progress." />
          ) : board ? (
            <div className="grid gap-3 md:grid-cols-2">
              {board.tiles.map((tile) => (
                <BoardTileCard key={tile.id} tile={tile} teams={teams} />
              ))}
            </div>
          ) : (
            <StateCard title="Board unavailable" detail="No board has been seeded yet." />
          )}
        </div>

        <div className="flex flex-col gap-4">
          <SectionHeading
            title="Recent Drops"
            description="Visible mock activity contributions"
          />
          <ContributionFeed
            isLoading={contributionsQuery.isLoading}
            contributions={contributions}
          />
          <ExternalFreshnessCard freshness={externalFreshness} />
          <ExternalRefreshCard
            freshness={externalFreshness}
            isRefreshing={templeRefreshMutation.isPending}
            refreshResult={templeRefreshMutation.data ?? null}
            error={templeRefreshMutation.error}
            onRefresh={() => templeRefreshMutation.mutate()}
          />
        </div>
      </section>
    </AppFrame>
  )
}

function ExternalFreshnessCard({
  freshness,
}: {
  freshness: EventExternalCompetitionFreshness[]
}) {
  if (freshness.length === 0) {
    return null
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>TempleOSRS</CardTitle>
        <CardDescription>Cached sync freshness</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {freshness.map((item) => (
          <div key={item.id} className="flex items-center justify-between gap-3 text-sm">
            <div className="min-w-0">
              <p className="truncate font-medium">{item.name}</p>
              <p className="truncate text-xs text-muted-foreground">
                {item.metricType} · {item.metricKey}
              </p>
            </div>
            <div className="flex flex-col items-end gap-1">
              <Badge variant={item.lastSyncStatus === "succeeded" ? "default" : "secondary"}>
                {item.lastSyncStatus ?? "unsynced"}
              </Badge>
              <span className="text-xs text-muted-foreground">
                {item.lastSuccessfulSyncAt ? formatTimestamp(item.lastSuccessfulSyncAt) : "Never"}
              </span>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function ExternalRefreshCard({
  freshness,
  isRefreshing,
  refreshResult,
  error,
  onRefresh,
}: {
  freshness: EventExternalCompetitionFreshness[]
  isRefreshing: boolean
  refreshResult: EventTempleRefreshResponse | null
  error: Error | null
  onRefresh: () => void
}) {
  const [refreshClockMs, setRefreshClockMs] = useState(() => Date.now())
  const resultNextRefreshAt = refreshResult?.competitions
    .map((competition) => competition.nextRefreshAvailableAt)
    .find(Boolean)
  const freshnessCooldowns = freshness
    .map((item) => cooldownUntil(item.lastSuccessfulSyncAt))
    .filter((value): value is string => value !== null)
  const freshnessNextRefreshAt =
    freshness.length > 0 && freshnessCooldowns.length === freshness.length
      ? freshnessCooldowns.sort()[0]
      : null
  const nextRefreshAt = resultNextRefreshAt ?? freshnessNextRefreshAt

  useEffect(() => {
    if (!nextRefreshAt) {
      return
    }

    const delayMs = Math.max(0, new Date(nextRefreshAt).getTime() - Date.now())
    if (delayMs === 0) {
      return
    }

    const timeoutId = window.setTimeout(() => setRefreshClockMs(Date.now()), delayMs)
    return () => window.clearTimeout(timeoutId)
  }, [nextRefreshAt])

  const canRefresh =
    !isRefreshing && (!nextRefreshAt || new Date(nextRefreshAt).getTime() <= refreshClockMs)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Refresh Temple</CardTitle>
        <CardDescription>Public read-only cache refresh</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Button disabled={!canRefresh} onClick={onRefresh}>
          {isRefreshing ? "Refreshing..." : "Refresh"}
        </Button>
        {freshness.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No linked TempleOSRS competition is currently visible.
          </p>
        ) : null}
        {!canRefresh && nextRefreshAt ? (
          <p className="text-sm text-muted-foreground">
            Refresh available again at {formatTimestamp(nextRefreshAt)}.
          </p>
        ) : null}
        {error ? <p className="text-sm text-destructive">{error.message}</p> : null}
        {refreshResult?.competitions.map((competition) => (
          <div key={`${competition.id}-${competition.externalId}`} className="rounded-lg border bg-background/40 p-3 text-sm">
            <div className="flex items-center justify-between gap-3">
              <span className="min-w-0 truncate font-medium">{competition.name}</span>
              <Badge variant={competition.status === "succeeded" ? "default" : "secondary"}>
                {competition.status}
              </Badge>
            </div>
            <p className="mt-1 text-xs text-muted-foreground">{competition.message}</p>
            {competition.nextRefreshAvailableAt ? (
              <p className="mt-1 text-xs text-muted-foreground">
                Next refresh {formatTimestamp(competition.nextRefreshAvailableAt)}
              </p>
            ) : null}
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function cooldownUntil(lastSuccessfulSyncAt: string | null) {
  if (!lastSuccessfulSyncAt) {
    return null
  }

  const nextRefreshAt = new Date(new Date(lastSuccessfulSyncAt).getTime() + 5 * 60 * 1000)
  return nextRefreshAt.getTime() > Date.now() ? nextRefreshAt.toISOString() : null
}
