import { test, expect } from "@playwright/test";

test("ingest and chat", async ({ page }) => {
  await page.goto("/");

  // 1. Ingest — only needed if the ingest panel is shown (not yet ingested)
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const chatInput = page.locator("#chat-input");

  const panelVisible = await ingestBtn
    .waitFor({ state: "visible", timeout: 5000 })
    .then(() => true)
    .catch(() => false);

  if (panelVisible) {
    await ingestBtn.click();
    await expect(page.locator(".status-banner")).toContainText(
      "Document ingested successfully",
      { timeout: 60000 },
    );
  }

  // 2. Chat — wait for the chat layout (input is always present once ingested)
  await chatInput.waitFor({ state: "visible", timeout: 10000 });
  await chatInput.fill("What are the store hours?");
  await page.click('button:has-text("Send")');

  await expect(
    page.locator('.message-card[data-role="assistant"]'),
  ).toContainText("Monday", { timeout: 60000 });
  await expect(page.locator(".citations-panel")).not.toBeEmpty();
});
