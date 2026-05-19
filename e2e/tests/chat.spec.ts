import { test, expect, type Page } from "@playwright/test";

async function resetIngestion(page: Page) {
  await page.request.delete(
    "http://127.0.0.1:5199/api/v1/Ingest/reset?confirm=RESET",
  );
}

async function ensureChatReady(page: Page) {
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const openChatBtn = page.getByRole("button", { name: /open chat/i });
  const chatInput = page.locator("#chat-input");

  const chatReady = await chatInput
    .waitFor({ state: "visible", timeout: 5000 })
    .then(() => true)
    .catch(() => false);

  if (chatReady) {
    return;
  }

  if (await openChatBtn.isVisible().catch(() => false)) {
    await openChatBtn.click();
    await chatInput.waitFor({ state: "visible", timeout: 30000 });
    return;
  }

  await ingestBtn.waitFor({ state: "visible", timeout: 10000 });
  await expect(ingestBtn).toBeEnabled({ timeout: 30000 });
  await ingestBtn.click();

  const statusBanner = page.locator(".status-banner");
  await expect(statusBanner).toHaveAttribute("data-tone", /success|warning|info/, {
    timeout: 30000,
  });
  await expect(statusBanner).toContainText(
    /calling the ingest endpoint|ingested successfully|already ingested/i,
    { timeout: 30000 },
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

test("ingest and chat", async ({ page }) => {
  test.setTimeout(120000);
  await resetIngestion(page);
  await page.goto("/");
  const chatInput = page.locator("#chat-input");
  await ensureChatReady(page);

  await chatInput.waitFor({ state: "visible", timeout: 30000 });

  // 2. Chat — wait for the chat layout (input is always present once ingested)
  await chatInput.fill("What are the store hours on Monday?");
  await page.getByRole("button", { name: /send/i }).click();

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
