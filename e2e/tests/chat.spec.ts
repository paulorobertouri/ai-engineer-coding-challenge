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
    await expect(statusBanner).toHaveAttribute("data-tone", /success|warning/, {
      timeout: 90000,
    });
    await expect(statusBanner).toContainText(
      /ingested successfully|already ingested/i,
      {
        timeout: 90000,
      },
    );
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
