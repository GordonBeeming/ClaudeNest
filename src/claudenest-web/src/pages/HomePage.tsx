import { useState, useEffect } from "react";
import { Navigate } from "react-router-dom";
import { useAuth0 } from "@auth0/auth0-react";
import {
  Download,
  FolderOpen,
  Code,
  ShieldCheck,
  KeyRound,
  MonitorOff,
  ArrowUpRight,
  Monitor,
  Activity,
  Laptop,
  Power,
  Lock,
  History,
} from "lucide-react";
import { isAuth0Configured } from "../config";
import { MarketingNav } from "../components/MarketingNav";
import { Footer } from "../components/Footer";
import { PricingCards } from "../components/PricingCards";
import { setPlanIntent } from "../utils/planIntent";
import type { PlanInfo } from "../types";

function HeroSection() {
  return (
    <section className="relative overflow-hidden bg-gradient-to-br from-nest-50 via-white to-nest-100 dark:from-gray-950 dark:via-gray-900 dark:to-nest-950/30">
      <div className="mx-auto max-w-6xl px-4 py-20 text-center sm:py-28">
        <h1 className="text-4xl font-extrabold tracking-tight text-gray-900 dark:text-white sm:text-5xl lg:text-6xl">
          Launch Claude Code Sessions
          <br />
          <span className="text-nest-500">From Anywhere</span>
        </h1>
        <p className="mx-auto mt-6 max-w-2xl text-lg text-gray-600 dark:text-gray-400">
          ClaudeNest is a lightweight remote launcher for Claude Code.
          Browse your dev folders, start sessions, and interact through
          Anthropic's native remote-control — no source code ever leaves your machine.
        </p>
        <div className="mt-10 flex flex-wrap items-center justify-center gap-4">
          <a
            href="#pricing"
            className="rounded-lg bg-nest-500 px-6 py-3 text-sm font-semibold text-white shadow-sm hover:bg-nest-600"
          >
            Get Started
          </a>
          <a
            href="#how-it-works"
            className="rounded-lg border border-gray-300 px-6 py-3 text-sm font-semibold text-gray-700 hover:bg-gray-100 dark:border-gray-700 dark:text-gray-300 dark:hover:bg-gray-800"
          >
            How It Works
          </a>
        </div>
      </div>
      {/* Decorative gradient blur */}
      <div className="pointer-events-none absolute -bottom-24 left-1/2 h-64 w-[600px] -translate-x-1/2 rounded-full bg-nest-400/20 blur-3xl dark:bg-nest-500/10" aria-hidden="true" />
    </section>
  );
}

