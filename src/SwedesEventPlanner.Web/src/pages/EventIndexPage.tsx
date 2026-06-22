import { useQuery } from "@tanstack/react-query"
import { listEvents } from "@/api/events"
import {
  AppFrame,
  BrandBlock,
  EventListCard,
  SectionHeading,
  StateCard,
  TopNav,
} from "@/components/event/EventUi"
import { Badge } from "@/components/ui/badge"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"

export function EventIndexPage() {
  const eventsQuery = useQuery({
    queryKey: ["events"],
    queryFn: ({ signal }) => listEvents(signal),
    retry: false,
  })

  const events = eventsQuery.data ?? []

  return (
    <AppFrame>
      <header className="flex flex-col gap-4 border-b pb-5 md:flex-row md:items-center md:justify-between">
        <BrandBlock
          title="Swedes Event Planner"
          subtitle="Clan boards, team progress, and drop feeds"
        />
        <TopNav />
      </header>

      <section className="grid gap-4 lg:grid-cols-[1fr_0.48fr]">
        <div className="flex flex-col gap-4">
          <SectionHeading
            title="Events"
            description="Visible event boards ready for participants"
          />
          {eventsQuery.isLoading ? (
            <StateCard title="Loading events" detail="Checking the local API." />
          ) : eventsQuery.isError ? (
            <StateCard title="Events unavailable" detail="The API did not respond." />
          ) : events.length === 0 ? (
            <StateCard
              title="No visible events"
              detail="Seed the mock activity demo from the admin helper."
            />
          ) : (
            <div className="grid gap-3 md:grid-cols-2">
              {events.map((event) => (
                <EventListCard key={event.id} event={event} />
              ))}
            </div>
          )}
        </div>

        <Card>
          <CardHeader>
            <CardTitle>Local Demo</CardTitle>
            <CardDescription>Seed and post mock drops to watch the board move.</CardDescription>
          </CardHeader>
          <CardContent className="flex flex-col gap-3">
            <Badge variant="secondary">/api/admin/dev/seed-mock-activity-demo</Badge>
            <p className="text-sm text-muted-foreground">
              The helper returns the seeded slug so this page can open directly at the live board.
            </p>
          </CardContent>
        </Card>
      </section>
    </AppFrame>
  )
}
