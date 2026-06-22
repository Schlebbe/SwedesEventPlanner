import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Link, useNavigate } from "react-router-dom"
import { PlusIcon, ShieldIcon } from "lucide-react"
import {
  createAdminEvent,
  listAdminEvents,
  type CreateAdminEventRequest,
} from "@/api/admin"
import {
  AppFrame,
  BrandBlock,
  SectionHeading,
  StateCard,
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
import { Input } from "@/components/ui/input"
import { formatTimestamp } from "@/components/event/event-format"

const tokenStorageKey = "swedes-event-planner-admin-token"

const defaultEvent = (): CreateAdminEventRequest => ({
  slug: "",
  name: "",
  eventType: "bingo",
  status: "draft",
  startsAt: new Date().toISOString(),
  endsAt: null,
  timeZone: "Europe/Stockholm",
})

export function AdminHomePage() {
  const navigate = useNavigate()
  const queryClient = useQueryClient()
  const [adminToken, setAdminToken] = useState(() => localStorage.getItem(tokenStorageKey) ?? "")
  const [eventForm, setEventForm] = useState<CreateAdminEventRequest>(defaultEvent)
  const tokenReady = adminToken.trim().length > 0

  const eventsQuery = useQuery({
    queryKey: ["admin-events", tokenReady],
    queryFn: ({ signal }) => listAdminEvents(adminToken, signal),
    enabled: tokenReady,
    retry: false,
  })

  const createMutation = useMutation({
    mutationFn: () => createAdminEvent(eventForm, adminToken),
    onSuccess: async (event) => {
      setEventForm(defaultEvent())
      await queryClient.invalidateQueries({ queryKey: ["admin-events"] })
      navigate(`/admin/events/${event.slug}/setup`)
    },
  })

  function saveToken(value: string) {
    setAdminToken(value)
    if (value.trim().length > 0) {
      localStorage.setItem(tokenStorageKey, value)
    } else {
      localStorage.removeItem(tokenStorageKey)
    }
  }

  return (
    <AppFrame>
      <header className="flex flex-col gap-4 border-b pb-5 md:flex-row md:items-center md:justify-between">
        <BrandBlock title="Admin Setup" subtitle="Development/testing setup for local events" />
        <TopNav />
      </header>

      <section className="grid gap-4 lg:grid-cols-[0.42fr_1fr]">
        <div className="flex flex-col gap-4">
          <Card>
            <CardHeader>
              <CardTitle>Admin Token</CardTitle>
              <CardDescription>Local testing token for setup endpoints.</CardDescription>
            </CardHeader>
            <CardContent className="flex flex-col gap-3">
              <label className="flex flex-col gap-2 text-sm font-medium">
                Token
                <Input
                  value={adminToken}
                  type="password"
                  autoComplete="off"
                  placeholder="dev-admin-token-change-me"
                  onChange={(event) => saveToken(event.target.value)}
                />
              </label>
            </CardContent>
          </Card>

          <CreateEventCard
            eventForm={eventForm}
            disabled={!tokenReady || createMutation.isPending}
            error={createMutation.error}
            onChange={setEventForm}
            onCreate={() => createMutation.mutate()}
          />
        </div>

        <div className="flex flex-col gap-4">
          <SectionHeading title="Events" description="Manual setup starts from an empty local database" />
          {!tokenReady ? (
            <StateCard title="Admin token required" detail="Enter the local admin token to list or create events." />
          ) : eventsQuery.isLoading ? (
            <StateCard title="Loading events" detail="Reading local setup data." />
          ) : eventsQuery.isError ? (
            <StateCard title="Events unavailable" detail={errorText(eventsQuery.error)} />
          ) : eventsQuery.data?.events.length === 0 ? (
            <StateCard title="No events yet" detail="Create an event here, then open its setup page." />
          ) : (
            <div className="grid gap-3">
              {eventsQuery.data?.events.map((event) => (
                <Card key={event.id}>
                  <CardContent className="grid gap-3 p-4 md:grid-cols-[1fr_auto] md:items-center">
                    <div className="min-w-0">
                      <div className="flex flex-wrap items-center gap-2">
                        <p className="truncate text-sm font-medium">{event.name}</p>
                        <Badge variant="secondary">{event.status}</Badge>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        {event.slug} · starts {formatTimestamp(event.startsAt)}
                      </p>
                    </div>
                    <div className="flex flex-wrap gap-2">
                      <Button asChild size="sm" variant="outline">
                        <Link to={`/events/${event.slug}`}>Public board</Link>
                      </Button>
                      <Button asChild size="sm">
                        <Link to={`/admin/events/${event.slug}/setup`}>
                          <ShieldIcon data-icon="inline-start" aria-hidden="true" />
                          Setup
                        </Link>
                      </Button>
                    </div>
                  </CardContent>
                </Card>
              ))}
            </div>
          )}
        </div>
      </section>
    </AppFrame>
  )
}

function CreateEventCard({
  eventForm,
  disabled,
  error,
  onChange,
  onCreate,
}: {
  eventForm: CreateAdminEventRequest
  disabled: boolean
  error: Error | null
  onChange: (value: CreateAdminEventRequest) => void
  onCreate: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Create Event</CardTitle>
        <CardDescription>Create real local test data manually.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Slug
          <Input
            value={eventForm.slug}
            placeholder="summer-bingo-2026"
            onChange={(event) => onChange({ ...eventForm, slug: event.target.value })}
          />
        </label>
        <label className="flex flex-col gap-2 text-sm font-medium">
          Name
          <Input
            value={eventForm.name}
            placeholder="Summer Bingo 2026"
            onChange={(event) => onChange({ ...eventForm, name: event.target.value })}
          />
        </label>
        <div className="grid gap-3 sm:grid-cols-2">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Status
            <select
              className="h-9 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              value={eventForm.status}
              onChange={(event) => onChange({ ...eventForm, status: event.target.value })}
            >
              <option value="draft">draft</option>
              <option value="scheduled">scheduled</option>
              <option value="active">active</option>
            </select>
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Type
            <Input
              value={eventForm.eventType}
              onChange={(event) => onChange({ ...eventForm, eventType: event.target.value })}
            />
          </label>
        </div>
        <Button
          disabled={disabled || eventForm.slug.trim().length === 0 || eventForm.name.trim().length === 0}
          onClick={onCreate}
        >
          <PlusIcon data-icon="inline-start" aria-hidden="true" />
          Create Event
        </Button>
        {error ? <p className="text-sm text-destructive">{errorText(error)}</p> : null}
      </CardContent>
    </Card>
  )
}

function errorText(error: unknown) {
  return error instanceof Error ? error.message : "The request failed."
}