function HowItWorksSection() {
  const steps = [
    {
      icon: Download,
      title: "Install the Agent",
      description: "Single binary, no runtime dependencies. Runs as a service on Windows, macOS, or Linux.",
    },
    {
      icon: FolderOpen,
      title: "Browse & Launch",
      description: "Pick a folder from your dev machine and click start — all from the web dashboard.",
    },
    {
      icon: Code,
      title: "Code in claude.ai",
      description: "Interact via Anthropic's native remote-control. ClaudeNest never touches your code.",
    },
  ];

  return (
    <section id="how-it-works" className="bg-white py-20 dark:bg-gray-950">
      <div className="mx-auto max-w-6xl px-4">
        <h2 className="text-center text-3xl font-bold text-gray-900 dark:text-white">
          How It Works
        </h2>
        <p className="mx-auto mt-3 max-w-xl text-center text-gray-500 dark:text-gray-400">
          Three steps to remote Claude Code sessions.
        </p>
        <div className="mt-14 grid gap-8 md:grid-cols-3">
          {steps.map((step, i) => (
            <div key={step.title} className="flex flex-col items-center text-center">
              <div className="flex h-14 w-14 items-center justify-center rounded-2xl bg-nest-100 dark:bg-nest-900/30">
                <step.icon className="h-7 w-7 text-nest-600 dark:text-nest-400" aria-hidden="true" />
              </div>
              <div className="mt-2 flex h-8 w-8 items-center justify-center rounded-full bg-nest-500 text-sm font-bold text-white" aria-hidden="true">
                {i + 1}
              </div>
              <h3 className="mt-4 text-lg font-semibold text-gray-900 dark:text-white">
                {step.title}
              </h3>
              <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
                {step.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function SecuritySection() {
  const cards = [
    {
      icon: ShieldCheck,
      title: "No Source Code Relay",
      description: "ClaudeNest never reads, transmits, or stores your source code. It's purely a session launcher.",
    },
    {
      icon: KeyRound,
      title: "No API Key Access",
      description: "Your Claude API keys stay on your machine. The cloud backend never sees them.",
    },
    {
      icon: MonitorOff,
      title: "No Terminal Streaming",
      description: "Uses Anthropic's native remote-control protocol. No terminal I/O passes through ClaudeNest.",
    },
    {
      icon: ArrowUpRight,
      title: "Outbound-Only Agent",
      description: "The agent connects outbound to the backend. No inbound ports, no firewall rules required.",
    },
  ];

  return (
    <section className="bg-gray-50 py-20 dark:bg-gray-900">
      <div className="mx-auto max-w-6xl px-4">
        <h2 className="text-center text-3xl font-bold text-gray-900 dark:text-white">
          Security First
        </h2>
        <p className="mx-auto mt-3 max-w-xl text-center text-gray-500 dark:text-gray-400">
          ClaudeNest is designed so your code and credentials never leave your machine.
        </p>
        <div className="mt-14 grid gap-6 sm:grid-cols-2 lg:grid-cols-4">
          {cards.map((card) => (
            <div
              key={card.title}
              className="rounded-xl border border-gray-200 bg-white p-6 dark:border-gray-800 dark:bg-gray-950"
            >
              <card.icon className="h-8 w-8 text-nest-500" aria-hidden="true" />
              <h3 className="mt-4 font-semibold text-gray-900 dark:text-white">
                {card.title}
              </h3>
              <p className="mt-2 text-sm text-gray-500 dark:text-gray-400">
                {card.description}
              </p>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function FeaturesSection() {
  const features = [
    { icon: Monitor, title: "Multi-Machine Management", description: "Manage agents across all your dev machines from one dashboard." },
    { icon: Activity, title: "Real-Time Status", description: "See agent connectivity and session states update live via SignalR." },
    { icon: Laptop, title: "Cross-Platform", description: "Native AOT binaries for Windows, macOS, and Linux — no runtime needed." },
    { icon: Power, title: "Auto-Start Service", description: "Install as a system service that starts on boot. Set it and forget it." },
    { icon: Lock, title: "Permission Modes", description: "Control what the agent is allowed to do with configurable permission levels." },
    { icon: History, title: "Session History", description: "Track all sessions with timestamps, exit codes, and error details." },
  ];

  return (
    <section className="bg-white py-20 dark:bg-gray-950">
      <div className="mx-auto max-w-6xl px-4">
        <h2 className="text-center text-3xl font-bold text-gray-900 dark:text-white">
          Features
        </h2>
        <div className="mt-14 grid gap-8 sm:grid-cols-2 lg:grid-cols-3">
          {features.map((f) => (
            <div key={f.title} className="flex gap-4">
              <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-nest-100 dark:bg-nest-900/30">
                <f.icon className="h-5 w-5 text-nest-600 dark:text-nest-400" aria-hidden="true" />
              </div>
              <div>
                <h3 className="font-semibold text-gray-900 dark:text-white">{f.title}</h3>
                <p className="mt-1 text-sm text-gray-500 dark:text-gray-400">{f.description}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function PricingSectionWithAuth() {
  const auth0 = useAuth0();
  return <PricingSectionInner onLogin={(planId) => {
    setPlanIntent(planId);
    auth0.loginWithRedirect();
  }} />;
}

function PricingSectionInner({ onLogin }: { onLogin?: (planId: string) => void }) {
  const [plans, setPlans] = useState<PlanInfo[]>([]);
  const [loading, setLoading] = useState(true);
  const [selecting, setSelecting] = useState<string | null>(null);

  useEffect(() => {
    fetch("/api/plans")
      .then((res) => res.json())
      .then((data) => setPlans(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleSelectPlan = (planId: string) => {
    if (!onLogin) return;
    setSelecting(planId);
    onLogin(planId);
  };

  if (loading) {
    return (
      <section id="pricing" className="bg-gray-50 py-20 dark:bg-gray-900">
        <div className="mx-auto max-w-4xl px-4 text-center">
          <h2 className="text-3xl font-bold text-gray-900 dark:text-white">Pricing</h2>
          <div className="mt-10 flex justify-center">
            <div className="h-5 w-5 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" role="status" aria-label="Loading pricing" />
          </div>
        </div>
      </section>
    );
  }

  return (
    <section id="pricing" className="bg-gray-50 py-20 dark:bg-gray-900">
      <div className="mx-auto max-w-4xl px-4">
        <h2 className="text-center text-3xl font-bold text-gray-900 dark:text-white">
          Pricing
        </h2>
        <p className="mx-auto mt-3 max-w-xl text-center text-gray-500 dark:text-gray-400">
          All plans include full access to ClaudeNest. Pick the one that fits your workflow.
        </p>
        <div className="mt-10">
          <PricingCards
            plans={plans}
            onSelectPlan={handleSelectPlan}
            selecting={selecting}
          />
        </div>
        <div className="mx-auto mt-6 max-w-xl rounded-lg border border-amber-200 bg-amber-50 px-4 py-3 text-sm text-amber-800 dark:border-amber-800 dark:bg-amber-950/30 dark:text-amber-300">
          <strong>Prerequisite:</strong> ClaudeNest uses Anthropic's{" "}
          <a
            href="https://code.claude.com/docs/en/remote-control"
            target="_blank"
            rel="noopener noreferrer"
            className="underline hover:text-amber-900 dark:hover:text-amber-200"
          >
            remote-control
          </a>{" "}
          feature, which requires a{" "}
          <a
            href="https://claude.ai/settings/billing"
            target="_blank"
            rel="noopener noreferrer"
            className="font-semibold underline hover:text-amber-900 dark:hover:text-amber-200"
          >
            Claude Max subscription
          </a>{" "}
          (separate from ClaudeNest).
        </div>
      </div>
    </section>
  );
}

function HomePageContent() {
  return (
    <div className="flex min-h-screen flex-col">
      <a href="#main-content" className="sr-only focus:not-sr-only focus:fixed focus:left-2 focus:top-2 focus:z-[100] focus:rounded-lg focus:bg-nest-500 focus:px-4 focus:py-2 focus:text-white">
        Skip to content
      </a>
      <MarketingNav />
      <main id="main-content" className="flex-1">
        <HeroSection />
        <HowItWorksSection />
        <SecuritySection />
        <FeaturesSection />
        {isAuth0Configured ? <PricingSectionWithAuth /> : <PricingSectionInner />}
      </main>
      <Footer />
    </div>
  );
}

function AuthAwareHomePage() {
  const { isAuthenticated, isLoading } = useAuth0();

  if (isLoading) {
    return (
      <div className="flex min-h-screen items-center justify-center">
        <div className="h-6 w-6 animate-spin rounded-full border-2 border-nest-500 border-t-transparent" role="status" aria-label="Loading" />
      </div>
    );
  }

  if (isAuthenticated) {
    return <Navigate to="/dashboard" replace />;
  }

  return <HomePageContent />;
}

export function HomePage() {
  if (!isAuth0Configured) {
    return <Navigate to="/dashboard" replace />;
  }

  return <AuthAwareHomePage />;
}
