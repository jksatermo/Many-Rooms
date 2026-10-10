import { Arrow } from './Icons';
import { useCallback } from 'react';
import { Link, Navigate, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { api } from '../api';
import { demoSession } from '../session';
import { useResource } from '../useResource';
import { Brand } from './Brand';
import { ErrorState, Loading } from './Ui';

export function AppShell() {
  const navigate = useNavigate();
  const persona = demoSession.persona();
  const context = useResource(
    useCallback(
      (signal: AbortSignal) => api.context(signal, persona),
      [persona],
    ),
    { refreshOnFocus: false },
  );
  if (!demoSession.entered()) return <Navigate to="/demo" replace />;
  return (
    <div className="app-layout">
      <header className="app-header">
        <div className="container app-header-inner">
          <Brand to="/app" />
          <nav aria-label="Community navigation">
            <NavLink end to="/app">
              Overview
            </NavLink>
            <NavLink to="/app/events">Events</NavLink>
            <NavLink to="/app/availability">My availability</NavLink>
          </nav>
          <button
            className="quiet-link exit-demo"
            onClick={() => {
              demoSession.leave();
              navigate('/');
            }}
          >
            Leave demo{' '}
            <span aria-hidden="true">
              <Arrow />
            </span>
          </button>
        </div>
      </header>
      <div className="demo-banner">
        <div className="container">
          <span className="demo-badge">
            {persona === 'leader' ? 'Demo leader view' : 'Development demo'}
          </span>
          <span>Fictional people. Real saved responses.</span>
          <Link to="/demo">About this demo</Link>
        </div>
      </div>
      <main id="main-content" className="container app-main">
        {context.loading ? (
          <Loading />
        ) : context.error ? (
          <ErrorState error={context.error} onRetry={context.reload} />
        ) : context.data ? (
          <Outlet context={context.data} />
        ) : null}
      </main>
      <footer className="app-footer container">
        <p>
          Many Rooms <span aria-hidden="true">/</span> Room for everyone.
        </p>
        <span>
          Schedules use the community’s time zone. Availability forms show your
          device’s zone.
        </span>
      </footer>
    </div>
  );
}
