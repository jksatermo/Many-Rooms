import { useEffect } from 'react';
import { Link, Route, Routes, useLocation } from 'react-router-dom';
import { AppShell } from './components/AppShell';
import { Landing } from './pages/Landing';
import { DemoEntry } from './pages/DemoEntry';
import { Dashboard } from './pages/Dashboard';
import { Events } from './pages/Events';
import { EventDetails } from './pages/EventDetails';
import { Availability } from './pages/Availability';
import { LeaderEvent } from './pages/LeaderEvent';

export function App() {
  const { pathname } = useLocation();
  useEffect(() => {
    window.scrollTo({ top: 0, behavior: 'instant' });
    // Route navigation gives keyboard and screen-reader users a consistent starting point.
    const main = document.querySelector<HTMLElement>('main');
    if (main) {
      main.tabIndex = -1;
      main.focus({ preventScroll: true });
    }
  }, [pathname]);
  return (
    <>
      <a className="skip-link" href="#main-content">
        Skip to content
      </a>
      <Routes>
        <Route path="/" element={<Landing />} />
        <Route path="/demo" element={<DemoEntry />} />
        <Route path="/app" element={<AppShell />}>
          <Route index element={<Dashboard />} />
          <Route path="events" element={<Events />} />
          <Route path="events/:eventId" element={<EventDetails />} />
          <Route path="availability" element={<Availability />} />
          <Route path="leader/events/:eventId" element={<LeaderEvent />} />
        </Route>
        <Route
          path="*"
          element={
            <main id="main-content" className="state-box">
              <h1>There’s no room here just yet.</h1>
              <p>That page doesn’t exist. Let’s find your way back.</p>
              <Link className="button" to="/">
                Back home
              </Link>
            </main>
          }
        />
      </Routes>
    </>
  );
}
