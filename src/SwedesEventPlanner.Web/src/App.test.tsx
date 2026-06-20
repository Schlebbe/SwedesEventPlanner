import { QueryClient, QueryClientProvider } from "@tanstack/react-query"
import { render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router-dom"
import { describe, expect, it } from "vitest"
import App from "./App"

describe("App", () => {
  it("renders the event scoreboard shell", () => {
    const queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
        },
      },
    })

    render(
      <QueryClientProvider client={queryClient}>
        <MemoryRouter>
          <App />
        </MemoryRouter>
      </QueryClientProvider>,
    )

    expect(
      screen.getByRole("heading", { name: "Swedes Event Planner" }),
    ).toBeInTheDocument()
    expect(screen.getByText("Board Progress")).toBeInTheDocument()
  })
})
