import { Link } from 'react-router-dom';

export function Brand({ to = '/' }: { to?: string }) {
  return (
    <Link className="brand" to={to} aria-label="Many Rooms home">
      <svg aria-hidden="true" viewBox="0 0 42 42">
        <path
          d="M5 34V20a8 8 0 0 1 16 0v14M21 34V12a8 8 0 0 1 16 0v22"
          fill="none"
          stroke="currentColor"
          strokeWidth="3.5"
          strokeLinecap="round"
        />
      </svg>
      <span>
        many rooms<span className="brand-dot">.</span>
      </span>
    </Link>
  );
}
