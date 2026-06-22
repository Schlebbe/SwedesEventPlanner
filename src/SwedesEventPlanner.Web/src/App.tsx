import { Navigate, Route, Routes } from "react-router-dom"
import { AdminEventSetupPage } from "@/pages/AdminEventSetupPage"
import { EventDetailPage } from "@/pages/EventDetailPage"
import { EventIndexPage } from "@/pages/EventIndexPage"

function App() {
  return (
    <Routes>
      <Route path="/" element={<Navigate to="/events" replace />} />
      <Route path="/events" element={<EventIndexPage />} />
      <Route path="/events/:eventSlug" element={<EventDetailPage />} />
      <Route path="/admin/events/:eventSlug/setup" element={<AdminEventSetupPage />} />
    </Routes>
  )
}

export default App
