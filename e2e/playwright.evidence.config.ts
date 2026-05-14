import { defineConfig, devices } from "@playwright/test";
import path from "path";

const root = path.resolve(__dirname, "..");

export default defineConfig({
  testDir: "./tests",
  reporter: [["html", { open: "never" }]],
  use: {
    baseURL: "http://127.0.0.1:49321",
    screenshot: "on",
    video: "off",
  },
  webServer: [
    {
      command: `Cors__AllowedOrigins__0=http://127.0.0.1:49321 dotnet run --project ${root}/backend/src/Api/Api.csproj --urls http://127.0.0.1:5199`,
      url: "http://127.0.0.1:5199/api/v1/health",
      reuseExistingServer: false,
      timeout: 60_000,
    },
    {
      command: `node -e "require('fs').writeFileSync('${root}/frontend/dist/config.json', JSON.stringify({ apiBaseUrl: 'http://127.0.0.1:5199' }))" && npm run preview --prefix ${root}/frontend -- --host 127.0.0.1 --port 49321 --strictPort`,
      url: "http://127.0.0.1:49321",
      reuseExistingServer: false,
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
