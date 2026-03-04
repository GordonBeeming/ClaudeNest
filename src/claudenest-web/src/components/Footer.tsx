import { Link } from "react-router-dom";
import { Bird, Star, Github } from "lucide-react";

const GITHUB_URL = "https://github.com/GordonBeeming/ClaudeNest";

export function Footer() {
  return (
    <footer className="border-t border-gray-200 bg-white py-10 dark:border-gray-800 dark:bg-gray-950">
      <div className="mx-auto flex max-w-6xl flex-col items-center gap-6 px-4 sm:flex-row sm:justify-between">
        <Link
          to="/"
          className="flex items-center gap-2 text-gray-900 dark:text-white"
          aria-label="ClaudeNest home"
        >
          <Bird className="h-5 w-5 text-nest-500" aria-hidden="true" />
          <span className="font-semibold">ClaudeNest</span>
        </Link>

        <div className="flex flex-wrap items-center justify-center gap-6 text-sm text-gray-500 dark:text-gray-400">
          <Link to="/privacy" className="hover:text-gray-700 dark:hover:text-gray-200">
            Privacy Policy
          </Link>
          <a
            href={GITHUB_URL}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-gray-700 dark:hover:text-gray-200"
          >
            <Star className="h-3.5 w-3.5" aria-hidden="true" />
            Star on GitHub
          </a>
          <a
            href={`${GITHUB_URL}/issues`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 hover:text-gray-700 dark:hover:text-gray-200"
          >
            <Github className="h-3.5 w-3.5" aria-hidden="true" />
            Report an Issue
          </a>
        </div>

        <p className="text-xs text-gray-400 dark:text-gray-600">
          &copy; {new Date().getFullYear()} ClaudeNest
        </p>
      </div>
    </footer>
  );
}
