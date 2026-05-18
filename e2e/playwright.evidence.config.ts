import { defineConfig, devices } from "@playwright/test";
import path from "path";

const root = path.resolve(__dirname, "..");

export default defineConfig({
  testDir: "./tests",
  outputDir: "../.build/test-results/e2e-evidence",
  reporter: [["html", { open: "never", outputFolder: "../.build/reports/playwright" }]],
  use: {
    baseURL: "http://127.0.0.1:49321",
    screenshot: "on",
    video: "off",
  },
  webServer: [
    {
      command: `mkdir -p ${root}/e2e/.tmp && rm -f ${root}/e2e/.tmp/vector-store.json && Cors__AllowedOrigins__0=http://127.0.0.1:49321 Challenge__SourceDocumentPath=${root}/knowledge-base/Grocery_Store_SOP.md Challenge__VectorStorePath=${root}/e2e/.tmp/vector-store.json OpenAI__ApiKey= dotnet run --project ${root}/backend/src/Api/Api.csproj --urls http://127.0.0.1:5199`,
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
      },
    },
  ],
});
