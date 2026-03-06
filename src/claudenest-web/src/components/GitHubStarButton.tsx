import { useState, useEffect } from "react";
import { Github, Star } from "lucide-react";

const GITHUB_REPO = "GordonBeeming/ClaudeNest";
const GITHUB_URL = `https://github.com/${GITHUB_REPO}`;
const CACHE_KEY = "claudenest_gh_stars";
const CACHE_TTL = 60 * 60 * 1000; // 1 hour

function getCachedStars(): number | null {
  try {
    const cached = localStorage.getItem(CACHE_KEY);
    if (cached) {
      const { count, ts } = JSON.parse(cached);
      if (Date.now() - ts < CACHE_TTL) {
        return count;
      }
    }
  } catch {
    // ignore malformed cache
  }
  return null;
}

export function GitHubStarButton() {
  const [stars, setStars] = useState<number | null>(getCachedStars);

  useEffect(() => {
    if (getCachedStars() !== null) return;

    fetch(`https://api.github.com/repos/${GITHUB_REPO}`)
      .then((res) => res.json())
      .then((data) => {
        if (typeof data.stargazers_count === "number") {
          setStars(data.stargazers_count);
          localStorage.setItem(CACHE_KEY, JSON.stringify({ count: data.stargazers_count, ts: Date.now() }));
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
