import { useMemo, type ComponentType, type ReactNode } from "react"
import { useQuery } from "@tanstack/react-query"
import {
  ActivityIcon,
  RefreshCwIcon,
  ShieldIcon,
  SwordsIcon,
} from "lucide-react"
import { NavLink, Route, Routes } from "react-router-dom"
import { listEvents, type EventSummary } from "@/api/events"
import { Badge } from "@/components/ui/badge"
import { Button } from "@/components/ui/button"
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card"
import { Progress } from "@/components/ui/progress"
import { Separator } from "@/components/ui/separator"
import { cn } from "@/lib/utils"

const fallbackEvents: EventSummary[] = [
  {
    id: 1,
    slug: "summer-bingo-2026",
    name: "Summer Bingo 2026",
    eventType: "bingo",
    status: "active",
    startsAt: "2026-07-02T18:00:00Z",
    endsAt: "2026-07-16T18:00:00Z",
    timeZone: "Europe/Stockholm",
  },
]

const teams = [
  { name: "Blue Dragons", score: 18, tiles: 7, color: "bg-chart-3" },
  { name: "Golden Chins", score: 16, tiles: 6, color: "bg-chart-2" },
  { name: "Verdant Spears", score: 14, tiles: 5, color: "bg-chart-1" },
  { name: "Crimson Helms", score: 11, tiles: 4, color: "bg-chart-4" },
]

const tiles = [
  { name: "TOB", detail: "18 / 25 pts", value: 72 },
  { name: "Pets", detail: "2 / 3 pets", value: 67 },
  { name: "Slayer", detail: "11.8m / 15m XP", value: 79 },
  { name: "Zulrah", detail: "64 / 100 KC", value: 64 },
  { name: "Soulreaper", detail: "3 / 4 pieces", value: 75 },
  { name: "Manual", detail: "1 / 3 reviews", value: 33 },
]

function App() {
  return (
    <Routes>
      <Route path="/" element={<Scoreboard />} />
      <Route path="/events/:eventSlug" element={<Scoreboard />} />
    </Routes>
  )
}

function Scoreboard() {
  const eventsQuery = useQuery({
    queryKey: ["events"],
    queryFn: ({ signal }) => listEvents(signal),
    retry: false,
  })

  const events = eventsQuery.data?.length ? eventsQuery.data : fallbackEvents
  const activeEvent = events[0]
  const formattedWindow = useMemo(
    () =>
      new Intl.DateTimeFormat("sv-SE", {
        dateStyle: "medium",
        timeStyle: "short",
        timeZone: activeEvent.timeZone,
      }).format(new Date(activeEvent.startsAt)),
    [activeEvent.startsAt, activeEvent.timeZone],
  )

  return (
    <main className="min-h-svh bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-4 py-5 sm:px-6 lg:px-8">
        <header className="flex flex-col gap-4 border-b pb-5 md:flex-row md:items-center md:justify-between">
          <div className="flex items-center gap-3">
            <div className="flex size-10 items-center justify-center rounded-lg border bg-card">
              <SwordsIcon className="size-5 text-primary" aria-hidden="true" />
            </div>
            <div>
              <h1 className="text-xl font-semibold leading-tight md:text-2xl">
                Swedes Event Planner
              </h1>
              <p className="text-sm text-muted-foreground">
                {activeEvent.name} · {formattedWindow}
              </p>
            </div>
          </div>
          <nav className="flex items-center gap-2">
            <NavItem to="/">Scoreboard</NavItem>
            <NavItem to={`/events/${activeEvent.slug}`}>Event</NavItem>
            <Button variant="outline" size="sm">
              <RefreshCwIcon data-icon="inline-start" aria-hidden="true" />
              Sync
            </Button>
          </nav>
        </header>

        <section className="grid gap-4 lg:grid-cols-[1.4fr_0.9fr]">
          <Card className="rounded-lg">
            <CardHeader>
              <CardTitle>Board Progress</CardTitle>
              <CardDescription>
                {activeEvent.status} · {activeEvent.eventType} ·{" "}
                {activeEvent.timeZone}
              </CardDescription>
            </CardHeader>
            <CardContent>
              <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
                {tiles.map((tile) => (
                  <div
                    key={tile.name}
                    className="rounded-lg border bg-background/40 p-3"
                  >
                    <div className="mb-3 flex items-start justify-between gap-3">
                      <div>
                        <h2 className="text-sm font-medium">{tile.name}</h2>
                        <p className="text-xs text-muted-foreground">
                          {tile.detail}
                        </p>
                      </div>
                      <span className="font-mono text-xs text-muted-foreground">
                        {tile.value}%
                      </span>
                    </div>
                    <Progress value={tile.value} aria-label={tile.name} />
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>

          <Card className="rounded-lg">
            <CardHeader>
              <CardTitle>Team Standings</CardTitle>
              <CardDescription>Cached event progress</CardDescription>
            </CardHeader>
            <CardContent>
              <ol className="flex flex-col gap-3">
                {teams.map((team, index) => (
                  <li key={team.name} className="flex items-center gap-3">
                    <span className="w-5 text-sm text-muted-foreground">
                      {index + 1}
                    </span>
                    <span
                      className={cn("size-2 rounded-sm", team.color)}
                      aria-hidden="true"
                    />
                    <div className="min-w-0 flex-1">
                      <p className="truncate text-sm font-medium">
                        {team.name}
                      </p>
                      <p className="text-xs text-muted-foreground">
                        {team.tiles} scored tiles
                      </p>
                    </div>
                    <span className="font-mono text-sm">{team.score}</span>
                  </li>
                ))}
              </ol>
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-4 lg:grid-cols-3">
          <StatusCard
            title="API"
            value={eventsQuery.isError ? "offline" : "ready"}
            icon={ActivityIcon}
          />
          <StatusCard title="Worker" value="separate process" icon={ShieldIcon} />
          <StatusCard title="Temple" value="deferred" icon={RefreshCwIcon} />
        </section>
      </div>
    </main>
  )
}

function NavItem({ to, children }: { to: string; children: ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn(
          "rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground",
          isActive && "bg-muted text-foreground",
        )
      }
    >
      {children}
    </NavLink>
  )
}

function StatusCard({
  title,
  value,
  icon: Icon,
}: {
  title: string
  value: string
  icon: ComponentType<{ className?: string; "aria-hidden"?: boolean }>
}) {
  return (
    <Card className="rounded-lg" size="sm">
      <CardHeader>
        <div className="flex items-center justify-between gap-3">
          <CardTitle>{title}</CardTitle>
          <Icon className="size-4 text-muted-foreground" aria-hidden={true} />
        </div>
      </CardHeader>
      <CardContent>
        <Separator className="mb-3" />
        <Badge variant="outline">{value}</Badge>
      </CardContent>
    </Card>
  )
}

export default App
