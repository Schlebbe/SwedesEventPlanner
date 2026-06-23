import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Link, useParams } from "react-router-dom"
import {
  getEventContributions,
  getEventTeamBoard,
  requestTempleRefresh,
  type EventTeamSummary,
} from "@/api/events"
import {
  AppFrame,
  BoardTileCard,
  BrandBlock,
  ContributionFeed,
  SectionHeading,
  StateCard,
  StatusRail,
  TopNav,
} from "@/components/event/EventUi"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { formatEventWindow } from "@/components/event/event-format"
import { ExternalFreshnessCard, ExternalRefreshCard } from "@/pages/EventDetailPage"

export function EventTeamPage() {
  const { eventSlug, teamId } = useParams()
  const slug = eventSlug ?? ""
  const numericTeamId = Number(teamId)
  const queryClient = useQueryClient()

  const boardQuery = useQuery({
    queryKey: ["event-team-board", slug, numericTeamId],
    queryFn: ({ signal }) => getEventTeamBoard(slug, numericTeamId, signal),
    enabled: slug.length > 0 && Number.isInteger(numericTeamId) && numericTeamId > 0,
    retry: false,
    refetchInterval: 5000,
  })
  const contributionsQuery = useQuery({
    queryKey: ["event-contributions", slug],
    queryFn: ({ signal }) => getEventContributions(slug, signal),
    enabled: slug.length > 0,
    retry: false,
    refetchInterval: 5000,
  })
  const templeRefreshMutation = useMutation({
    mutationFn: () => requestTempleRefresh(slug),
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: ["event-team-board", slug, numericTeamId] }),
        queryClient.invalidateQueries({ queryKey: ["event-contributions", slug] }),
      ])
    },
  })

  if (!Number.isInteger(numericTeamId) || numericTeamId <= 0) {
    return (
      <AppFrame>
        <StateCard title="Team unavailable" detail="The team route is missing a valid team ID." />
      </AppFrame>
    )
  }

  if (boardQuery.isError) {
    return (
      <AppFrame>
        <StateCard title="Team unavailable" detail="The requested team board was not found." />
      </AppFrame>
    )
  }

  const data = boardQuery.data
  if (!data) {
    return (
      <AppFrame>
        <StateCard title="Loading team board" detail="Reading current tile progress." />
      </AppFrame>
    )
  }

  const teamSummary: EventTeamSummary = {
    ...data.team,
    contributionCount:
      contributionsQuery.data?.contributions.filter((contribution) => contribution.teamId === data.team.id).length ?? 0,
  }
  const teamContributions = contributionsQuery.data?.contributions.filter(
    (contribution) => contribution.teamId === data.team.id,
  ) ?? []
  const completedTiles = data.board.tiles.filter((tile) =>
    tile.teamProgress.some((progress) => progress.teamId === data.team.id && progress.isCompleted),
  ).length
  const activeTiles = data.board.tiles.filter((tile) =>
    tile.tiers.some((tier) =>
      tier.teamProgress.some((progress) => progress.teamId === data.team.id && progress.currentValue > 0),
    ),
  ).length
  const externalFreshness = data.externalCompetitionFreshness

  return (
    <AppFrame>
      <header className="flex flex-col gap-4 border-b pb-5 md:flex-row md:items-center md:justify-between">
        <BrandBlock title={`${data.team.name} Board`} subtitle={`${data.event.name} · ${formatEventWindow(data.event)}`} />
        <TopNav activeSlug={data.event.slug} />
      </header>

      <div className="flex flex-wrap gap-2">
        <Button asChild variant="outline">
          <Link to={`/events/${data.event.slug}`}>Event Overview</Link>
        </Button>
        <Button asChild variant="outline">
          <Link to={`/admin/events/${data.event.slug}/setup`}>Admin Setup</Link>
        </Button>
      </div>

      <section className="grid gap-4 lg:grid-cols-[1fr_0.36fr]">
        <TeamStatusCard team={teamSummary} completedTiles={completedTiles} activeTiles={activeTiles} />
        <StatusRail event={data.event} />
      </section>

      <section className="grid gap-4 xl:grid-cols-[1fr_0.42fr]">
        <div className="flex flex-col gap-4">
          <SectionHeading title={data.board.name} description="This team’s tile and tier progress" />
          {data.board.tiles.length === 0 ? (
            <StateCard title="No tiles yet" detail="Tiles will appear after they are created in admin setup." />
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {data.board.tiles.map((tile) => (
                <BoardTileCard key={tile.id} tile={tile} teams={[teamSummary]} />
              ))}
            </div>
          )}
        </div>

        <div className="flex flex-col gap-4">
          <SectionHeading title="Team Contributions" description="Activity and TempleOSRS rows for this team" />
          <ContributionFeed
            isLoading={contributionsQuery.isLoading}
            contributions={teamContributions}
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

function TeamStatusCard({
  team,
  completedTiles,
  activeTiles,
}: {
  team: EventTeamSummary
  completedTiles: number
  activeTiles: number
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{team.name}</CardTitle>
        <CardDescription>Live team board, refreshed automatically</CardDescription>
      </CardHeader>
      <CardContent className="grid gap-3 sm:grid-cols-4">
        <TeamMetric label="Score" value={`${team.score} pts`} />
        <TeamMetric label="Scored tiers" value={team.scoredTiers.toString()} />
        <TeamMetric label="Completed tiles" value={completedTiles.toString()} />
        <TeamMetric label="Active tiles" value={activeTiles.toString()} />
      </CardContent>
    </Card>
  )
}

function TeamMetric({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border bg-background/40 p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="text-lg font-semibold tabular-nums">{value}</p>
    </div>
  )
}
