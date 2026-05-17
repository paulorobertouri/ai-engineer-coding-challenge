import { test, expect, type Page } from "@playwright/test";

async function stabilizePageForScreenshots(page: Page): Promise<void> {
  await page.setViewportSize({ width: 1440, height: 960 });
  await page.emulateMedia({ reducedMotion: "reduce" });
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation: none !important;
        transition: none !important;
        caret-color: transparent !important;
      }

      .message-time {
        visibility: hidden !important;
      }
    `,
  });
}

async function completeIngest(page: Page): Promise<void> {
  const chatInput = page.locator("#chat-input");
  const ingestButton = page.locator('button:has-text("Use Default SOP")');

  const chatReady = await chatInput
    .waitFor({ state: "visible", timeout: 10_000 })
    .then(() => true)
    .catch(() => false);

  if (chatReady) {
    return;
  }

  await ingestButton.waitFor({ state: "visible", timeout: 20_000 });
  await ingestButton.click();
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

test.describe("visual regression", () => {
  test.describe.configure({ mode: "serial" });

  test("visual: setup flow", async ({ page }) => {
    test.setTimeout(120000);
    await page.request.delete(
      "http://127.0.0.1:5199/api/v1/Ingest/reset?confirm=RESET",
    );
    await page.goto("/");
    await stabilizePageForScreenshots(page);

    await expect(page.locator("main.app-shell--setup")).toBeVisible({
      timeout: 15_000,
    });
    const setupShell = page.locator("main.app-shell--setup");
    await expect(setupShell).toHaveScreenshot("setup-flow.png", {
      animations: "disabled",
      scale: "css",
      maxDiffPixelRatio: 0.01,
    });
  });

  test("visual: chat flow", async ({ page }) => {
    test.setTimeout(120000);
    await page.goto("/");
    await stabilizePageForScreenshots(page);
    await completeIngest(page);

    const chatInput = page.locator("#chat-input");
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

    await expect(page.locator("main.app-shell")).toHaveScreenshot(
      "chat-flow.png",
      {
        animations: "disabled",
        scale: "css",
        maxDiffPixelRatio: 0.01,
      },
    );
  });

  test("visual: citation source viewer flow", async ({ page }) => {
    test.setTimeout(120000);
    await page.goto("/");
    await stabilizePageForScreenshots(page);
    await completeIngest(page);

    const chatInput = page.locator("#chat-input");
    await chatInput.fill("What are the store hours on Monday?");
    await page.click('button:has-text("Send")');

    const citationButton = page.locator(".citation-select-btn").first();
    await expect(citationButton).toBeVisible({ timeout: 60000 });
    await citationButton.click();
    await expect(
      page.locator(".source-viewer-item--selected").first(),
    ).toBeVisible({
      timeout: 30000,
    });

    await expect(page.locator("main.app-shell")).toHaveScreenshot(
      "citation-source-flow.png",
      {
        animations: "disabled",
        scale: "css",
        maxDiffPixelRatio: 0.01,
      },
    );
  });
});
