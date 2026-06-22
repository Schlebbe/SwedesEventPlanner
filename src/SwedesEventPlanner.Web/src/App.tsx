import { Navigate, Route, Routes } from "react-router-dom"
import { AdminEventSetupPage } from "@/pages/AdminEventSetupPage"
import { AdminHomePage } from "@/pages/AdminHomePage"
import { EventDetailPage } from "@/pages/EventDetailPage"
import { EventIndexPage } from "@/pages/EventIndexPage"
import { EventTeamPage } from "@/pages/EventTeamPage"

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/events" replace />} />
      <Route path="/events" element={<EventIndexPage />} />
      <Route path="/events/:eventSlug" element={<EventDetailPage />} />
      <Route path="/events/:eventSlug/teams/:teamId" element={<EventTeamPage />} />
      <Route path="/admin" element={<AdminHomePage />} />
      <Route path="/admin/events/:eventSlug/setup" element={<AdminEventSetupPage />} />
    </Routes>
  )
}

export default App
