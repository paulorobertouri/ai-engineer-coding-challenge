import { defineConfig, devices } from "@playwright/test";

export default defineConfig({
  testDir: "./tests",
  reporter: [["html", { open: "never" }]],
  use: {
    baseURL: "http://localhost:5173",
    screenshot: "on",
    video: "off",
  },
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
