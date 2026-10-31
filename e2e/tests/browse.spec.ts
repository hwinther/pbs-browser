import { readFile } from "node:fs/promises";

import { expect, test } from "@playwright/test";

// FakePbsClient sample data (backend/src/Api/Pbs/FakePbsClient.cs).
const SNAPSHOT = "host/grafana/2026-07-01T04:30:00Z";

test.describe("browse and restore", () => {
  test.beforeEach(async ({ page }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { name: "PBS Browser" })).toBeVisible();
  });

  test("lists the snapshots grouped by host", async ({ page }) => {
    const snapshot = page.getByLabel("Snapshot");
    // Option text is a locale-formatted date, so assert on the groups and option values.
    await expect(snapshot.locator("optgroup")).toHaveCount(3);
    await expect(snapshot.locator("optgroup").first()).toHaveAttribute("label", "host/grafana");
    await expect(snapshot.locator(`option[value="${SNAPSHOT}"]`)).toHaveCount(1);
  });

  test("shows the catalog of a snapshot", async ({ page }) => {
    await page.getByLabel("Snapshot").selectOption(SNAPSHOT);

    // Only pxar/img archives are offered, without the index suffix.
    const archive = page.getByLabel("Archive");
    await expect(archive).toHaveValue("config.pxar");
    await expect(archive.locator("option")).toHaveCount(1);

    await expect(page.getByText("adminlist.txt")).toBeVisible();
    await expect(page.getByText("Geirangerheim.db")).toBeHidden();

    const folder = page.getByRole("button", { name: /worlds_local/ });
    await expect(folder).toHaveAttribute("aria-expanded", "false");
    await folder.click();
    await expect(folder).toHaveAttribute("aria-expanded", "true");

    const row = page.locator(".row.file", { hasText: "Geirangerheim.db" });
    await expect(row.locator(".size")).toHaveText("8.0 MB");
  });

  test("restores a file as a download", async ({ page }) => {
    await page.getByLabel("Snapshot").selectOption(SNAPSHOT);
    await page.getByRole("button", { name: /worlds_local/ }).click();

    const row = page.locator(".row.file", { hasText: "Geirangerheim.db" });
    const downloadPromise = page.waitForEvent("download");
    await row.getByRole("link", { name: "Download" }).click();
    const download = await downloadPromise;

    expect(download.suggestedFilename()).toBe("Geirangerheim.db");
    const body = await readFile(await download.path(), "utf8");
    expect(body).toContain("restored /worlds_local/Geirangerheim.db");
    expect(body).toContain(`from config.pxar in ${SNAPSHOT}`);
  });
});

test.describe("identity", () => {
  // In the cluster Traefik/Authelia inject these; here the test plays the forward-auth proxy.
  test.use({ extraHTTPHeaders: { "Remote-User": "e2e-user", "Remote-Email": "e2e@example.com" } });

  test("shows the forward-auth user", async ({ page }) => {
    await page.goto("/");
    await expect(page.getByText("Signed in as e2e-user")).toBeVisible();
  });
});
