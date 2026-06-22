import type { ComponentType, ReactNode } from "react"
import { Link, NavLink } from "react-router-dom"
import {
  ActivityIcon,
  ArrowRightIcon,
  CalendarDaysIcon,
  MedalIcon,
  ShieldIcon,
  SwordsIcon,
  TrophyIcon,
} from "lucide-react"
import type {
  BoardTile,
  BoardTileTeamProgress,
  BoardTileTier,
  EventContribution,
  EventSummary,
  EventTeamSummary,
} from "@/api/events"
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
import {
  formatDate,
  formatEventWindow,
  formatNumber,
  formatTimestamp,
} from "@/components/event/event-format"
import { cn } from "@/lib/utils"

const teamToneClasses = [
  "bg-chart-3",
  "bg-chart-2",
  "bg-chart-1",
  "bg-chart-4",
  "bg-chart-5",
]

export function AppFrame({ children }: { children: ReactNode }) {
  return (
    <main className="min-h-svh bg-background text-foreground">
      <div className="mx-auto flex w-full max-w-7xl flex-col gap-5 px-4 py-5 sm:px-6 lg:px-8">
        {children}
      </div>
    </main>
  )
}

export function BrandBlock({ title, subtitle }: { title: string; subtitle: string }) {
  return (
    <div className="flex items-center gap-3">
      <div className="flex size-10 items-center justify-center rounded-lg border bg-card">
        <SwordsIcon className="size-5 text-primary" aria-hidden="true" />
      </div>
      <div className="min-w-0">
        <h1 className="truncate text-xl font-semibold leading-tight md:text-2xl">
          {title}
        </h1>
        <p className="text-sm text-muted-foreground">{subtitle}</p>
      </div>
    </div>
  )
}

export function TopNav({ activeSlug }: { activeSlug?: string }) {
  return (
    <nav className="flex flex-wrap items-center gap-2">
      <NavItem to="/events">Events</NavItem>
      {activeSlug ? <NavItem to={`/events/${activeSlug}`}>Board</NavItem> : null}
      {activeSlug ? <NavItem to={`/admin/events/${activeSlug}/setup`}>Admin Setup</NavItem> : null}
    </nav>
  )
}

export function EventListCard({ event }: { event: EventSummary }) {
  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <CardTitle className="truncate">{event.name}</CardTitle>
            <CardDescription>{formatEventWindow(event)}</CardDescription>
          </div>
          <Badge variant={event.status === "active" ? "default" : "secondary"}>
            {event.status}
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-3">
        <Badge variant="outline">{event.eventType}</Badge>
        <Button asChild size="sm">
          <Link to={`/events/${event.slug}`}>
            Open
            <ArrowRightIcon data-icon="inline-end" aria-hidden="true" />
          </Link>
        </Button>
      </CardContent>
    </Card>
  )
}

