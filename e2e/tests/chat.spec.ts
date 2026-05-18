import { test, expect } from "@playwright/test";

test("ingest and chat", async ({ page }) => {
  test.setTimeout(120000);
  await page.goto("/");

  // 1. Wait until either chat is ready or ingest panel is shown.
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const chatInput = page.locator("#chat-input");

  const chatReady = await chatInput
    .waitFor({ state: "visible", timeout: 5000 })
    .then(() => true)
    .catch(() => false);

  if (!chatReady) {
    await ingestBtn.waitFor({ state: "visible", timeout: 10000 });
    await ingestBtn.click();
    const statusBanner = page.locator(".status-banner");
    await expect(statusBanner).toHaveAttribute(
      "data-tone",
      /success|warning|info/,
      { timeout: 30000 },
    );
    await expect(statusBanner).toContainText(
      /calling the ingest endpoint|ingested successfully|already ingested/i,
      { timeout: 30000 },
    );

    await expect
      .poll(
        async () => {
          if (await chatInput.isVisible()) {
            return "ready";
          }

          return (await statusBanner.textContent()) ?? "";
        },
        { timeout: 90000 },
      )
      .toMatch(/ready|ingested successfully|already ingested/i);
  }

  await chatInput.waitFor({ state: "visible", timeout: 30000 });

  // 2. Chat — wait for the chat layout (input is always present once ingested)
  await chatInput.fill("What are the store hours on Monday?");
  await page.click('button:has-text("Send")');

  const assistantMessages = page.locator(
    '.message-card[data-role="assistant"]',
  );
  const latestAssistantMessage = assistantMessages.last();
  await expect(latestAssistantMessage).toContainText(
    /monday|store hours|open|could not find enough relevant information/i,
    {
      timeout: 60000,
    },
  );

  const assistantText =
    (await latestAssistantMessage.textContent())?.toLowerCase() ?? "";
  if (!assistantText.includes("could not find enough relevant information")) {
    await expect(page.locator(".citations-panel")).not.toBeEmpty();
  }
});
