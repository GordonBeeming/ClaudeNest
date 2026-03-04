import { MarketingNav } from "../components/MarketingNav";
import { Footer } from "../components/Footer";

export function PrivacyPolicy() {
  return (
    <div className="flex min-h-screen flex-col">
      <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-[100] focus:rounded-lg focus:bg-nest-500 focus:px-4 focus:py-2 focus:text-white">
        Skip to content
      </a>
      <MarketingNav />
      <main id="main-content" className="mx-auto max-w-3xl flex-1 px-4 py-12">
        <h1 className="text-3xl font-bold text-gray-900 dark:text-white">
          Privacy Policy
        </h1>
        <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
          Last updated: March 2026
        </p>

        <div className="mt-8 space-y-8 text-sm leading-relaxed text-gray-700 dark:text-gray-300">
          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              What ClaudeNest Is
            </h2>
            <p className="mt-3">
              ClaudeNest is a remote session launcher for Claude Code. It allows you to
              browse folders on your dev machines and start Claude Code remote-control
              sessions from a web dashboard. You interact with Claude through Anthropic's
              native interface at claude.ai — ClaudeNest is purely a session manager.
            </p>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              What We Do NOT Access
            </h2>
            <ul className="mt-3 list-disc space-y-2 pl-5">
              <li>
                <strong>Source code</strong> — ClaudeNest never reads, transmits, or stores
                your source code. The agent only lists directory names for folder browsing.
              </li>
              <li>
                <strong>API keys and credentials</strong> — Your Claude API keys, tokens, and
                credentials remain on your local machine. The cloud backend never sees them.
              </li>
              <li>
                <strong>Terminal I/O</strong> — ClaudeNest does not stream terminal input or
                output. All interaction happens through Anthropic's native remote-control
                protocol, which connects directly between your machine and Anthropic's servers.
              </li>
              <li>
                <strong>File contents</strong> — The agent does not read or transmit file
                contents. It only provides directory listings (folder names) for navigation.
              </li>
            </ul>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              What We Do Store
            </h2>
            <ul className="mt-3 list-disc space-y-2 pl-5">
              <li>
                <strong>Account information</strong> — Email address, display name, and account
                settings necessary to provide the service.
              </li>
              <li>
                <strong>Agent metadata</strong> — Agent names, hostnames, OS type, version
                information, and connectivity status.
              </li>
              <li>
                <strong>Session metadata</strong> — Session IDs, start/end times, working
                directory paths, session states, and exit codes. This does not include any
                session content or terminal output.
              </li>
              <li>
                <strong>Payment information</strong> — Billing is processed through Stripe.
                We store your Stripe customer ID and subscription status. We do not store
                credit card numbers or bank details — those are held by Stripe.
              </li>
            </ul>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              Authentication
            </h2>
            <p className="mt-3">
              We use <a href="https://auth0.com" target="_blank" rel="noopener noreferrer" className="font-medium text-nest-600 hover:text-nest-500 dark:text-nest-400">Auth0</a> for
              user authentication. You can sign in with your Google or GitHub account.
              We receive your email address and display name from the social login provider.
              We do not receive or store your social login passwords.
            </p>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              Payment Processing
            </h2>
            <p className="mt-3">
              Payments are processed by <a href="https://stripe.com" target="_blank" rel="noopener noreferrer" className="font-medium text-nest-600 hover:text-nest-500 dark:text-nest-400">Stripe</a>.
              When you subscribe to a plan, you are redirected to Stripe's checkout page.
              Your payment details are handled entirely by Stripe and are subject to{" "}
              <a href="https://stripe.com/privacy" target="_blank" rel="noopener noreferrer" className="font-medium text-nest-600 hover:text-nest-500 dark:text-nest-400">Stripe's Privacy Policy</a>.
            </p>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              Cookies & Local Storage
            </h2>
            <ul className="mt-3 list-disc space-y-2 pl-5">
              <li>
                <strong>Authentication tokens</strong> — Stored in localStorage by Auth0 to
                maintain your login session.
              </li>
              <li>
                <strong>Plan selection intent</strong> — Temporarily stored in localStorage
                when you select a plan before signing in, so we can resume the flow after
                authentication.
              </li>
              <li>
                <strong>GitHub star count</strong> — Cached in sessionStorage to reduce API
                calls to GitHub.
              </li>
            </ul>
            <p className="mt-3">
              We do not use tracking cookies or third-party advertising cookies.
            </p>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              Data Security
            </h2>
            <p className="mt-3">
              All communication between the agent and backend uses encrypted connections (TLS).
              Agent credentials are hashed before storage. The agent only makes outbound
              connections — no inbound ports or firewall rules are required.
            </p>
          </section>

          <section>
            <h2 className="text-xl font-semibold text-gray-900 dark:text-white">
              Contact
            </h2>
            <p className="mt-3">
              If you have questions about this privacy policy, please contact us at{" "}
              <a href="mailto:support@claudenest.com" className="font-medium text-nest-600 hover:text-nest-500 dark:text-nest-400">
                support@claudenest.com
              </a>{" "}
              or open an issue on{" "}
              <a href="https://github.com/GordonBeeming/ClaudeNest/issues" target="_blank" rel="noopener noreferrer" className="font-medium text-nest-600 hover:text-nest-500 dark:text-nest-400">
                GitHub
              </a>.
            </p>
          </section>
        </div>
      </main>

      <Footer />
    </div>
  );
}
