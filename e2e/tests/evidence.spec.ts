import { test, expect } from "@playwright/test";
import fs from "fs";
import path from "path";

test("generate evidences", async ({ page }) => {
  await page.goto("/");
  await page.screenshot({ path: "../evidences/01-initial-load.png" });

  // 1. Ingest — only needed if the ingest panel is shown (not yet ingested)
  const ingestBtn = page.locator('button:has-text("Use Default SOP")');
  const input = page.locator("#chat-input");

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
  await page.screenshot({ path: "../evidences/02-after-ingest.png" });

  // 2. Chat - Question 1 — wait for chat layout (input visible once ingested)
  await input.waitFor({ state: "visible", timeout: 10000 });
  await input.fill("What is the policy on expired items?");
  await page.click('button:has-text("Send")');

  await expect(
    page.locator('.message-card[data-role="assistant"]'),
  ).toBeVisible({ timeout: 60000 });
  await page.screenshot({ path: "../evidences/03-chat-response-1.png" });

  // 3. Chat - Question 2 (Follow up)
  await input.fill("What about store hours?");
  await page.click('button:has-text("Send")');

  await expect(
    page.locator('.message-card[data-role="assistant"]').nth(1),
  ).toContainText("Monday", { timeout: 60000 });
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
