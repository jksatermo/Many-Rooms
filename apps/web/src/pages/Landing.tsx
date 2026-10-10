import { Arrow, Spark } from '../components/Icons';
import { Link } from 'react-router-dom';
import { Brand } from '../components/Brand';

export function Landing() {
  return (
    <div className="landing">
      <header className="public-header container">
        <Brand />
        <nav aria-label="Main navigation">
          <a className="quiet-link" href="#a-little-less-admin">
            The idea
          </a>
          <Link className="button button-outline button-small" to="/demo">
            Explore the demo{' '}
            <span aria-hidden="true">
              <Arrow />
            </span>
          </Link>
        </nav>
      </header>
      <main id="main-content">
        <section className="hero container">
          <div className="hero-copy">
            <p className="eyebrow">
              <span className="small-sun" aria-hidden="true">
                <Spark />
              </span>{' '}
              Good things happen together
            </p>
            <h1>
              Room for
              <br />
              <em>everyone.</em>
            </h1>
            <p className="hero-description">
              A little less organizing.
              <br />A little more being there.
            </p>
            <p className="hero-detail">
              A welcoming home for the people, plans, and small acts of service
              that bring your community together.
            </p>
            <Link className="button" to="/demo">
              Step inside the demo{' '}
              <span aria-hidden="true">
                <Arrow />
              </span>
            </Link>
            <p className="demo-caption">
              An early look at Many Rooms · Actively in development
            </p>
          </div>
          <div
            className="hero-art"
            aria-label="Illustration of a community volunteer schedule"
          >
            <div className="arch arch-back" />
            <div className="arch arch-front" />
            <span className="art-spark" aria-hidden="true">
              <Spark />
            </span>
            <div className="preview-card">
              <div className="preview-top">
                <span className="eyebrow">A little look inside</span>
                <span className="preview-dot" />
              </div>
              <p className="preview-community">Cedar Grove Community</p>
              <h2>Sunday, together.</h2>
              <p className="preview-subtitle">
                Community gathering · Main hall
              </p>
              <div className="preview-rule" />
              <p className="preview-label">A place on the team</p>
              <div className="preview-person">
                <span className="avatar avatar-clay">AM</span>
                <div>
                  <strong>Alex Morgan</strong>
                  <span>Welcome team · Greeter</span>
                </div>
                <span className="preview-check" aria-label="Accepted">
                  ✓
                </span>
              </div>
              <div className="preview-person">
                <span className="avatar avatar-sage">JR</span>
                <div>
                  <strong>Jordan Rivera</strong>
                  <span>Hospitality · Coffee host</span>
                </div>
                <span className="preview-check" aria-label="Accepted">
                  ✓
                </span>
              </div>
              <div className="preview-note">
                <span aria-hidden="true">
                  <Spark />
                </span>{' '}
                Small contributions. Shared purpose.
              </div>
            </div>
            <div className="floating-note">
              <span aria-hidden="true">✓</span>
              <div>
                Count me in.<small>One simple response.</small>
              </div>
            </div>
            <span className="art-caption">Fictional demo community</span>
          </div>
        </section>
        <section className="idea-section" id="a-little-less-admin">
          <div className="container">
            <div className="section-intro">
              <p className="eyebrow">For the people who show up</p>
              <h2>
                More connection.
                <br />
                Fewer loose ends.
              </h2>
              <p>
                Communities run on people who care. Their tools should make it
                easier to lend a hand.
              </p>
            </div>
            <div className="feature-grid">
              <article>
                <span className="feature-number">01 / Gather</span>
                <h3>Know what’s coming.</h3>
                <p>
                  See upcoming events and the people helping bring each
                  gathering to life.
                </p>
              </article>
              <article>
                <span className="feature-number">02 / Belong</span>
                <h3>Find your part.</h3>
                <p>
                  Keep teams, volunteer roles, and event details together in one
                  calm, clear place.
                </p>
              </article>
              <article>
                <span className="feature-number">03 / Respond</span>
                <h3>A simple yes or no.</h3>
                <p>
                  Accept or decline your assignment, with a response that stays
                  saved for your next visit.
                </p>
              </article>
            </div>
          </div>
        </section>
        <section className="road-ahead container">
          <div>
            <p className="eyebrow">Starting small. Building thoughtfully.</p>
            <h2>
              A growing home
              <br />
              for your community.
            </h2>
          </div>
          <div>
            <p>
              Our working demo brings events, unavailable time, and clear
              schedule conflicts together. Volunteers can respond to their
              assignments on web and mobile.
            </p>
            <p className="muted">
              Ahead: recurring events, reminders, and SMS replies for people who
              prefer a text. These features are on the roadmap.
            </p>
            <Link className="text-link" to="/demo">
              See what’s here today{' '}
              <span aria-hidden="true">
                <Arrow />
              </span>
            </Link>
          </div>
        </section>
      </main>
      <footer className="public-footer container">
        <Brand />
        <p>Room for everyone.</p>
        <span>Built with care. Growing with purpose.</span>
      </footer>
    </div>
  );
}
