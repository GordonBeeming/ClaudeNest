import { ApplicationInsights } from "@microsoft/applicationinsights-web";
import { appInsightsConnectionString } from "./config";

let appInsights: ApplicationInsights | null = null;

if (appInsightsConnectionString) {
  appInsights = new ApplicationInsights({
    config: {
      connectionString: appInsightsConnectionString,
      enableAutoRouteTracking: true,
    },
  });
  appInsights.loadAppInsights();
}

export { appInsights };
