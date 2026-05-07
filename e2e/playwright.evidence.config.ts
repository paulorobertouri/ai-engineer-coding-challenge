import { defineConfig, devices } from "@playwright/test";
import path from "path";

const root = path.resolve(__dirname, "..");

export default defineConfig({
  testDir: "./tests",
  reporter: [["html", { open: "never" }]],
  use: {
    baseURL: "http://localhost:5173",
    screenshot: "on",
    video: "off",
  },
  webServer: [
    {
      command: `dotnet run --project ${root}/backend/src/Api/Api.csproj --launch-profile http`,
      url: "http://localhost:5181/api/v1/health",
      reuseExistingServer: true,
      timeout: 60_000,
    },
    {
      command: `npm run dev --prefix ${root}/frontend`,
      url: "http://localhost:5173",
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
  outputDir: "../evidences/raw",
  projects: [
    {
      name: "google-chrome",
      use: {
        channel: "chrome",
        launchOptions: {
          executablePath: "/usr/bin/google-chrome",
        },
      },
    },
  ],
});
