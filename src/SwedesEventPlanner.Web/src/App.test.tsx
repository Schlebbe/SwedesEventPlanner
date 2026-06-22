import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { afterEach, describe, expect, it, vi } from "vitest"
import App from "./App"

const eventSummary = {
  id: 1,
  slug: "manual-bingo-2026",
  name: "Manual Bingo 2026",
  eventType: "bingo",
  status: "active",
  startsAt: "2026-01-01T00:00:00Z",
  endsAt: "2026-12-31T23:59:59Z",
  timeZone: "Europe/Stockholm",
}

afterEach(() => {
  vi.unstubAllGlobals()
})

describe("App", () => {
  it("renders the event index with mocked API data", async () => {
    stubFetch({
      "/api/events": { events: [eventSummary] },
    })

    renderApp("/events")

    expect(
      await screen.findByRole("heading", { name: "Swedes Event Planner" }),
    ).toBeInTheDocument()
    expect(await screen.findByText("Manual Bingo 2026")).toBeInTheDocument()
  })

  it("renders the event page with board progress and contributions", async () => {
    stubFetch({
      "/api/events/manual-bingo-2026": eventSummary,
      "/api/events/manual-bingo-2026/board": {
        event: eventSummary,
        teams: [
          {
            id: 1,
            name: "Blue",
            score: 0,
            scoredTiers: 0,
            completedTiles: 0,
            currentValue: 7,
          },
        ],
        board: {
          id: 1,
          name: "Demo Board",
          rows: null,
          columns: null,
          tiles: [
            {
              id: 1,
              title: "TOB",
              description: "Earn Theatre of Blood points.",
              positionX: null,
              positionY: null,
              sortOrder: 1,
              teamProgress: [
                {
                  teamId: 1,
                  teamName: "Blue",
                  currentValue: 7,
                  currentTier: 0,
                  isCompleted: false,
                  completedAt: null,
                },
              ],
              tiers: [
                {
                  id: 1,
                  tierNumber: 1,
                  title: "TOB Tier 1",
                  description: null,
                  scoreValue: 1,
                  isRequiredForBoardCompletion: true,
                  requiredValue: 10,
                  teamProgress: [
                    {
                      teamId: 1,
                      teamName: "Blue",
                      currentValue: 7,
                      isAchieved: false,
                      achievedAt: null,
                      isScored: false,
                      scoredAt: null,
                      scoreAwarded: 0,
                    },
                  ],
                },
              ],
            },
          ],
        },
      },
      "/api/events/manual-bingo-2026/teams": {
        event: eventSummary,
        teams: [
          {
            id: 1,
            name: "Blue",
            score: 0,
            scoredTiers: 0,
            completedTiles: 0,
            currentValue: 7,
            contributionCount: 1,
          },
        ],
      },
      "/api/events/manual-bingo-2026/contributions": {
        event: eventSummary,
        contributions: [
          {
            id: 1,
            playerName: "Sebbe",
            teamId: 1,
            teamName: "Blue",
            tileTitle: "TOB",
            tierTitle: "TOB Tier 1",
            valueAdded: 7,
            description: "Matched 7 point(s).",
            createdAt: "2026-07-02T18:30:00Z",
          },
        ],
      },
    })

    renderApp("/events/manual-bingo-2026")

    expect(
      await screen.findByRole("heading", { name: "Manual Bingo 2026" }),
    ).toBeInTheDocument()
    expect(await screen.findByText("TOB Tier 1")).toBeInTheDocument()
    expect(await screen.findByText(/Sebbe added/)).toBeInTheDocument()
  })
})

function renderApp(route: string) {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  })

  render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <App />
      </MemoryRouter>
    </QueryClientProvider>,
  )
}

function stubFetch(responses: Record<string, unknown>) {
  vi.stubGlobal(
    "fetch",
    vi.fn((input: RequestInfo | URL) => {
      const url = input.toString()
      const path = new URL(url, "http://localhost").pathname
      const body = responses[path]

      if (!body) {
        return Promise.resolve(new Response(null, { status: 404 }))
      }

      return Promise.resolve(
        new Response(JSON.stringify(body), {
          status: 200,
          headers: { "Content-Type": "application/json" },
        }),
      )
    }),
  )
}
