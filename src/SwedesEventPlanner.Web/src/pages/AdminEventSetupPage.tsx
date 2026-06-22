import { useMemo, useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import {
  AlertTriangleIcon,
  CheckCircleIcon,
  DatabaseIcon,
  LayersIcon,
  LinkIcon,
  PlusIcon,
  RefreshCwIcon,
  SaveIcon,
  Trash2Icon,
  UploadIcon,
  UsersIcon,
} from "lucide-react"
import { useLocation, useParams } from "react-router-dom"
import {
  assignParticipantTeam,
  createBingoBoard,
  createBingoTile,
  createBingoTileTier,
  createEventTeam,
  createTileRule,
  deleteBingoTile,
  deleteBingoTileTier,
  deleteTileRule,
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
  type AdminEventTeamRoster,
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
const selectControlClassName =
  "h-9 rounded-lg border border-input bg-background px-2.5 text-sm text-foreground outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 [&>option]:bg-background [&>option]:text-foreground"
const compactSelectControlClassName =
  "h-8 rounded-lg border border-input bg-background px-2.5 text-sm text-foreground outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50 [&>option]:bg-background [&>option]:text-foreground"
const tierNumberOptions = [1, 2, 3, 4, 5]
const activityTypeOptions = [
  { value: "item_drop", label: "Item drop" },
  { value: "collection_log_entry", label: "Collection log entry" },
  { value: "xp_snapshot", label: "XP snapshot" },
  { value: "collection_log_snapshot", label: "Collection log snapshot" },
]

type ActionMessage = {
  type: "success" | "error"
  text: string
}

export function AdminEventSetupPage() {
  const { eventSlug } = useParams()
  const location = useLocation()
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
  const [selectedRuleTierId, setSelectedRuleTierId] = useState<number | null>(null)
  const [tierTitle, setTierTitle] = useState("")
  const [tierNumber, setTierNumber] = useState("1")
  const [tierTarget, setTierTarget] = useState("1")
  const [ruleType, setRuleType] = useState("item_count")
  const [ruleScope, setRuleScope] = useState("team")
  const [ruleConfigJson, setRuleConfigJson] = useState(defaultRuleConfig("item_count", "1"))
  const [useAdvancedRuleConfig, setUseAdvancedRuleConfig] = useState(false)
  const [itemActivityType, setItemActivityType] = useState("item_drop")
  const [itemIds, setItemIds] = useState("22486")
  const [duplicatesCount, setDuplicatesCount] = useState(true)
  const [pointActivityType, setPointActivityType] = useState("item_drop")
  const [pointRows, setPointRows] = useState("22486,10")
  const [externalCompetitionId, setExternalCompetitionId] = useState("")
  const [externalMetricType, setExternalMetricType] = useState("xp")
  const [externalMetricKey, setExternalMetricKey] = useState("overall")
  const [actionMessage, setActionMessage] = useState<ActionMessage | null>(() =>
    readNavigationActionMessage(location.state),
  )
  const tokenReady = adminToken.trim().length > 0

  const signupsQuery = useQuery({
    queryKey: ["admin-event-signups", slug, tokenReady],
    queryFn: ({ signal }) => listAdminSignups(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
    refetchInterval: 10000,
  })
  const participantsQuery = useQuery({
    queryKey: ["admin-event-participants", slug, tokenReady],
    queryFn: ({ signal }) => listAdminParticipants(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
    refetchInterval: 10000,
  })
  const competitionsQuery = useQuery({
    queryKey: ["admin-external-competitions", slug, tokenReady],
    queryFn: ({ signal }) => listExternalCompetitions(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
    refetchInterval: 10000,
  })
  const boardSetupQuery = useQuery({
    queryKey: ["admin-board-setup", slug, tokenReady],
    queryFn: ({ signal }) => getAdminBoardSetup(slug, adminToken, signal),
    enabled: slug.length > 0 && tokenReady,
    retry: false,
    refetchInterval: 10000,
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
    refetchInterval: 5000,
  })
  const playerMetricsQuery = useQuery({
    queryKey: ["admin-external-competition-player-metrics", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionPlayerMetrics(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
    refetchInterval: 10000,
  })
  const teamMetricsQuery = useQuery({
    queryKey: ["admin-external-competition-team-metrics", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionTeamMetrics(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
    refetchInterval: 10000,
  })
  const unmatchedQuery = useQuery({
    queryKey: ["admin-external-competition-unmatched", effectiveCompetitionId, tokenReady],
    queryFn: ({ signal }) =>
      listExternalCompetitionUnmatchedIdentities(effectiveCompetitionId ?? 0, adminToken, signal),
    enabled: tokenReady && effectiveCompetitionId !== null,
    retry: false,
    refetchInterval: 10000,
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
  const teamGroups = participantsQuery.data?.teamGroups ?? []

  const importMutation = useMutation({
    mutationFn: () => importCsvSignups(slug, csvText, adminToken),
    onSuccess: async (response) => {
      setLastImport(response)
      setActionMessage({
        type: "success",
        text: `CSV imported: ${response.participantsCreated} participant(s) created, ${response.participantsUpdated} updated.`,
      })
      await invalidateSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })

  const createTeamMutation = useMutation({
    mutationFn: () => createEventTeam(slug, teamName, adminToken),
    onSuccess: async (team) => {
      setTeamName("")
      setActionMessage({ type: "success", text: `Team created: ${team.name}.` })
      await invalidateSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })

  const assignTeamMutation = useMutation({
    mutationFn: (participant: AdminEventParticipant) => {
      const selectedValue = selectedTeams[participant.id]
      const teamId = selectedValue && selectedValue !== "none" ? Number(selectedValue) : null
      return assignParticipantTeam(slug, participant.id, teamId, adminToken)
    },
    onSuccess: async () => {
      setActionMessage({ type: "success", text: "Participant team assignment updated." })
      await invalidateSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const linkTempleMutation = useMutation({
    mutationFn: () => linkTempleCompetition(slug, templeCompetitionId, adminToken),
    onSuccess: async (competition) => {
      setTempleCompetitionId("")
      setSelectedCompetitionId(competition.id)
      setExternalCompetitionId(competition.id.toString())
      setActionMessage({ type: "success", text: `Temple competition linked: ${competition.name}.` })
      await invalidateExternalCompetitionQueries(queryClient, slug, competition.id)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const createBoardMutation = useMutation({
    mutationFn: () => createBingoBoard(slug, { name: boardName, rows: 5, columns: 5 }, adminToken),
    onSuccess: async (board) => {
      setActionMessage({ type: "success", text: `Board saved: ${board.name}.` })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
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
      setSelectedRuleTierId(null)
      setActionMessage({ type: "success", text: `Tile created: ${tile.title}.` })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
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
    onSuccess: async (tier) => {
      setTierTitle("")
      setSelectedRuleTierId(tier.id)
      setRuleConfigJson(defaultRuleConfig(ruleType, tierTarget))
      setActionMessage({ type: "success", text: "Tier created." })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const createRuleMutation = useMutation({
    mutationFn: () =>
      createTileRule(
        slug,
        selectedTileId ?? boardSetupQuery.data?.tiles[0]?.id ?? 0,
        {
          tileTierId: selectedTileTierId(boardSetupQuery.data?.tiles ?? [], selectedTileId, selectedRuleTierId),
          ruleType,
          scope: ruleScope,
          isActive: true,
          configJson: buildRuleConfigJson({
            ruleType,
            useAdvancedRuleConfig,
            ruleConfigJson,
            tierTarget,
            itemActivityType,
            itemIds,
            duplicatesCount,
            pointActivityType,
            pointRows,
            externalCompetitionId,
            externalMetricType,
            externalMetricKey,
          }),
        },
        adminToken,
      ),
    onSuccess: async () => {
      setActionMessage({ type: "success", text: "Rule created." })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const deleteTileMutation = useMutation({
    mutationFn: (tile: AdminBingoTile) => deleteBingoTile(slug, tile.id, adminToken),
    onSuccess: async (_result, tile) => {
      setSelectedTileId(null)
      setActionMessage({ type: "success", text: `Tile deleted: ${tile.title}.` })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const deleteTierMutation = useMutation({
    mutationFn: ({ tile, tierId }: { tile: AdminBingoTile; tierId: number }) =>
      deleteBingoTileTier(slug, tile.id, tierId, adminToken),
    onSuccess: async () => {
      setActionMessage({ type: "success", text: "Tier deleted." })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const deleteRuleMutation = useMutation({
    mutationFn: ({ tile, ruleId }: { tile: AdminBingoTile; ruleId: number }) =>
      deleteTileRule(slug, tile.id, ruleId, adminToken),
    onSuccess: async () => {
      setActionMessage({ type: "success", text: "Rule deleted." })
      await invalidateBoardSetupQueries(queryClient, slug)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
  })
  const syncTempleMutation = useMutation({
    mutationFn: (competition: AdminExternalCompetition) =>
      syncExternalCompetition(slug, competition.id, adminToken),
    onSuccess: async (_run, competition) => {
      setActionMessage({
        type: _run.status === "failed" ? "error" : "success",
        text: _run.status === "failed" ? "Temple sync failed." : `Temple sync ${_run.status}.`,
      })
      await invalidateExternalCompetitionQueries(queryClient, slug, competition.id)
    },
    onError: (error) => setActionMessage({ type: "error", text: errorText(error) }),
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

      {actionMessage ? (
        <ActionMessageCard message={actionMessage} onDismiss={() => setActionMessage(null)} />
      ) : null}

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
            useAdvancedRuleConfig={useAdvancedRuleConfig}
            itemActivityType={itemActivityType}
            itemIds={itemIds}
            duplicatesCount={duplicatesCount}
            pointActivityType={pointActivityType}
            pointRows={pointRows}
            externalCompetitionId={externalCompetitionId}
            externalMetricType={externalMetricType}
            externalMetricKey={externalMetricKey}
            selectedTileId={selectedTileId}
            selectedRuleTierId={selectedRuleTierId}
            tiles={boardSetupQuery.data?.tiles ?? []}
            linkedCompetitions={competitionsQuery.data?.competitions ?? []}
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
            onUseAdvancedRuleConfigChange={setUseAdvancedRuleConfig}
            onItemActivityTypeChange={setItemActivityType}
            onItemIdsChange={setItemIds}
            onDuplicatesCountChange={setDuplicatesCount}
            onPointActivityTypeChange={setPointActivityType}
            onPointRowsChange={setPointRows}
            onExternalCompetitionIdChange={setExternalCompetitionId}
            onExternalMetricTypeChange={setExternalMetricType}
            onExternalMetricKeyChange={setExternalMetricKey}
            onSelectedTileChange={(value) => {
              setSelectedTileId(value)
              setSelectedRuleTierId(null)
            }}
            onSelectedRuleTierChange={setSelectedRuleTierId}
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
              teamGroups={teamGroups}
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

          <SectionHeading title="Board Setup" description="Manual tile, tier, and rule configuration" />
          {!tokenReady ? null : boardSetupQuery.isLoading ? (
            <StateCard title="Loading board setup" detail="Reading board, tiles, tiers, and rules." />
          ) : boardSetupQuery.isError ? (
            <StateCard title="Board setup unavailable" detail={errorText(boardSetupQuery.error)} />
          ) : (
            <BoardSetupSummaryCard
              boardName={boardSetupQuery.data?.board?.name ?? null}
              tiles={boardSetupQuery.data?.tiles ?? []}
              isDeleting={deleteTileMutation.isPending || deleteTierMutation.isPending || deleteRuleMutation.isPending}
              onDeleteTile={(tile) => deleteTileMutation.mutate(tile)}
              onDeleteTier={(tile, tierId) => deleteTierMutation.mutate({ tile, tierId })}
              onDeleteRule={(tile, ruleId) => deleteRuleMutation.mutate({ tile, ruleId })}
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

function ActionMessageCard({
  message,
  onDismiss,
}: {
  message: ActionMessage
  onDismiss: () => void
}) {
  const isSuccess = message.type === "success"

  return (
    <Card className={cn(isSuccess ? "border-primary/40 bg-primary/5" : "border-destructive/50 bg-destructive/5")}>
      <CardContent className="flex flex-col gap-3 py-4 sm:flex-row sm:items-center sm:justify-between">
        <div className="flex items-start gap-2 text-sm">
          {isSuccess ? (
            <CheckCircleIcon className="mt-0.5 size-4 text-primary" aria-hidden="true" />
          ) : (
            <AlertTriangleIcon className="mt-0.5 size-4 text-destructive" aria-hidden="true" />
          )}
          <p className={cn(isSuccess ? "text-foreground" : "text-destructive")}>{message.text}</p>
        </div>
        <Button size="sm" variant="ghost" onClick={onDismiss}>
          Dismiss
        </Button>
      </CardContent>
    </Card>
  )
}

function readNavigationActionMessage(state: unknown): ActionMessage | null {
  if (!state || typeof state !== "object" || !("actionMessage" in state)) {
    return null
  }

  const message = (state as { actionMessage?: Partial<ActionMessage> }).actionMessage
  if (
    message?.type !== "success" &&
    message?.type !== "error"
  ) {
    return null
  }

  return typeof message.text === "string" && message.text.length > 0
    ? { type: message.type, text: message.text }
    : null
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
  teamGroups,
  teams,
  selectedTeams,
  unassignedCount,
  assigningParticipantId,
  isAssigning,
  onSelectTeam,
  onAssign,
}: {
  participants: AdminEventParticipant[]
  teamGroups: AdminEventTeamRoster[]
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

  const groups =
    teamGroups.length > 0
      ? teamGroups
      : [
          {
            teamId: null,
            teamName: "Unassigned",
            isUnassigned: true,
            participantCount: participants.length,
            participants,
          },
        ]

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
      <CardContent className="flex flex-col gap-4">
        {groups.map((group) => (
          <div
            key={group.teamId ?? "unassigned"}
            className={cn(
              "rounded-lg border bg-background/40",
              group.isUnassigned ? "border-accent/60 bg-accent/10" : "",
            )}
          >
            <div className="flex flex-wrap items-center justify-between gap-2 border-b px-3 py-2">
              <div className="flex min-w-0 items-center gap-2">
                <UsersIcon className="size-4 text-muted-foreground" aria-hidden="true" />
                <h3 className="truncate text-sm font-medium">{group.teamName}</h3>
                {group.isUnassigned ? <Badge variant="secondary">assign these</Badge> : null}
              </div>
              <Badge variant="outline">{group.participantCount} player(s)</Badge>
            </div>
            <div className="flex flex-col gap-2 p-3">
              {group.participants.length === 0 ? (
                <p className="text-sm text-muted-foreground">No players assigned yet.</p>
              ) : (
                group.participants.map((participant) => (
                  <div
                    key={participant.id}
                    className="grid gap-3 rounded-md p-2 md:grid-cols-[1fr_0.8fr_auto] md:items-center"
                  >
                    <div className="min-w-0">
                      <div className="flex min-w-0 items-center gap-2">
                        <p className="truncate text-sm font-medium">{participant.displayName}</p>
                        {participant.isUnassigned ? <Badge variant="secondary">needs team</Badge> : null}
                      </div>
                      <p className="text-xs text-muted-foreground">
                        {participant.runeScapeName} · {participant.teamName ?? "No team"} · {participant.status}
                      </p>
                    </div>
                    <select
                      className={compactSelectControlClassName}
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
                ))
              )}
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
  useAdvancedRuleConfig,
  itemActivityType,
  itemIds,
  duplicatesCount,
  pointActivityType,
  pointRows,
  externalCompetitionId,
  externalMetricType,
  externalMetricKey,
  selectedTileId,
  selectedRuleTierId,
  tiles,
  linkedCompetitions,
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
  onUseAdvancedRuleConfigChange,
  onItemActivityTypeChange,
  onItemIdsChange,
  onDuplicatesCountChange,
  onPointActivityTypeChange,
  onPointRowsChange,
  onExternalCompetitionIdChange,
  onExternalMetricTypeChange,
  onExternalMetricKeyChange,
  onSelectedTileChange,
  onSelectedRuleTierChange,
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
  useAdvancedRuleConfig: boolean
  itemActivityType: string
  itemIds: string
  duplicatesCount: boolean
  pointActivityType: string
  pointRows: string
  externalCompetitionId: string
  externalMetricType: string
  externalMetricKey: string
  selectedTileId: number | null
  selectedRuleTierId: number | null
  tiles: AdminBingoTile[]
  linkedCompetitions: AdminExternalCompetition[]
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
  onUseAdvancedRuleConfigChange: (value: boolean) => void
  onItemActivityTypeChange: (value: string) => void
  onItemIdsChange: (value: string) => void
  onDuplicatesCountChange: (value: boolean) => void
  onPointActivityTypeChange: (value: string) => void
  onPointRowsChange: (value: string) => void
  onExternalCompetitionIdChange: (value: string) => void
  onExternalMetricTypeChange: (value: string) => void
  onExternalMetricKeyChange: (value: string) => void
  onSelectedTileChange: (value: number | null) => void
  onSelectedRuleTierChange: (value: number | null) => void
  onCreateBoard: () => void
  onCreateTile: () => void
  onCreateTier: () => void
  onCreateRule: () => void
}) {
  const selectedTile = tiles.find((tile) => tile.id === selectedTileId) ?? tiles[0] ?? null
  const selectedTier =
    selectedTile?.tiers.find((tier) => tier.id === selectedRuleTierId) ?? selectedTile?.tiers[0] ?? null
  const usedTierNumbers = new Set(selectedTile?.tiers.map((tier) => tier.tierNumber) ?? [])
  const tierNumberValue = numberOrDefault(tierNumber, 1)
  const tierNumberAlreadyExists = Boolean(selectedTile && usedTierNumbers.has(tierNumberValue))
  const selectedTierHasActiveRule = Boolean(
    selectedTier &&
      selectedTile?.rules.some((rule) => rule.tileTierId === selectedTier.id && rule.isActive),
  )
  const firstError = errors.find(Boolean)
  const canCreateTier = Boolean(selectedTile && !tierNumberAlreadyExists)
  const canCreateRule = Boolean(selectedTile && selectedTier && !selectedTierHasActiveRule)

  return (
    <Card>
      <CardHeader>
        <CardTitle>Board Builder</CardTitle>
        <CardDescription>Create one active rule per tier. Advanced JSON is optional.</CardDescription>
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
            className={selectControlClassName}
            value={selectedTile?.id ?? ""}
            onChange={(event) => onSelectedTileChange(event.target.value ? Number(event.target.value) : null)}
          >
            <option value="">Choose tile</option>
            {tiles.map((tile) => (
              <option key={tile.id} value={tile.id}>
                {tile.title} ({tile.tiers.length} tier(s), {tile.rules.length} rule(s))
              </option>
            ))}
          </select>
        </label>

        <div className="grid gap-3 sm:grid-cols-3">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Tier number
            <select
              className={selectControlClassName}
              value={tierNumber}
              onChange={(event) => onTierNumberChange(event.target.value)}
            >
              {tierNumberOptions.map((number) => (
                <option key={number} value={number} disabled={usedTierNumbers.has(number)}>
                  {usedTierNumbers.has(number) ? `Tier ${number} already exists` : `Tier ${number}`}
                </option>
              ))}
            </select>
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Completion target
            <Input value={tierTarget} inputMode="numeric" onChange={(event) => onTierTargetChange(event.target.value)} />
            <span className="text-xs text-muted-foreground">
              The amount needed to complete this tier: item count, points, or Temple XP/KC gain.
            </span>
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
        {tierNumberAlreadyExists ? (
          <p className="rounded-lg border border-accent/50 bg-accent/10 p-3 text-sm text-muted-foreground">
            {`Tier ${tierNumberValue} already exists on this tile. Choose the next available tier number or delete the existing tier first.`}
          </p>
        ) : null}
        <Button disabled={disabled || isSaving || !canCreateTier} onClick={onCreateTier}>
          <PlusIcon data-icon="inline-start" aria-hidden="true" />
          Add Tier
        </Button>

        <Separator />

        <div className="grid gap-3 sm:grid-cols-2">
          <label className="flex flex-col gap-2 text-sm font-medium">
            Rule type
            <select
              className={selectControlClassName}
              value={ruleType}
              onChange={(event) => onRuleTypeChange(event.target.value)}
            >
              <option value="item_count">{ruleTypeLabel("item_count")}</option>
              <option value="point_threshold">{ruleTypeLabel("point_threshold")}</option>
              <option value="external_competition_metric">{ruleTypeLabel("external_competition_metric")}</option>
              <option value="manual">{ruleTypeLabel("manual")}</option>
            </select>
          </label>
          <label className="flex flex-col gap-2 text-sm font-medium">
            Scope
            <select
              className={selectControlClassName}
              value={ruleScope}
              onChange={(event) => onRuleScopeChange(event.target.value)}
            >
              <option value="team">{ruleScopeLabel("team")}</option>
              <option value="player">{ruleScopeLabel("player")}</option>
              <option value="event">{ruleScopeLabel("event")}</option>
            </select>
            <span className="text-xs text-muted-foreground">{ruleScopeHelp(ruleScope)}</span>
          </label>
        </div>

        <label className="flex flex-col gap-2 text-sm font-medium">
          Rule tier
          <select
            className={selectControlClassName}
            value={selectedTier?.id ?? ""}
            disabled={!selectedTile || selectedTile.tiers.length === 0}
            onChange={(event) => onSelectedRuleTierChange(event.target.value ? Number(event.target.value) : null)}
          >
            <option value="">Choose tier</option>
            {selectedTile?.tiers.map((tier) => (
              <option key={tier.id} value={tier.id}>
                {tier.title ?? `Tier ${tier.tierNumber}`}
              </option>
            ))}
          </select>
          <span className="text-xs text-muted-foreground">
            The new rule is attached to this tier. Each tier can have one active rule in this setup UI.
          </span>
        </label>

        <div className="flex items-center justify-between gap-3 rounded-lg border bg-background/40 p-3">
          <div>
            <p className="text-sm font-medium">Rule config</p>
            <p className="text-xs text-muted-foreground">
              {selectedTier
                ? `Applies to ${selectedTier.title ?? `Tier ${selectedTier.tierNumber}`}.`
                : "Create a tier before adding a rule."}
            </p>
          </div>
          <label className="flex items-center gap-2 text-sm">
            <input
              type="checkbox"
              checked={useAdvancedRuleConfig}
              onChange={(event) => onUseAdvancedRuleConfigChange(event.target.checked)}
            />
            Advanced JSON
          </label>
        </div>

        {useAdvancedRuleConfig ? (
          <label className="flex flex-col gap-2 text-sm font-medium">
            Rule config JSON
            <Textarea
              value={ruleConfigJson}
              className="min-h-36 font-mono text-xs"
              onChange={(event) => onRuleConfigJsonChange(event.target.value)}
            />
          </label>
        ) : (
          <StructuredRuleFields
            ruleType={ruleType}
            itemActivityType={itemActivityType}
            itemIds={itemIds}
            duplicatesCount={duplicatesCount}
            pointActivityType={pointActivityType}
            pointRows={pointRows}
            externalCompetitionId={externalCompetitionId}
            externalMetricType={externalMetricType}
            externalMetricKey={externalMetricKey}
            linkedCompetitions={linkedCompetitions}
            onItemActivityTypeChange={onItemActivityTypeChange}
            onItemIdsChange={onItemIdsChange}
            onDuplicatesCountChange={onDuplicatesCountChange}
            onPointActivityTypeChange={onPointActivityTypeChange}
            onPointRowsChange={onPointRowsChange}
            onExternalCompetitionIdChange={onExternalCompetitionIdChange}
            onExternalMetricTypeChange={onExternalMetricTypeChange}
            onExternalMetricKeyChange={onExternalMetricKeyChange}
          />
        )}

        {selectedTierHasActiveRule ? (
          <p className="rounded-lg border border-accent/50 bg-accent/10 p-3 text-sm text-muted-foreground">
            This tier already has an active rule. Delete the existing rule before adding another one.
          </p>
        ) : null}
        <Button disabled={disabled || isSaving || !canCreateRule} onClick={onCreateRule}>
          <SaveIcon data-icon="inline-start" aria-hidden="true" />
          Create Rule
        </Button>
        {firstError ? <p className="text-sm text-destructive">{errorText(firstError)}</p> : null}
      </CardContent>
    </Card>
  )
}

function StructuredRuleFields({
  ruleType,
  itemActivityType,
  itemIds,
  duplicatesCount,
  pointActivityType,
  pointRows,
  externalCompetitionId,
  externalMetricType,
  externalMetricKey,
  linkedCompetitions,
  onItemActivityTypeChange,
  onItemIdsChange,
  onDuplicatesCountChange,
  onPointActivityTypeChange,
  onPointRowsChange,
  onExternalCompetitionIdChange,
  onExternalMetricTypeChange,
  onExternalMetricKeyChange,
}: {
  ruleType: string
  itemActivityType: string
  itemIds: string
  duplicatesCount: boolean
  pointActivityType: string
  pointRows: string
  externalCompetitionId: string
  externalMetricType: string
  externalMetricKey: string
  linkedCompetitions: AdminExternalCompetition[]
  onItemActivityTypeChange: (value: string) => void
  onItemIdsChange: (value: string) => void
  onDuplicatesCountChange: (value: boolean) => void
  onPointActivityTypeChange: (value: string) => void
  onPointRowsChange: (value: string) => void
  onExternalCompetitionIdChange: (value: string) => void
  onExternalMetricTypeChange: (value: string) => void
  onExternalMetricKeyChange: (value: string) => void
}) {
  if (ruleType === "point_threshold") {
    return (
      <div className="grid gap-3 rounded-lg border bg-background/40 p-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Activity type
          <ActivityTypeSelect value={pointActivityType} onChange={onPointActivityTypeChange} />
        </label>
        <PointRowsEditor value={pointRows} onChange={onPointRowsChange} />
      </div>
    )
  }

  if (ruleType === "external_competition_metric") {
    return (
      <div className="grid gap-3 rounded-lg border bg-background/40 p-3 sm:grid-cols-3">
        <label className="flex flex-col gap-2 text-sm font-medium">
          Temple link
          <select
            className={selectControlClassName}
            value={externalCompetitionId}
            onChange={(event) => onExternalCompetitionIdChange(event.target.value)}
          >
            <option value="">Select link</option>
            {linkedCompetitions.map((competition) => (
              <option key={competition.id} value={competition.id}>
                {competition.name} ({competition.externalId})
              </option>
            ))}
          </select>
        </label>
        <label className="flex flex-col gap-2 text-sm font-medium">
          Metric type
          <Input value={externalMetricType} onChange={(event) => onExternalMetricTypeChange(event.target.value)} />
        </label>
        <label className="flex flex-col gap-2 text-sm font-medium">
          Metric key
          <Input value={externalMetricKey} onChange={(event) => onExternalMetricKeyChange(event.target.value)} />
        </label>
      </div>
    )
  }

  if (ruleType === "manual") {
    return (
      <div className="rounded-lg border bg-background/40 p-3 text-sm text-muted-foreground">
        Manual rules only need a tier target for this MVP setup.
      </div>
    )
  }

  return (
    <div className="grid gap-3 rounded-lg border bg-background/40 p-3">
      <label className="flex flex-col gap-2 text-sm font-medium">
        Activity type
        <ActivityTypeSelect value={itemActivityType} onChange={onItemActivityTypeChange} />
      </label>
      <label className="flex flex-col gap-2 text-sm font-medium">
        Item IDs
        <Input
          value={itemIds}
          placeholder="22486, 20784"
          onChange={(event) => onItemIdsChange(event.target.value)}
        />
        <span className="text-xs text-muted-foreground">Comma, space, or newline separated OSRS item IDs.</span>
      </label>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={duplicatesCount}
          onChange={(event) => onDuplicatesCountChange(event.target.checked)}
        />
        Count duplicate item drops
      </label>
    </div>
  )
}

function ActivityTypeSelect({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  return (
    <select
      className={selectControlClassName}
      value={value}
      onChange={(event) => onChange(event.target.value)}
    >
      {activityTypeOptions.map((option) => (
        <option key={option.value} value={option.value}>
          {option.label}
        </option>
      ))}
    </select>
  )
}

function PointRowsEditor({
  value,
  onChange,
}: {
  value: string
  onChange: (value: string) => void
}) {
  const rows = parseEditablePointRows(value)

  function updateRow(index: number, field: "itemId" | "points", nextValue: string) {
    const nextRows = rows.map((row, rowIndex) =>
      rowIndex === index ? { ...row, [field]: nextValue } : row,
    )
    onChange(serializeEditablePointRows(nextRows))
  }

  function addRow() {
    onChange(serializeEditablePointRows([...rows, { itemId: "", points: "" }]))
  }

  function removeRow(index: number) {
    const nextRows = rows.filter((_, rowIndex) => rowIndex !== index)
    onChange(serializeEditablePointRows(nextRows.length > 0 ? nextRows : [{ itemId: "", points: "" }]))
  }

  return (
    <div className="flex flex-col gap-2 text-sm font-medium">
      Item points
      <div className="grid gap-2">
        {rows.map((row, index) => (
          <div key={index} className="grid grid-cols-[1fr_0.8fr_auto] gap-2">
            <Input
              value={row.itemId}
              inputMode="numeric"
              placeholder="Item ID"
              aria-label={`Item ID ${index + 1}`}
              onChange={(event) => updateRow(index, "itemId", event.target.value)}
            />
            <Input
              value={row.points}
              inputMode="decimal"
              placeholder="Points"
              aria-label={`Points ${index + 1}`}
              onChange={(event) => updateRow(index, "points", event.target.value)}
            />
            <Button
              type="button"
              size="icon"
              variant="ghost"
              aria-label={`Remove item point row ${index + 1}`}
              disabled={rows.length === 1}
              onClick={() => removeRow(index)}
            >
              <Trash2Icon className="size-4" aria-hidden="true" />
            </Button>
          </div>
        ))}
      </div>
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="text-xs text-muted-foreground">
          Each row maps an OSRS item ID to the points it should award.
        </span>
        <Button type="button" size="sm" variant="outline" onClick={addRow}>
          <PlusIcon data-icon="inline-start" aria-hidden="true" />
          Add item
        </Button>
      </div>
    </div>
  )
}

function BoardSetupSummaryCard({
  boardName,
  tiles,
  isDeleting,
  onDeleteTile,
  onDeleteTier,
  onDeleteRule,
}: {
  boardName: string | null
  tiles: AdminBingoTile[]
  isDeleting: boolean
  onDeleteTile: (tile: AdminBingoTile) => void
  onDeleteTier: (tile: AdminBingoTile, tierId: number) => void
  onDeleteRule: (tile: AdminBingoTile, ruleId: number) => void
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
                  <div className="flex min-w-0 items-center gap-2">
                    <p className="truncate text-sm font-medium">{tile.title}</p>
                    <Button
                      size="icon"
                      variant="ghost"
                      aria-label={`Delete ${tile.title}`}
                      disabled={isDeleting}
                      onClick={() => {
                        if (window.confirm(`Delete tile "${tile.title}" and its tiers/rules?`)) {
                          onDeleteTile(tile)
                        }
                      }}
                    >
                      <Trash2Icon className="size-4" aria-hidden="true" />
                    </Button>
                  </div>
                  <p className="text-xs text-muted-foreground">
                    {tile.tiers.length} tier(s) · {tile.rules.length} rule(s)
                  </p>
                  <div className="mt-2 flex flex-wrap gap-2">
                    {tile.tiers.map((tier) => (
                      <span key={tier.id} className="inline-flex items-center gap-1 rounded-md border px-2 py-1 text-xs">
                        {tier.title ?? `Tier ${tier.tierNumber}`}
                        <button
                          type="button"
                          className="rounded-sm p-0.5 text-muted-foreground hover:text-destructive"
                          disabled={isDeleting}
                          aria-label={`Delete ${tier.title ?? `Tier ${tier.tierNumber}`}`}
                          onClick={() => {
                            if (window.confirm(`Delete ${tier.title ?? `Tier ${tier.tierNumber}`}?`)) {
                              onDeleteTier(tile, tier.id)
                            }
                          }}
                        >
                          <Trash2Icon className="size-3" aria-hidden="true" />
                        </button>
                      </span>
                    ))}
                  </div>
                  {tile.rules.length > 0 ? (
                    <p className="mt-2 text-xs text-muted-foreground">
                      Progress appears on public/team pages after matching activity is processed. Check event window,
                      team assignment, and item IDs if it stays at zero.
                    </p>
                  ) : null}
                </div>
                <div className="flex flex-col gap-2 text-xs text-muted-foreground">
                  {tile.rules.length === 0 ? (
                    <span>No rules</span>
                  ) : (
                    tile.rules.map((rule: AdminTileRule) => (
                      <div key={rule.id} className="flex items-center justify-between gap-2 rounded-md border bg-background/50 px-2 py-1.5">
                        <span className="min-w-0 truncate">
                          {ruleTypeLabel(rule.ruleType)} · {ruleScopeLabel(rule.scope)}
                        </span>
                        <Button
                          size="icon"
                          variant="ghost"
                          aria-label={`Delete ${rule.ruleType} rule`}
                          disabled={isDeleting}
                          onClick={() => {
                            if (window.confirm(`Delete ${rule.ruleType} rule?`)) {
                              onDeleteRule(tile, rule.id)
                            }
                          }}
                        >
                          <Trash2Icon className="size-3.5" aria-hidden="true" />
                        </Button>
                      </div>
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
            className={selectControlClassName}
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

function selectedTileTierId(
  tiles: AdminBingoTile[],
  selectedTileId: number | null,
  selectedRuleTierId: number | null,
) {
  const tile = tiles.find((candidate) => candidate.id === selectedTileId) ?? tiles[0]
  return tile?.tiers.find((tier) => tier.id === selectedRuleTierId)?.id ?? tile?.tiers[0]?.id ?? null
}

function numberOrDefault(value: string, fallback: number) {
  const parsed = Number(value)
  return Number.isFinite(parsed) && parsed > 0 ? parsed : fallback
}

type BuildRuleConfigInput = {
  ruleType: string
  useAdvancedRuleConfig: boolean
  ruleConfigJson: string
  tierTarget: string
  itemActivityType: string
  itemIds: string
  duplicatesCount: boolean
  pointActivityType: string
  pointRows: string
  externalCompetitionId: string
  externalMetricType: string
  externalMetricKey: string
}

function buildRuleConfigJson(input: BuildRuleConfigInput) {
  if (input.useAdvancedRuleConfig) {
    try {
      const parsed = JSON.parse(input.ruleConfigJson) as unknown
      if (!parsed || Array.isArray(parsed) || typeof parsed !== "object") {
        throw new Error("Rule config must be a JSON object.")
      }

      return JSON.stringify(parsed, null, 2)
    } catch (error) {
      throw new Error(errorText(error), { cause: error })
    }
  }

  const requiredValue = positiveNumber(input.tierTarget, "Tier target")

  if (input.ruleType === "point_threshold") {
    return JSON.stringify(
      {
        activityType: requiredString(input.pointActivityType, "Activity type"),
        pointsTable: parsePointRows(input.pointRows),
        requiredValue,
      },
      null,
      2,
    )
  }

  if (input.ruleType === "external_competition_metric") {
    return JSON.stringify(
      {
        provider: "templeosrs",
        externalCompetitionId: positiveInteger(input.externalCompetitionId, "Temple link"),
        metricType: requiredString(input.externalMetricType, "Metric type"),
        metricKey: requiredString(input.externalMetricKey, "Metric key"),
        requiredValue,
      },
      null,
      2,
    )
  }

  if (input.ruleType === "manual") {
    return JSON.stringify({ requiredValue }, null, 2)
  }

  return JSON.stringify(
    {
      activityType: requiredString(input.itemActivityType, "Activity type"),
      itemIds: parseItemIds(input.itemIds),
      requiredValue,
      duplicatesCount: input.duplicatesCount,
    },
    null,
    2,
  )
}

function positiveNumber(value: string, label: string) {
  const parsed = Number(value)
  if (!Number.isFinite(parsed) || parsed <= 0) {
    throw new Error(`${label} must be greater than zero.`)
  }

  return parsed
}

function positiveInteger(value: string, label: string) {
  const parsed = Number(value)
  if (!Number.isInteger(parsed) || parsed <= 0) {
    throw new Error(`${label} is required.`)
  }

  return parsed
}

function requiredString(value: string, label: string) {
  const trimmed = value.trim()
  if (!trimmed) {
    throw new Error(`${label} is required.`)
  }

  return trimmed
}

function parseItemIds(value: string) {
  const ids = value
    .split(/[\s,;]+/)
    .map((part) => part.trim())
    .filter(Boolean)
    .map((part) => positiveInteger(part, "Item ID"))

  if (ids.length === 0) {
    throw new Error("At least one item ID is required.")
  }

  return ids
}

function parsePointRows(value: string) {
  const rows = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [itemId, points] = line.split(/[\s,;:]+/)
      return {
        itemId: positiveInteger(itemId ?? "", "Item ID"),
        points: positiveNumber(points ?? "", "Points"),
      }
    })

  if (rows.length === 0) {
    throw new Error("At least one item point row is required.")
  }

  return rows
}

function parseEditablePointRows(value: string) {
  const rows = value
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [itemId, points] = line.split(/[\s,;:]+/)
      return {
        itemId: itemId ?? "",
        points: points ?? "",
      }
    })

  return rows.length > 0 ? rows : [{ itemId: "", points: "" }]
}

function serializeEditablePointRows(rows: { itemId: string; points: string }[]) {
  return rows.map((row) => `${row.itemId.trim()},${row.points.trim()}`).join("\n")
}

function ruleTypeLabel(ruleType: string) {
  const labels: Record<string, string> = {
    item_count: "Item count",
    point_threshold: "Point threshold",
    external_competition_metric: "Temple metric",
    manual: "Manual",
  }

  return labels[ruleType] ?? ruleType
}

function ruleScopeLabel(scope: string) {
  const labels: Record<string, string> = {
    team: "Team scoring",
    player: "Player scoring",
    event: "Whole event",
  }

  return labels[scope] ?? scope
}

function ruleScopeHelp(scope: string) {
  const help: Record<string, string> = {
    team: "Use this for normal team bingo tiles. Progress is tracked separately for each team.",
    player: "Use this only for individual-player scoring. Team assignment is ignored.",
    event: "Use this for one event-wide total shared by everyone.",
  }

  return help[scope] ?? "Choose how the rule should group progress."
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
