import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useAuth0 } from "@auth0/auth0-react";
import { Bird, Star, Github } from "lucide-react";
import { isAuth0Configured } from "../config";

const GITHUB_REPO = "GordonBeeming/ClaudeNest";
const GITHUB_URL = `https://github.com/${GITHUB_REPO}`;
const CACHE_KEY = "claudenest_gh_stars";

function GitHubStarButton() {
  const [stars, setStars] = useState<number | null>(null);

  useEffect(() => {
    const cached = sessionStorage.getItem(CACHE_KEY);
    if (cached) {
      setStars(parseInt(cached, 10));
      return;
    }

    fetch(`https://api.github.com/repos/${GITHUB_REPO}`)
      .then((res) => res.json())
      .then((data) => {
        if (typeof data.stargazers_count === "number") {
          setStars(data.stargazers_count);
          sessionStorage.setItem(CACHE_KEY, String(data.stargazers_count));
        }
      })
      .catch(() => {
        // Silently fail — button still links to GitHub
      });
  }, []);

  return (
    <a
      href={GITHUB_URL}
      target="_blank"
      rel="noopener noreferrer"
      className="inline-flex items-center gap-1.5 rounded-lg border border-gray-300 px-3 py-1.5 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
      aria-label={`Star ClaudeNest on GitHub${stars !== null ? ` — ${stars} stars` : ""}`}
    >
      <Github className="h-4 w-4" aria-hidden="true" />
      <Star className="h-3.5 w-3.5" aria-hidden="true" />
      {stars !== null ? (
        <span>{stars}</span>
      ) : (
        <span className="inline-block h-4 w-6 animate-pulse rounded bg-gray-200 dark:bg-gray-700" />
      )}
    </a>
  );
}

function AuthButtons() {
  const { loginWithRedirect, isAuthenticated } = useAuth0();

  if (isAuthenticated) {
    return (
      <Link
        to="/dashboard"
        className="rounded-lg bg-nest-500 px-4 py-2 text-sm font-semibold text-white hover:bg-nest-600"
      >
        Dashboard
      </Link>
    );
  }

  return (
    <>
      <button
        onClick={() => loginWithRedirect()}
        className="rounded-lg px-4 py-2 text-sm font-medium text-gray-700 hover:bg-gray-100 dark:text-gray-300 dark:hover:bg-gray-800"
      >
        Sign In
      </button>
      <a
        href="#pricing"
        className="rounded-lg bg-nest-500 px-4 py-2 text-sm font-semibold text-white hover:bg-nest-600"
      >
        Get Started
      </a>
    </>
  );
}

function DevModeButtons() {
  return (
    <Link
      to="/dashboard"
      className="rounded-lg bg-nest-500 px-4 py-2 text-sm font-semibold text-white hover:bg-nest-600"
    >
      Dashboard
    </Link>
  );
}

export function MarketingNav() {
  return (
    <nav
      className="sticky top-0 z-50 border-b border-gray-200 bg-white/80 backdrop-blur-sm dark:border-gray-800 dark:bg-gray-950/80"
      aria-label="Marketing navigation"
    >
      <div className="mx-auto flex h-14 max-w-6xl items-center justify-between px-4">
        <Link
          to="/"
          className="flex items-center gap-2 text-lg font-semibold text-gray-900 dark:text-white"
          aria-label="ClaudeNest home"
        >
          <Bird className="h-6 w-6 text-nest-500" aria-hidden="true" />
          <span>ClaudeNest</span>
        </Link>

        <div className="flex items-center gap-3">
          <GitHubStarButton />
          {isAuth0Configured ? <AuthButtons /> : <DevModeButtons />}
        </div>
      </div>
    </nav>
  );
}