export function EventHero({
  event,
  teams,
}: {
  event: EventSummary
  teams: EventTeamSummary[]
}) {
  const topTeam = teams[0]
  const maxScore = Math.max(1, ...teams.map((team) => team.score))

  return (
    <Card>
      <CardHeader className="border-b">
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div className="min-w-0">
            <CardTitle>Scoreboard</CardTitle>
            <CardDescription>
              {topTeam ? `${topTeam.name} leads with ${topTeam.score} point(s)` : "No scored tiers yet"}
            </CardDescription>
          </div>
          <div className="flex flex-wrap gap-2">
            <Badge variant="outline">
              <ActivityIcon data-icon="inline-start" aria-hidden="true" />
              {event.status}
            </Badge>
            <Badge variant="outline">
              <ShieldIcon data-icon="inline-start" aria-hidden="true" />
              {teams.length} teams
            </Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {teams.length === 0 ? (
          <p className="text-sm text-muted-foreground">Teams will appear when the event is seeded.</p>
        ) : (
          teams.slice(0, 5).map((team, index) => (
            <div key={team.id} className="grid gap-2 md:grid-cols-[minmax(0,1fr)_auto] md:items-center">
              <div className="min-w-0">
                <div className="flex items-center gap-2">
                  <span
                    className={cn("size-2 rounded-sm", teamToneClasses[index % teamToneClasses.length])}
                    aria-hidden="true"
                  />
                  <span className="truncate text-sm font-medium">{team.name}</span>
                  {index === 0 ? <TrophyIcon className="size-3.5 text-accent" aria-hidden="true" /> : null}
                </div>
                <Progress
                  className="mt-2 h-1.5"
                  value={(team.score / maxScore) * 100}
                  aria-label={`${team.name} score share`}
                />
              </div>
              <div className="flex gap-3 text-xs text-muted-foreground md:justify-end">
                <span className="tabular-nums">{team.score} pts</span>
                <span className="tabular-nums">{team.completedTiles} tiles</span>
                <span className="tabular-nums">{formatNumber(team.currentValue)} total</span>
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  )
}

export function StatusRail({ event }: { event: EventSummary }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Event Window</CardTitle>
        <CardDescription>{event.timeZone}</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <RailItem icon={CalendarDaysIcon} label="Starts" value={formatDate(event.startsAt, event.timeZone)} />
        <RailItem icon={CalendarDaysIcon} label="Ends" value={event.endsAt ? formatDate(event.endsAt, event.timeZone) : "Open"} />
      </CardContent>
    </Card>
  )
}

export function BoardTileCard({
  tile,
  teams,
}: {
  tile: BoardTile
  teams: EventTeamSummary[]
}) {
  const completedTeams = tile.teamProgress.filter((progress) => progress.isCompleted).length
  const fallbackProgress: BoardTileTeamProgress = {
    teamId: 0,
    teamName: "",
    currentValue: 0,
    currentTier: 0,
    isCompleted: false,
    completedAt: null,
  }
  const topProgress = tile.teamProgress.reduce(
    (current, candidate) => (candidate.currentValue > current.currentValue ? candidate : current),
    tile.teamProgress[0] ?? fallbackProgress,
  )

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div className="min-w-0">
            <CardTitle className="truncate">{tile.title}</CardTitle>
            <CardDescription>{tile.description ?? "Progress tile"}</CardDescription>
          </div>
          {completedTeams > 0 ? (
            <Badge>{completedTeams}/{tile.teamProgress.length} complete</Badge>
          ) : (
            <Badge variant="outline">top tier {topProgress.currentTier}</Badge>
          )}
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {tile.tiers.map((tier) => (
          <TileTierBlock key={tier.id} tier={tier} teams={teams} />
        ))}
      </CardContent>
    </Card>
  )
}

function TileTierBlock({
  tier,
  teams,
}: {
  tier: BoardTileTier
  teams: EventTeamSummary[]
}) {
  return (
    <div className="flex flex-col gap-3 rounded-lg bg-background/50 p-3 ring-1 ring-foreground/10">
      <div className="flex items-center justify-between gap-3">
        <div className="min-w-0">
          <h2 className="truncate text-sm font-medium">
            {tier.title ?? `Tier ${tier.tierNumber}`}
          </h2>
          <p className="text-xs text-muted-foreground">
            {tier.requiredValue ? `Target ${formatNumber(tier.requiredValue)}` : "Target pending"}
          </p>
        </div>
        <Badge variant="secondary">{tier.scoreValue} pt</Badge>
      </div>
      <div className="flex flex-col gap-3">
        {tier.teamProgress.map((progress, index) => {
          const percent = progressPercent(progress.currentValue, tier.requiredValue)
          const team = teams.find((candidate) => candidate.id === progress.teamId)
          return (
            <div key={progress.teamId} className="flex flex-col gap-2">
              <div className="flex items-center justify-between gap-3 text-xs">
                <div className="flex min-w-0 items-center gap-2">
                  <span
                    className={cn("size-2 rounded-sm", teamToneClasses[index % teamToneClasses.length])}
                    aria-hidden="true"
                  />
                  <span className="truncate">{team?.name ?? progress.teamName}</span>
                  {progress.isAchieved ? <Badge variant="secondary">done</Badge> : null}
                </div>
                <span className="font-mono tabular-nums text-muted-foreground">
                  {formatNumber(progress.currentValue)}
                  {tier.requiredValue ? ` / ${formatNumber(tier.requiredValue)}` : ""}
                </span>
              </div>
              <div className="grid grid-cols-[1fr_auto] items-center gap-2">
                <Progress
                  className="h-1.5"
                  value={percent}
                  aria-label={`${progress.teamName} ${tier.title ?? "tier"} progress`}
                />
                <span className="w-9 text-right font-mono text-[11px] tabular-nums text-muted-foreground">
                  {Math.round(percent)}%
                </span>
              </div>
            </div>
          )
        })}
      </div>
    </div>
  )
}

export function ContributionFeed({
  isLoading,
  contributions,
}: {
  isLoading: boolean
  contributions: EventContribution[]
}) {
  if (isLoading) {
    return <StateCard title="Loading feed" detail="Reading recent contributions." />
  }

  if (contributions.length === 0) {
    return <StateCard title="No drops yet" detail="Post mock activity to populate the feed." />
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        {contributions.map((contribution, index) => (
          <div key={contribution.id} className="flex flex-col gap-3">
            {index > 0 ? <Separator /> : null}
            <div className="flex items-start gap-3">
              <div className="flex size-8 items-center justify-center rounded-md border bg-background">
                <MedalIcon className="size-4 text-primary" aria-hidden="true" />
              </div>
              <div className="min-w-0 flex-1">
                <p className="text-sm font-medium">
                  {contribution.playerName} added {formatNumber(contribution.valueAdded)} to {contribution.tileTitle}
                </p>
                <p className="text-xs text-muted-foreground">
                  {contribution.teamName ?? "No team"} · {contribution.tierTitle ?? "Tile"} ·{" "}
                  {formatTimestamp(contribution.createdAt)}
                </p>
                {contribution.description ? (
                  <p className="mt-1 text-xs text-muted-foreground">{contribution.description}</p>
                ) : null}
              </div>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

export function SectionHeading({ title, description }: { title: string; description: string }) {
  return (
    <div className="flex flex-col gap-1">
      <h2 className="text-lg font-semibold">{title}</h2>
      <p className="text-sm text-muted-foreground">{description}</p>
    </div>
  )
}

export function StateCard({ title, detail }: { title: string; detail: string }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>{title}</CardTitle>
        <CardDescription>{detail}</CardDescription>
      </CardHeader>
    </Card>
  )
}

function RailItem({
  icon: Icon,
  label,
  value,
}: {
  icon: ComponentType<{ className?: string; "aria-hidden"?: boolean }>
  label: string
  value: string
}) {
  return (
    <div className="flex items-center gap-3">
      <Icon className="size-4 text-muted-foreground" aria-hidden={true} />
      <div className="min-w-0">
        <p className="text-xs text-muted-foreground">{label}</p>
        <p className="truncate text-sm font-medium">{value}</p>
      </div>
    </div>
  )
}

function NavItem({ to, children }: { to: string; children: ReactNode }) {
  return (
    <NavLink
      to={to}
      className={({ isActive }) =>
        cn(
          "rounded-md px-3 py-1.5 text-sm text-muted-foreground transition-colors hover:bg-muted hover:text-foreground",
          isActive ? "bg-muted text-foreground" : "",
        )
      }
    >
      {children}
    </NavLink>
  )
}

function progressPercent(currentValue: number, requiredValue: number | null) {
  if (!requiredValue || requiredValue <= 0) {
    return 0
  }

  return Math.min(100, Math.max(0, (currentValue / requiredValue) * 100))
}
