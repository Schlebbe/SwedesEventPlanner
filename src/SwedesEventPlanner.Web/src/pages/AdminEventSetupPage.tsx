import { useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { SaveIcon, UploadIcon, UsersIcon } from "lucide-react"
import { useParams } from "react-router-dom"
import {
  assignParticipantTeam,
  createEventTeam,
  importCsvSignups,
  listAdminParticipants,
  listAdminSignups,
  type AdminEventParticipant,
  type CsvSignupImportResponse,
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
import { Separator } from "@/components/ui/separator"
import { Textarea } from "@/components/ui/textarea"

const tokenStorageKey = "swedes-event-planner-admin-token"
const sampleCsv = [
  "Timestamp,Email Address,RuneScape Name,Availability,Daily Hours,Preferred Content,Notes,Team Preference",
  "2026-07-02T17:30:00Z,player@example.invalid,Sebbe,Evenings,3,Raids,Ready,Blue",
].join("\n")

export function AdminEventSetupPage() {
  const { eventSlug } = useParams()
  const slug = eventSlug ?? ""
  const queryClient = useQueryClient()
  const [adminToken, setAdminToken] = useState(() => localStorage.getItem(tokenStorageKey) ?? "")
  const [csvText, setCsvText] = useState(sampleCsv)
  const [teamName, setTeamName] = useState("")
  const [selectedTeams, setSelectedTeams] = useState<Record<number, string>>({})
  const [lastImport, setLastImport] = useState<CsvSignupImportResponse | null>(null)
  const tokenReady = adminToken.trim().length > 0

  const signupsQuery = useQuery({
    queryKey: ["admin-event-signups", slug, tokenReady],
    queryFn: ({ signal }) => listAdminSignups(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
  })
  const participantsQuery = useQuery({
    queryKey: ["admin-event-participants", slug, tokenReady],
    queryFn: ({ signal }) => listAdminParticipants(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
  })

  const event = participantsQuery.data?.event ?? signupsQuery.data?.event
  const participants = useMemo(
    () => participantsQuery.data?.participants ?? [],
    [participantsQuery.data?.participants],
  )
  const teams = useMemo(
    () => participantsQuery.data?.teams ?? [],
    [participantsQuery.data?.teams],
  )
  const unassignedParticipants = useMemo(
    () => participants.filter((participant) => participant.isUnassigned),
    [participants],
  )

  const importMutation = useMutation({
    mutationFn: () => importCsvSignups(slug, csvText, adminToken),
    onSuccess: async (response) => {
      setLastImport(response)
      await invalidateSetupQueries(queryClient, slug)
    },
  })

  const createTeamMutation = useMutation({
    mutationFn: () => createEventTeam(slug, teamName, adminToken),
    onSuccess: async () => {
      setTeamName("")
      await invalidateSetupQueries(queryClient, slug)
    },
  })

  const assignTeamMutation = useMutation({
    mutationFn: (participant: AdminEventParticipant) => {
      const selectedValue = selectedTeams[participant.id]
      const teamId = selectedValue && selectedValue !== "none" ? Number(selectedValue) : null
      return assignParticipantTeam(slug, participant.id, teamId, adminToken)
    },
    onSuccess: async () => {
      await invalidateSetupQueries(queryClient, slug)
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
        <BrandBlock
          title={event?.name ?? "Event Setup"}
          subtitle={slug ? `Admin/testing roster setup for ${slug}` : "Admin/testing roster setup"}
        />
        <TopNav activeSlug={slug || undefined} />
      </header>

      <section className="grid gap-4 lg:grid-cols-[0.8fr_1.2fr]">
        <div className="flex flex-col gap-4">
          <AdminTokenCard adminToken={adminToken} onChange={saveToken} />
          <CsvImportCard
            csvText={csvText}
            disabled={!tokenReady || importMutation.isPending}
            error={importMutation.error}
            lastImport={lastImport}
            onCsvTextChange={setCsvText}
            onImport={() => importMutation.mutate()}
          />
          <TeamCreateCard
            teamName={teamName}
            disabled={!tokenReady || createTeamMutation.isPending}
            error={createTeamMutation.error}
            onTeamNameChange={setTeamName}
            onCreate={() => createTeamMutation.mutate()}
          />
        </div>

        <div className="flex flex-col gap-4">
          <SectionHeading
            title="Roster"
            description="Imported participants and manual team assignment"
          />
          {!tokenReady ? (
            <StateCard title="Admin token required" detail="Enter the local admin token to load roster data." />
          ) : participantsQuery.isLoading ? (
            <StateCard title="Loading roster" detail="Reading participants and teams." />
          ) : participantsQuery.isError ? (
            <StateCard title="Roster unavailable" detail={errorText(participantsQuery.error)} />
          ) : (
            <RosterCard
              participants={participants}
              selectedTeams={selectedTeams}
              teams={teams}
              unassignedCount={participantsQuery.data?.unassignedCount ?? 0}
              onSelectTeam={(participantId, teamId) =>
                setSelectedTeams((current) => ({ ...current, [participantId]: teamId }))
              }
              onAssign={(participant) => assignTeamMutation.mutate(participant)}
              assigningParticipantId={assignTeamMutation.variables?.id}
              isAssigning={assignTeamMutation.isPending}
            />
          )}

          <SectionHeading title="Signups" description="Event-scoped imported signup fields" />
          {!tokenReady ? null : signupsQuery.isLoading ? (
            <StateCard title="Loading signups" detail="Reading imported rows." />
          ) : signupsQuery.isError ? (
            <StateCard title="Signups unavailable" detail={errorText(signupsQuery.error)} />
          ) : (
            <SignupsCard signups={signupsQuery.data?.signups ?? []} />
          )}

          {unassignedParticipants.length > 0 ? (
            <UnassignedCard participants={unassignedParticipants} />
          ) : null}
        </div>
      </section>
    </AppFrame>
  )
}

function AdminTokenCard({
  adminToken,
  onChange,
}: {
  adminToken: string
  onChange: (value: string) => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Admin Token</CardTitle>
        <CardDescription>Stored in this browser for local admin/testing calls.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Token
          <Input
            value={adminToken}
            type="password"
            autoComplete="off"
            placeholder="dev-admin-token-change-me"
            onChange={(event) => onChange(event.target.value)}
          />
        </label>
      </CardContent>
    </Card>
  )
}

function CsvImportCard({
  csvText,
  disabled,
  error,
  lastImport,
  onCsvTextChange,
  onImport,
}: {
  csvText: string
  disabled: boolean
  error: Error | null
  lastImport: CsvSignupImportResponse | null
  onCsvTextChange: (value: string) => void
  onImport: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>CSV Import</CardTitle>
        <CardDescription>Paste a Google Forms-style signup export.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <Textarea
          value={csvText}
          className="min-h-44 font-mono text-xs"
          onChange={(event) => onCsvTextChange(event.target.value)}
        />
        <Button disabled={disabled} onClick={onImport}>
          <UploadIcon data-icon="inline-start" aria-hidden="true" />
          Import CSV
        </Button>
        {error ? <p className="text-sm text-destructive">{errorText(error)}</p> : null}
        {lastImport ? <ImportSummary response={lastImport} /> : null}
      </CardContent>
    </Card>
  )
}

function ImportSummary({ response }: { response: CsvSignupImportResponse }) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border bg-background/40 p-3">
      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{response.rowsRead} rows</Badge>
        <Badge variant="secondary">{response.signupsCreated} signups created</Badge>
        <Badge variant="secondary">{response.participantsCreated} participants created</Badge>
        <Badge variant="outline">{response.invalidRows} invalid</Badge>
      </div>
      {response.issues.length > 0 ? (
        <div className="flex flex-col gap-1 text-xs text-muted-foreground">
          {response.issues.map((issue) => (
            <p key={`${issue.rowNumber}-${issue.reason}`}>
              Row {issue.rowNumber}: {issue.reason}
            </p>
          ))}
        </div>
      ) : null}
    </div>
  )
}

function TeamCreateCard({
  teamName,
  disabled,
  error,
  onTeamNameChange,
  onCreate,
}: {
  teamName: string
  disabled: boolean
  error: Error | null
  onTeamNameChange: (value: string) => void
  onCreate: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Teams</CardTitle>
        <CardDescription>Create event-scoped teams before assignment.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Team name
          <Input
            value={teamName}
            placeholder="Blue"
            onChange={(event) => onTeamNameChange(event.target.value)}
          />
        </label>
        <Button disabled={disabled || teamName.trim().length === 0} onClick={onCreate}>
          <SaveIcon data-icon="inline-start" aria-hidden="true" />
          Create Team
        </Button>
        {error ? <p className="text-sm text-destructive">{errorText(error)}</p> : null}
      </CardContent>
    </Card>
  )
}

function RosterCard({
  participants,
  teams,
  selectedTeams,
  unassignedCount,
  assigningParticipantId,
  isAssigning,
  onSelectTeam,
  onAssign,
}: {
  participants: AdminEventParticipant[]
  teams: { id: number; name: string; participantCount: number }[]
  selectedTeams: Record<number, string>
  unassignedCount: number
  assigningParticipantId?: number
  isAssigning: boolean
  onSelectTeam: (participantId: number, teamId: string) => void
  onAssign: (participant: AdminEventParticipant) => void
}) {
  if (participants.length === 0) {
    return <StateCard title="No participants" detail="Import signups to create event participants." />
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex items-start justify-between gap-3">
          <div>
            <CardTitle>Participants</CardTitle>
            <CardDescription>{unassignedCount} without team assignment</CardDescription>
          </div>
          <Badge variant={unassignedCount > 0 ? "secondary" : "default"}>
            {participants.length} total
          </Badge>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {participants.map((participant, index) => (
          <div key={participant.id} className="flex flex-col gap-3">
            {index > 0 ? <Separator /> : null}
            <div className="grid gap-3 md:grid-cols-[1fr_0.8fr_auto] md:items-center">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">{participant.displayName}</p>
                <p className="text-xs text-muted-foreground">
                  {participant.runeScapeName} · {participant.status}
                </p>
              </div>
              <select
                className="h-8 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
                value={selectedTeams[participant.id] ?? participant.teamId?.toString() ?? "none"}
                onChange={(event) => onSelectTeam(participant.id, event.target.value)}
              >
                <option value="none">No team</option>
                {teams.map((team) => (
                  <option key={team.id} value={team.id}>
                    {team.name}
                  </option>
                ))}
              </select>
              <Button
                size="sm"
                variant="outline"
                disabled={isAssigning && assigningParticipantId === participant.id}
                onClick={() => onAssign(participant)}
              >
                Assign
              </Button>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function SignupsCard({
  signups,
}: {
  signups: {
    id: number
    runeScapeName: string
    availabilityText: string | null
    dailyHours: number | null
    preferredContent: string | null
    teamPreference: string | null
    notes: string | null
  }[]
}) {
  if (signups.length === 0) {
    return <StateCard title="No signups" detail="Imported signup rows will appear here." />
  }

  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        {signups.map((signup, index) => (
          <div key={signup.id} className="flex flex-col gap-3">
            {index > 0 ? <Separator /> : null}
            <div className="grid gap-2 md:grid-cols-[0.8fr_1.2fr]">
              <div className="min-w-0">
                <p className="truncate text-sm font-medium">{signup.runeScapeName}</p>
                <p className="text-xs text-muted-foreground">
                  {signup.teamPreference ?? "No team preference"}
                </p>
              </div>
              <div className="flex flex-wrap gap-2">
                {signup.availabilityText ? <Badge variant="outline">{signup.availabilityText}</Badge> : null}
                {signup.dailyHours ? <Badge variant="outline">{signup.dailyHours}h/day</Badge> : null}
                {signup.preferredContent ? <Badge variant="outline">{signup.preferredContent}</Badge> : null}
                {signup.notes ? <Badge variant="secondary">{signup.notes}</Badge> : null}
              </div>
            </div>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

function UnassignedCard({ participants }: { participants: AdminEventParticipant[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Unassigned</CardTitle>
        <CardDescription>Team-scoped events ignore these players until assigned.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-2">
        {participants.map((participant) => (
          <div key={participant.id} className="flex items-center gap-2 text-sm">
            <UsersIcon data-icon="inline-start" aria-hidden="true" />
            <span>{participant.displayName}</span>
          </div>
        ))}
      </CardContent>
    </Card>
  )
}

async function invalidateSetupQueries(
  queryClient: ReturnType<typeof useQueryClient>,
  slug: string,
) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ["admin-event-signups", slug] }),
    queryClient.invalidateQueries({ queryKey: ["admin-event-participants", slug] }),
  ])
}

function errorText(error: unknown) {
  return error instanceof Error ? error.message : "The request failed."
}
