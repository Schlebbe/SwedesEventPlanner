import type { EventSummary } from "@/api/events"

export function formatEventWindow(event: EventSummary) {
  const start = formatDate(event.startsAt, event.timeZone)
  const end = event.endsAt ? formatDate(event.endsAt, event.timeZone) : "open"
  return `${start} to ${end}`
}

export function formatDate(value: string, timeZone: string) {
  return new Intl.DateTimeFormat("sv-SE", {
    dateStyle: "medium",
    timeStyle: "short",
    timeZone,
  }).format(new Date(value))
}

export function formatTimestamp(value: string) {
  return new Intl.DateTimeFormat("sv-SE", {
    dateStyle: "medium",
    timeStyle: "short",
  }).format(new Date(value))
}

export function formatNumber(value: number) {
  return new Intl.NumberFormat("sv-SE", {
    maximumFractionDigits: 1,
  }).format(value)
}
