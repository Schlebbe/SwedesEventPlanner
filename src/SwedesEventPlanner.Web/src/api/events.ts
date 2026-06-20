export type EventSummary = {
  id: number
  slug: string
  name: string
  eventType: string
  status: string
  startsAt: string
  endsAt: string | null
  timeZone: string
}

type EventListResponse = {
  events: EventSummary[]
}

export async function listEvents(signal?: AbortSignal): Promise<EventSummary[]> {
  const response = await fetch("/api/events", { signal })

  if (!response.ok) {
    throw new Error(`Events request failed with ${response.status}`)
  }

  const data = (await response.json()) as EventListResponse
  return data.events
}
