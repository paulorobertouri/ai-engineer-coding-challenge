import { test, expect } from "@playwright/test";

test("ingest and chat", async ({ page }) => {
  await page.goto("/");

  // 1. Ingest
  await page.click('button:has-text("Run Ingest")');
  await expect(page.locator(".status-banner")).toContainText(
    "SOP document ingested successfully",
    { timeout: 60000 },
  );

  // 2. Chat
  const input = page.locator('textarea[placeholder*="Example:"]');
  await input.fill("What are the store hours?");
  await page.click('button:has-text("Send")');

  await expect(
    page.locator('.message-card[data-role="assistant"]'),
  ).toContainText("Monday", { timeout: 60000 });
  await expect(page.locator(".citations-panel")).not.toBeEmpty();
});
