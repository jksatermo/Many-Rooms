import { Arrow, Spark } from '../components/Icons';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { api } from '../api';
import { Brand } from '../components/Brand';
import { demoSession } from '../session';

export function DemoEntry() {
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const navigate = useNavigate();
  async function enter() {
    setBusy(true);
    setError('');
    try {
      await api.context();
      demoSession.enter();
      navigate('/app');
    } catch (cause) {
      setError(
        cause instanceof Error
          ? cause.message
          : 'The demo is unavailable. Please try again.',
      );
    } finally {
      setBusy(false);
    }
  }
  return (
    <div className="entry-page">
      <header className="public-header container">
        <Brand />
        <Link className="quiet-link" to="/">
          Back home{' '}
          <span aria-hidden="true">
            <Arrow />
          </span>
        </Link>
      </header>
      <main id="main-content" className="entry-main">
        <div className="entry-ornament" aria-hidden="true">
          <Spark />
        </div>
        <p className="eyebrow">Come on in</p>
        <h1>Take a look around.</h1>
        <p className="entry-lead">
          Meet a fictional community. Find your place on a team. Try a simple
          response.
        </p>
        <div className="entry-card">
          <span className="pill">Development demo</span>
          <h2>A shared space to explore.</h2>
          <p>
            You’ll enter as a fictional volunteer. No account or password
            needed.
          </p>
          <ul>
            <li>Explore upcoming events and volunteer teams.</li>
            <li>Accept or decline your own assignments.</li>
            <li>Refresh to see your saved response.</li>
          </ul>
          <div className="demo-disclosure">
            <strong>This is a shared development demo.</strong>
            <p>
              Responses are saved to the local demo database and can be changed
              by other people using it. This is not production sign-in. Please
              don’t enter personal information.
            </p>
          </div>
          {error && (
            <p className="inline-error" role="alert">
              {error}
            </p>
          )}
          <button
            className="button button-wide"
            onClick={() => void enter()}
            disabled={busy}
          >
            {busy ? 'Opening the demo…' : 'Enter the demo'}{' '}
            <span aria-hidden="true">
              <Arrow />
            </span>
          </button>
          <p className="entry-help">
            The demo must be enabled on the development API.
          </p>
        </div>
      </main>
    </div>
  );
}
