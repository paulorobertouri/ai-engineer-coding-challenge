import { test, expect } from "@playwright/test";

test("ingest and chat", async ({ page }) => {
  test.setTimeout(120000);
  await page.goto("/");

  // 1. Wait until either chat is ready or ingest panel is shown.
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const chatInput = page.locator("#chat-input");

  const chatReady = await chatInput
    .waitFor({ state: "visible", timeout: 10000 })
    .then(() => true)
    .catch(() => false);

  if (!chatReady) {
    await ingestBtn.waitFor({ state: "visible", timeout: 20000 });
    await ingestBtn.click();
    const statusBanner = page.locator(".status-banner");
    await expect(statusBanner).toHaveAttribute(
      "data-tone",
      /success|warning|info/,
      {
        timeout: 90000,
      },
    );
    await expect(statusBanner).toContainText(
      /calling the ingest endpoint|ingested successfully|already ingested/i,
      {
        timeout: 90000,
      },
    );

    const retryIngestButton = page.locator('button:has-text("Retry ingest")');
    const ingestDeadline = Date.now() + 120000;
    while (!(await chatInput.isVisible())) {
      if (await retryIngestButton.isVisible()) {
        await retryIngestButton.click();
      }

      if (Date.now() >= ingestDeadline) {
        throw new Error("Timed out waiting for chat input after ingest.");
      }

      await page.waitForTimeout(1000);
    }
  }

  // 2. Chat — wait for the chat layout (input is always present once ingested)
  await chatInput.waitFor({ state: "visible", timeout: 30000 });
  await chatInput.fill("What are the store hours on Monday?");
  await page.click('button:has-text("Send")');

  const assistantMessages = page.locator(
    '.message-card[data-role="assistant"]',
  );
  await expect(assistantMessages.last()).toContainText(
    /monday|store hours|open/i,
    {
      timeout: 60000,
    },
  );
  await expect(page.locator(".citations-panel")).not.toBeEmpty();
});
