import { useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  AlertTriangleIcon,
  DatabaseIcon,
  LayersIcon,
  LinkIcon,
  PlusIcon,
  RefreshCwIcon,
  SaveIcon,
  UploadIcon,
  UsersIcon,
} from "lucide-react"
import { useParams } from "react-router-dom"
import {
  assignParticipantTeam,
  createBingoBoard,
  createBingoTile,
  createBingoTileTier,
  createEventTeam,
  createTileRule,
  getAdminBoardSetup,
  importCsvSignups,
  linkTempleCompetition,
  listExternalCompetitionPlayerMetrics,
  listExternalCompetitions,
  listExternalCompetitionSyncRuns,
  listExternalCompetitionTeamMetrics,
  listExternalCompetitionUnmatchedIdentities,
  listAdminParticipants,
  listAdminSignups,
  syncExternalCompetition,
  type AdminExternalCompetition,
  type AdminExternalCompetitionPlayerMetric,
  type AdminExternalCompetitionSyncRun,
  type AdminExternalCompetitionTeamMetric,
  type AdminExternalCompetitionUnmatchedIdentity,
  type AdminBingoTile,
  type AdminEventParticipant,
  type AdminTileRule,
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
import { formatNumber, formatTimestamp } from "@/components/event/event-format"
import { cn } from "@/lib/utils"

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
  const [templeCompetitionId, setTempleCompetitionId] = useState("")
  const [selectedCompetitionId, setSelectedCompetitionId] = useState<number | null>(null)
  const [selectedTeams, setSelectedTeams] = useState<Record<number, string>>({})
  const [lastImport, setLastImport] = useState<CsvSignupImportResponse | null>(null)
  const [boardName, setBoardName] = useState("Bingo Board")
  const [tileTitle, setTileTitle] = useState("")
  const [tileDescription, setTileDescription] = useState("")
  const [selectedTileId, setSelectedTileId] = useState<number | null>(null)
  const [tierTitle, setTierTitle] = useState("")
  const [tierNumber, setTierNumber] = useState("1")
  const [tierTarget, setTierTarget] = useState("1")
  const [ruleType, setRuleType] = useState("item_count")
  const [ruleScope, setRuleScope] = useState("team")
  const [ruleConfigJson, setRuleConfigJson] = useState(defaultRuleConfig("item_count", "1"))
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
  const competitionsQuery = useQuery({
    queryKey: ["admin-external-competitions", slug, tokenReady],
    queryFn: ({ signal }) => listExternalCompetitions(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
  })
  const boardSetupQuery = useQuery({
    queryKey: ["admin-board-setup", slug, tokenReady],
    queryFn: ({ signal }) => getAdminBoardSetup(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
  })
  const selectedCompetition =
    competitionsQuery.data?.competitions.find((competition) => competition.id === selectedCompetitionId) ??
    competitionsQuery.data?.competitions[0] ??
    null
  const effectiveCompetitionId = selectedCompetition?.id ?? null
  const syncRunsQuery = useQuery({
    queryKey: ["admin-external-competition-sync-runs", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionSyncRuns(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
  })
  const playerMetricsQuery = useQuery({
    queryKey: ["admin-external-competition-player-metrics", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionPlayerMetrics(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
  })
  const teamMetricsQuery = useQuery({
    queryKey: ["admin-external-competition-team-metrics", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionTeamMetrics(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
  })
  const unmatchedQuery = useQuery({
    queryKey: ["admin-external-competition-unmatched", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionUnmatchedIdentities(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
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
  const linkTempleMutation = useMutation({
    mutationFn: () => linkTempleCompetition(slug, templeCompetitionId, adminToken),
    onSuccess: async (competition) => {
      setTempleCompetitionId("")
      setSelectedCompetitionId(competition.id)
      await invalidateExternalCompetitionQueries(queryClient, slug, competition.id)
    },
  })
  const createBoardMutation = useMutation({
    mutationFn: () => createBingoBoard(slug, { name: boardName, rows: 5, columns: 5 }, adminToken),
    onSuccess: async () => {
      await invalidateBoardSetupQueries(queryClient, slug)
    },
  })
  const createTileMutation = useMutation({
    mutationFn: () =>
      createBingoTile(
        slug,
        boardSetupQuery.data?.board?.id ?? 0,
        {
          title: tileTitle,
          description: tileDescription.trim().length > 0 ? tileDescription : null,
          positionX: null,
          positionY: null,
          sortOrder: boardSetupQuery.data?.tiles.length ?? 0,
        },
        adminToken,
      ),
    onSuccess: async (tile) => {
      setTileTitle("")
      setTileDescription("")
      setSelectedTileId(tile.id)
      await invalidateBoardSetupQueries(queryClient, slug)
    },
  })
  const createTierMutation = useMutation({
    mutationFn: () =>
      createBingoTileTier(
        slug,
        selectedTileId ?? boardSetupQuery.data?.tiles[0]?.id ?? 0,
        {
          tierNumber: numberOrDefault(tierNumber, 1),
          title: tierTitle.trim().length > 0 ? tierTitle : null,
          description: null,
          scoreValue: 1,
          isRequiredForBoardCompletion: true,
          sortOrder: numberOrDefault(tierNumber, 1),
        },
        adminToken,
      ),
    onSuccess: async () => {
      setTierTitle("")
      setRuleConfigJson(defaultRuleConfig(ruleType, tierTarget))
      await invalidateBoardSetupQueries(queryClient, slug)
    },
  })
  const createRuleMutation = useMutation({
    mutationFn: () =>
      createTileRule(
        slug,
        selectedTileId ?? boardSetupQuery.data?.tiles[0]?.id ?? 0,
        {
          tileTierId: selectedTileTierId(boardSetupQuery.data?.tiles ?? [], selectedTileId),
          ruleType,
          scope: ruleScope,
          isActive: true,
          configJson: ruleConfigJson,
        },
        adminToken,
      ),
    onSuccess: async () => {
      await invalidateBoardSetupQueries(queryClient, slug)
    },
  })
  const syncTempleMutation = useMutation({
    mutationFn: (competition: AdminExternalCompetition) =>
      syncExternalCompetition(slug, competition.id, adminToken),
    onSuccess: async (_run, competition) => {
      await invalidateExternalCompetitionQueries(queryClient, slug, competition.id)
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
          <TempleLinkCard
            competitionId={templeCompetitionId}
            disabled={!tokenReady || linkTempleMutation.isPending}
            error={linkTempleMutation.error}
            onCompetitionIdChange={setTempleCompetitionId}
            onLink={() => linkTempleMutation.mutate()}
          />
          <BoardSetupActionsCard
            boardName={boardName}
            tileTitle={tileTitle}
            tileDescription={tileDescription}
            tierNumber={tierNumber}
            tierTitle={tierTitle}
            tierTarget={tierTarget}
            ruleType={ruleType}
            ruleScope={ruleScope}
            ruleConfigJson={ruleConfigJson}
            selectedTileId={selectedTileId}
            tiles={boardSetupQuery.data?.tiles ?? []}
            hasBoard={Boolean(boardSetupQuery.data?.board)}
            disabled={!tokenReady}
            errors={[
              createBoardMutation.error,
              createTileMutation.error,
              createTierMutation.error,
              createRuleMutation.error,
            ]}
            onBoardNameChange={setBoardName}
            onTileTitleChange={setTileTitle}
            onTileDescriptionChange={setTileDescription}
            onTierNumberChange={setTierNumber}
            onTierTitleChange={setTierTitle}
            onTierTargetChange={(value) => {
              setTierTarget(value)
              setRuleConfigJson(defaultRuleConfig(ruleType, value))
            }}
            onRuleTypeChange={(value) => {
              setRuleType(value)
              setRuleConfigJson(defaultRuleConfig(value, tierTarget))
            }}
            onRuleScopeChange={setRuleScope}
            onRuleConfigJsonChange={setRuleConfigJson}
            onSelectedTileChange={setSelectedTileId}
            onCreateBoard={() => createBoardMutation.mutate()}
            onCreateTile={() => createTileMutation.mutate()}
            onCreateTier={() => createTierMutation.mutate()}
            onCreateRule={() => createRuleMutation.mutate()}
            isSaving={
              createBoardMutation.isPending ||
              createTileMutation.isPending ||
              createTierMutation.isPending ||
              createRuleMutation.isPending
            }
          />
        </div>

        <div className="flex flex-col gap-4">
          <SectionHeading
            title="Roster"
            description="Local testing roster controls"
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

          <SectionHeading title="Board Setup" description="Manual tile, tier, and rule configuration" />
          {!tokenReady ? null : boardSetupQuery.isLoading ? (
            <StateCard title="Loading board setup" detail="Reading board, tiles, tiers, and rules." />
          ) : boardSetupQuery.isError ? (
            <StateCard title="Board setup unavailable" detail={errorText(boardSetupQuery.error)} />
          ) : (
            <BoardSetupSummaryCard
              boardName={boardSetupQuery.data?.board?.name ?? null}
              tiles={boardSetupQuery.data?.tiles ?? []}
            />
          )}

          <SectionHeading
            title="TempleOSRS"
            description="Read-only cache status for linked competitions"
          />
          {!tokenReady ? null : competitionsQuery.isLoading ? (
            <StateCard title="Loading Temple links" detail="Reading linked competitions." />
          ) : competitionsQuery.isError ? (
            <StateCard title="Temple links unavailable" detail={errorText(competitionsQuery.error)} />
          ) : (
            <TempleDiagnosticsCard
              competitions={competitionsQuery.data?.competitions ?? []}
              selectedCompetitionId={effectiveCompetitionId}
              syncRuns={syncRunsQuery.data?.runs ?? []}
              playerMetrics={playerMetricsQuery.data?.metrics ?? []}
              teamMetrics={teamMetricsQuery.data?.metrics ?? []}
              unmatchedIdentities={unmatchedQuery.data?.identities ?? []}
              isSyncing={syncTempleMutation.isPending}
              syncError={syncTempleMutation.error}
              onSelectCompetition={setSelectedCompetitionId}
              onSync={(competition) => syncTempleMutation.mutate(competition)}
            />
          )}
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
        <CardDescription>Local development/testing only. Stored in this browser.</CardDescription>
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
        <CardDescription>Paste signup rows to create participants for this event.</CardDescription>
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
        <CardDescription>Create teams first, then assign imported participants.</CardDescription>
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

function TempleLinkCard({
  competitionId,
  disabled,
  error,
  onCompetitionIdChange,
  onLink,
}: {
  competitionId: string
  disabled: boolean
  error: Error | null
  onCompetitionIdChange: (value: string) => void
  onLink: () => void
}) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Temple Link</CardTitle>
        <CardDescription>Attach an existing TempleOSRS competition by ID for read-only sync.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Competition ID
          <Input
            value={competitionId}
            inputMode="numeric"
            placeholder="12345"
            onChange={(event) => onCompetitionIdChange(event.target.value)}
          />
        </label>
        <Button disabled={disabled || competitionId.trim().length === 0} onClick={onLink}>
          <LinkIcon data-icon="inline-start" aria-hidden="true" />
          Link Temple
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
          <div className="flex flex-wrap justify-end gap-2">
            {unassignedCount > 0 ? <Badge variant="secondary">{unassignedCount} unassigned</Badge> : null}
            <Badge variant={unassignedCount > 0 ? "outline" : "default"}>{participants.length} total</Badge>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {participants.map((participant, index) => (
          <div key={participant.id} className="flex flex-col gap-3">
            {index > 0 ? <Separator /> : null}
            <div
              className={cn(
                "grid gap-3 rounded-md p-2 md:grid-cols-[1fr_0.8fr_auto] md:items-center",
                participant.isUnassigned ? "bg-accent/10 ring-1 ring-accent/30" : "bg-transparent",
              )}
            >
              <div className="min-w-0">
                <div className="flex min-w-0 items-center gap-2">
                  <p className="truncate text-sm font-medium">{participant.displayName}</p>
                  {participant.isUnassigned ? <Badge variant="secondary">needs team</Badge> : null}
                </div>
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
        <CardDescription>These players will not count for team-scoped scoring until assigned.</CardDescription>
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

function BoardSetupActionsCard({
  boardName,
  tileTitle,
  tileDescription,
  tierNumber,
  tierTitle,
  tierTarget,
  ruleType,
  ruleScope,
  ruleConfigJson,
  selectedTileId,
  tiles,
  hasBoard,
  disabled,
  errors,
  isSaving,
  onBoardNameChange,
  onTileTitleChange,
  onTileDescriptionChange,
  onTierNumberChange,
  onTierTitleChange,
  onTierTargetChange,
  onRuleTypeChange,
  onRuleScopeChange,
  onRuleConfigJsonChange,
  onSelectedTileChange,
  onCreateBoard,
  onCreateTile,
  onCreateTier,
  onCreateRule,
}: {
  boardName: string
  tileTitle: string
  tileDescription: string
  tierNumber: string
  tierTitle: string
  tierTarget: string
  ruleType: string
  ruleScope: string
  ruleConfigJson: string
  selectedTileId: number | null
  tiles: AdminBingoTile[]
  hasBoard: boolean
  disabled: boolean
  errors: (Error | null)[]
  isSaving: boolean
  onBoardNameChange: (value: string) => void
  onTileTitleChange: (value: string) => void
  onTileDescriptionChange: (value: string) => void
  onTierNumberChange: (value: string) => void
  onTierTitleChange: (value: string) => void
  onTierTargetChange: (value: string) => void
  onRuleTypeChange: (value: string) => void
  onRuleScopeChange: (value: string) => void
  onRuleConfigJsonChange: (value: string) => void
  onSelectedTileChange: (value: number | null) => void
  onCreateBoard: () => void
  onCreateTile: () => void
  onCreateTier: () => void
  onCreateRule: () => void
}) {
  const selectedTile = tiles.find((tile) => tile.id === selectedTileId) ?? tiles[0] ?? null
  const firstError = errors.find(Boolean)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Board Builder</CardTitle>
        <CardDescription>Manual MVP setup. Rule config is validated JSON.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid gap-3 sm:grid-cols-[1fr_auto] sm:items-end">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Board name
            <Input value={boardName} onChange={(event) => onBoardNameChange(event.target.value)} />
          </label>
          <Button disabled={disabled || isSaving || boardName.trim().length === 0} onClick={onCreateBoard}>
            <LayersIcon data-icon="inline-start" aria-hidden="true" />
            {hasBoard ? "Update Board" : "Create Board"}
          </Button>
        </div>

        <Separator />

        <div className="grid gap-3">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tile title
            <Input
              value={tileTitle}
              placeholder="Scythe drops"
              onChange={(event) => onTileTitleChange(event.target.value)}
            />
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tile description
            <Input
              value={tileDescription}
              placeholder="Optional public description"
              onChange={(event) => onTileDescriptionChange(event.target.value)}
            />
          </label>
          <Button disabled={disabled || isSaving || !hasBoard || tileTitle.trim().length === 0} onClick={onCreateTile}>
            <PlusIcon data-icon="inline-start" aria-hidden="true" />
            Add Tile
          </Button>
        </div>

        <Separator />

        <label className="flex flex-col gap-2 text-sm font-medium">
          Selected tile
          <select
            className="h-9 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            value={selectedTile?.id ?? ""}
            onChange={(event) => onSelectedTileChange(event.target.value ? Number(event.target.value) : null)}
          >
            <option value="">Choose tile</option>
            {tiles.map((tile) => (
              <option key={tile.id} value={tile.id}>
                {tile.title}
              </option>
            ))}
          </select>
        </label>

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tier number
            <Input value={tierNumber} inputMode="numeric" onChange={(event) => onTierNumberChange(event.target.value)} />
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tier target
            <Input value={tierTarget} inputMode="numeric" onChange={(event) => onTierTargetChange(event.target.value)} />
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tier title
            <Input
              value={tierTitle}
              placeholder="Tier 1"
              onChange={(event) => onTierTitleChange(event.target.value)}
            />
          </label>
        </div>
        <Button disabled={disabled || isSaving || !selectedTile} onClick={onCreateTier}>
          <PlusIcon data-icon="inline-start" aria-hidden="true" />
          Add Tier
        </Button>

        <Separator />

        <div className="grid gap-3 sm:grid-cols-2">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Rule type
            <select
              className="h-9 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              value={ruleType}
              onChange={(event) => onRuleTypeChange(event.target.value)}
            >
              <option value="item_count">item_count</option>
              <option value="point_threshold">point_threshold</option>
              <option value="external_competition_metric">external_competition_metric</option>
              <option value="manual">manual</option>
            </select>
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Scope
            <select
              className="h-9 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
              value={ruleScope}
              onChange={(event) => onRuleScopeChange(event.target.value)}
            >
              <option value="team">team</option>
              <option value="player">player</option>
              <option value="event">event</option>
            </select>
          </label>
        </div>
        <label className="flex flex-col gap-2 text-sm font-medium">
          Rule config JSON
          <Textarea
            value={ruleConfigJson}
            className="min-h-36 font-mono text-xs"
            onChange={(event) => onRuleConfigJsonChange(event.target.value)}
          />
        </label>
        <Button disabled={disabled || isSaving || !selectedTile} onClick={onCreateRule}>
          <SaveIcon data-icon="inline-start" aria-hidden="true" />
          Create Rule
        </Button>
        {firstError ? <p className="text-sm text-destructive">{errorText(firstError)}</p> : null}
      </CardContent>
    </Card>
  )
}

function BoardSetupSummaryCard({
  boardName,
  tiles,
}: {
  boardName: string | null
  tiles: AdminBingoTile[]
}) {
  if (!boardName) {
    return <StateCard title="No board yet" detail="Create a board, then add tiles, tiers, and rules." />
  }

  return (
    <Card>
      <CardHeader>
        <CardTitle>{boardName}</CardTitle>
        <CardDescription>{tiles.length} tile(s) configured</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        {tiles.length === 0 ? (
          <p className="text-sm text-muted-foreground">Tiles will appear here after you add them.</p>
        ) : (
          tiles.map((tile, index) => (
            <div key={tile.id} className="flex flex-col gap-3">
              {index > 0 ? <Separator /> : null}
              <div className="grid gap-3 md:grid-cols-[1fr_auto] md:items-start">
                <div className="min-w-0">
                  <p className="truncate text-sm font-medium">{tile.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {tile.tiers.length} tier(s) · {tile.rules.length} rule(s)
                  </p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {tile.tiers.map((tier) => (
                      <Badge key={tier.id} variant="outline">
                        {tier.title ?? `Tier ${tier.tierNumber}`}
                      </Badge>
                    ))}
                  </div>
                </div>
                <div className="flex flex-col gap-1 text-xs text-muted-foreground">
                  {tile.rules.length === 0 ? (
                    <span>No rules</span>
                  ) : (
                    tile.rules.map((rule: AdminTileRule) => (
                      <span key={rule.id}>
                        {rule.ruleType} · {rule.scope}
                      </span>
                    ))
                  )}
                </div>
              </div>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  )
}

function TempleDiagnosticsCard({
  competitions,
  selectedCompetitionId,
  syncRuns,
  playerMetrics,
  teamMetrics,
  unmatchedIdentities,
  isSyncing,
  syncError,
  onSelectCompetition,
  onSync,
}: {
  competitions: AdminExternalCompetition[]
  selectedCompetitionId: number | null
  syncRuns: AdminExternalCompetitionSyncRun[]
  playerMetrics: AdminExternalCompetitionPlayerMetric[]
  teamMetrics: AdminExternalCompetitionTeamMetric[]
  unmatchedIdentities: AdminExternalCompetitionUnmatchedIdentity[]
  isSyncing: boolean
  syncError: Error | null
  onSelectCompetition: (competitionId: number) => void
  onSync: (competition: AdminExternalCompetition) => void
}) {
  const selectedCompetition =
    competitions.find((competition) => competition.id === selectedCompetitionId) ?? competitions[0]
  const mismatches = teamMetrics.filter((metric) => metric.hasLocalTeamMismatch)

  if (competitions.length === 0) {
    return <StateCard title="No Temple links" detail="Link a competition ID to enable read-only sync." />
  }

  return (
    <Card>
      <CardHeader>
        <div className="flex flex-col gap-3 md:flex-row md:items-start md:justify-between">
          <div className="min-w-0">
            <CardTitle>Temple Cache</CardTitle>
            <CardDescription>
              {selectedCompetition.name} · {selectedCompetition.competitionMode}
            </CardDescription>
          </div>
          <Button
            size="sm"
            disabled={isSyncing}
            onClick={() => onSync(selectedCompetition)}
          >
            <RefreshCwIcon data-icon="inline-start" aria-hidden="true" />
            Sync cache
          </Button>
        </div>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <div className="grid gap-3 md:grid-cols-[1fr_auto] md:items-center">
          <select
            className="h-9 rounded-lg border border-input bg-transparent px-2.5 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50"
            value={selectedCompetition.id}
            onChange={(event) => onSelectCompetition(Number(event.target.value))}
          >
            {competitions.map((competition) => (
              <option key={competition.id} value={competition.id}>
                {competition.name} ({competition.externalId})
              </option>
            ))}
          </select>
          <Badge variant={selectedCompetition.lastSyncStatus === "succeeded" ? "default" : "secondary"}>
            {selectedCompetition.lastSyncStatus ?? "unsynced"}
          </Badge>
        </div>
        <div className="grid gap-2 text-sm md:grid-cols-3">
          <MetricPill label="Last success" value={formatNullableTime(selectedCompetition.lastSuccessfulSyncAt)} />
          <MetricPill label="Cached players" value={playerMetrics.length.toString()} />
          <MetricPill label="Cached teams" value={teamMetrics.length.toString()} />
        </div>
        {syncError ? <p className="text-sm text-destructive">{errorText(syncError)}</p> : null}
        {selectedCompetition.lastSyncError ? (
          <p className="text-sm text-destructive">{selectedCompetition.lastSyncError}</p>
        ) : null}
        {mismatches.length > 0 ? <MismatchList metrics={mismatches} /> : null}
        <TempleMetricPreview
          syncRuns={syncRuns}
          playerMetrics={playerMetrics}
          teamMetrics={teamMetrics}
          unmatchedIdentities={unmatchedIdentities}
        />
      </CardContent>
    </Card>
  )
}

function MetricPill({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-lg border bg-background/40 p-3">
      <p className="text-xs text-muted-foreground">{label}</p>
      <p className="truncate text-sm font-medium">{value}</p>
    </div>
  )
}

function MismatchList({ metrics }: { metrics: AdminExternalCompetitionTeamMetric[] }) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border border-accent/50 bg-accent/10 p-3">
      {metrics.map((metric) => (
        <div key={metric.id} className="flex items-center gap-2 text-sm">
          <AlertTriangleIcon data-icon="inline-start" aria-hidden="true" />
          <span className="min-w-0 truncate">
            {metric.teamName} has no matching local team
          </span>
        </div>
      ))}
    </div>
  )
}

function TempleMetricPreview({
  syncRuns,
  playerMetrics,
  teamMetrics,
  unmatchedIdentities,
}: {
  syncRuns: AdminExternalCompetitionSyncRun[]
  playerMetrics: AdminExternalCompetitionPlayerMetric[]
  teamMetrics: AdminExternalCompetitionTeamMetric[]
  unmatchedIdentities: AdminExternalCompetitionUnmatchedIdentity[]
}) {
  return (
    <div className="grid gap-3 xl:grid-cols-2">
      <PreviewList
        title="Player Metrics"
        rows={playerMetrics.slice(0, 6).map((metric) => ({
          id: metric.id,
          primary: metric.runeScapeName,
          secondary: metric.localPlayerName ?? "unmatched",
          value: formatNumber(metric.gainedValue),
        }))}
      />
      <PreviewList
        title="Team Metrics"
        rows={teamMetrics.slice(0, 6).map((metric) => ({
          id: metric.id,
          primary: metric.teamName,
          secondary: metric.localTeamName ?? "no local team",
          value: formatNumber(metric.gainedValue),
        }))}
      />
      <PreviewList
        title="Unmatched"
        rows={unmatchedIdentities.slice(0, 6).map((identity) => ({
          id: identity.id,
          primary: identity.displayName,
          secondary: formatTimestamp(identity.lastSeenAt),
          value: "review",
        }))}
      />
      <PreviewList
        title="Sync Runs"
        rows={syncRuns.slice(0, 6).map((run) => ({
          id: run.id,
          primary: run.status,
          secondary: formatTimestamp(run.startedAt),
          value: run.rowsRead?.toString() ?? "0",
        }))}
      />
    </div>
  )
}

function PreviewList({
  title,
  rows,
}: {
  title: string
  rows: { id: number; primary: string; secondary: string; value: string }[]
}) {
  return (
    <div className="flex flex-col gap-2 rounded-lg border bg-background/40 p-3">
      <div className="flex items-center gap-2 text-sm font-medium">
        <DatabaseIcon data-icon="inline-start" aria-hidden="true" />
        {title}
      </div>
      {rows.length === 0 ? (
        <p className="text-xs text-muted-foreground">No cached rows</p>
      ) : (
        rows.map((row) => (
          <div key={row.id} className="grid grid-cols-[1fr_auto] gap-3 text-xs">
            <div className="min-w-0">
              <p className="truncate font-medium">{row.primary}</p>
              <p className="truncate text-muted-foreground">{row.secondary}</p>
            </div>
            <span className="font-mono text-muted-foreground">{row.value}</span>
          </div>
        ))
      )}
    </div>
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

async function invalidateBoardSetupQueries(
  queryClient: ReturnType<typeof useQueryClient>,
  slug: string,
) {
  await queryClient.invalidateQueries({ queryKey: ["admin-board-setup", slug] })
}

async function invalidateExternalCompetitionQueries(
  queryClient: ReturnType<typeof useQueryClient>,
  slug: string,
  competitionId: number,
) {
  await Promise.all([
    queryClient.invalidateQueries({ queryKey: ["admin-external-competitions", slug] }),
    queryClient.invalidateQueries({ queryKey: ["admin-external-competition-sync-runs", competitionId] }),
    queryClient.invalidateQueries({ queryKey: ["admin-external-competition-player-metrics", competitionId] }),
    queryClient.invalidateQueries({ queryKey: ["admin-external-competition-team-metrics", competitionId] }),
    queryClient.invalidateQueries({ queryKey: ["admin-external-competition-unmatched", competitionId] }),
  ])
}

function formatNullableTime(value: string | null) {
  return value ? formatTimestamp(value) : "Never"
}

function selectedTileTierId(tiles: AdminBingoTile[], selectedTileId: number | null) {
  const tile = tiles.find((candidate) => candidate.id === selectedTileId) ?? tiles[0]
  return tile?.tiers[0]?.id ?? null
}

function numberOrDefault(value: string, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

function defaultRuleConfig(ruleType: string, target: string, externalCompetitionId?: number) {
  const requiredValue = numberOrDefault(target, 1)
  const configs: Record<string, unknown> = {
    item_count: {
      activityType: "item_drop",
      itemIds: [22486],
      requiredValue,
      duplicatesCount: true,
    },
    point_threshold: {
      activityType: "item_drop",
      pointsTable: [
        {
          itemId: 22486,
          points: requiredValue,
        },
      ],
      requiredValue,
    },
    external_competition_metric: {
      provider: "templeosrs",
      metricType: "xp",
      metricKey: "overall",
      requiredValue,
      ...(externalCompetitionId ? { externalCompetitionId } : {}),
    },
    manual: {
      requiredValue,
    },
  }

  return JSON.stringify(configs[ruleType] ?? configs.item_count, null, 2)
}

function errorText(error: unknown) {
  return error instanceof Error ? error.message : "The request failed."
}
