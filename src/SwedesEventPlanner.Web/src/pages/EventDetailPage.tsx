import { useMemo } from "react"
import { useQuery } from "@tanstack/react-query"
import { useParams } from "react-router-dom"
import {
  getEvent,
  getEventBoard,
  getEventContributions,
  getEventTeams,
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
import { formatEventWindow } from "@/components/event/event-format"

export function EventDetailPage() {
  const { eventSlug } = useParams()
  const slug = eventSlug ?? ""

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
  const contributions = contributionsQuery.data?.contributions ?? []

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
        </div>
      </section>
    </AppFrame>
  )
}
