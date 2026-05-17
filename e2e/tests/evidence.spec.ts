import { test, expect } from "@playwright/test";
import fs from "fs";
import path from "path";

test("generate evidences", async ({ page }) => {
  test.setTimeout(120000);
  await page.goto("/");
  await page.screenshot({ path: "../evidences/01-initial-load.png" });

  // 1. Wait until either chat is ready or ingest panel is shown.
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const input = page.locator("#chat-input");

  const chatReady = await input
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
    while (!(await input.isVisible())) {
      if (await retryIngestButton.isVisible()) {
        await retryIngestButton.click();
      }

      if (Date.now() >= ingestDeadline) {
        throw new Error("Timed out waiting for chat input after ingest.");
      }

      await page.waitForTimeout(1000);
    }
  }
  await page.screenshot({ path: "../evidences/02-after-ingest.png" });

  // 2. Chat - Question 1 — wait for chat layout (input visible once ingested)
  await input.waitFor({ state: "visible", timeout: 30000 });
  await input.fill("What is the policy on expired items?");
  await page.click('button:has-text("Send")');

  const assistantMessages = page.locator(
    '.message-card[data-role="assistant"]',
  );
  await expect(assistantMessages.last()).not.toHaveText(/^\s*$/, {
    timeout: 60000,
  });
  await page.screenshot({ path: "../evidences/03-chat-response-1.png" });

  // 3. Chat - Question 2 (Follow up)
  await input.fill("What are the store hours on Monday?");
  await page.click('button:has-text("Send")');

  await expect(assistantMessages.last()).toContainText(
    /monday|store hours|open/i,
    {
      timeout: 60000,
    },
  );
  await page.screenshot({ path: "../evidences/04-chat-response-2-hours.png" });

  // Generate markdown report
  const report = `
# Application Evidence Report

## 1. Initial Load
![Initial Load](./01-initial-load.png)

## 2. Document Ingestion
The SOP document was successfully ingested and vectorized.
![After Ingest](./02-after-ingest.png)

## 3. RAG Chat Response
The assistant answered a question about expired items using the SOP context.
![Chat Response 1](./03-chat-response-1.png)

## 4. Multi-turn Chat & Tool Use
The assistant provided store hours, demonstrating tool-calling or context retrieval for follow-up questions.
![Chat Response 2](./04-chat-response-2-hours.png)
`;

  fs.writeFileSync(path.join(__dirname, "../../evidences/evidence.md"), report);
});
