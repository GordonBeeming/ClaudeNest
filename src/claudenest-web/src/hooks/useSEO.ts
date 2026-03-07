import { useEffect } from "react";

const SITE_NAME = "ClaudeNest";
const DEFAULT_DESCRIPTION =
  "Launch Claude Code sessions from anywhere. Browse dev folders, start remote sessions, and code through Anthropic's native remote-control — no source code ever leaves your machine.";
const DEFAULT_OG_IMAGE = "https://claudenest.com/logo.png";

interface SEOOptions {
  title?: string;
  description?: string;
  canonicalPath?: string;
  ogImage?: string;
  ogType?: "website" | "article";
  noindex?: boolean;
}

function setMetaTag(name: string, content: string, attribute = "name") {
  let el = document.querySelector(`meta[${attribute}="${name}"]`);
  if (!el) {
    el = document.createElement("meta");
    el.setAttribute(attribute, name);
    document.head.appendChild(el);
  }
  el.setAttribute("content", content);
}

function setLinkTag(rel: string, href: string) {
  let el = document.querySelector(`link[rel="${rel}"]`) as HTMLLinkElement | null;
  if (!el) {
    el = document.createElement("link");
    el.setAttribute("rel", rel);
    document.head.appendChild(el);
  }
  el.setAttribute("href", href);
}

function removeTag(selector: string) {
  document.querySelector(selector)?.remove();
}

export function useSEO({
  title,
  description = DEFAULT_DESCRIPTION,
  canonicalPath,
  ogImage = DEFAULT_OG_IMAGE,
  ogType = "website",
  noindex = false,
}: SEOOptions = {}) {
  useEffect(() => {
    const fullTitle = title ? `${title} - ${SITE_NAME}` : SITE_NAME;
    document.title = fullTitle;

    setMetaTag("description", description);

    // Open Graph
    setMetaTag("og:title", fullTitle, "property");
    setMetaTag("og:description", description, "property");
    setMetaTag("og:type", ogType, "property");
    setMetaTag("og:site_name", SITE_NAME, "property");
    setMetaTag("og:image", ogImage, "property");

    // Twitter Card
    setMetaTag("twitter:card", "summary_large_image");
    setMetaTag("twitter:title", fullTitle);
    setMetaTag("twitter:description", description);
    setMetaTag("twitter:image", ogImage);

    // Canonical
    if (canonicalPath !== undefined) {
      const canonicalUrl = `https://claudenest.com${canonicalPath}`;
      setLinkTag("canonical", canonicalUrl);
      setMetaTag("og:url", canonicalUrl, "property");
    }

    // Robots
    if (noindex) {
      setMetaTag("robots", "noindex, nofollow");
    } else {
      removeTag('meta[name="robots"]');
    }

    return () => {
      // Reset to defaults on unmount
      document.title = SITE_NAME;
    };
  }, [title, description, canonicalPath, ogImage, ogType, noindex]);
}
