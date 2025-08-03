import { test, expect } from '@playwright/test';

test.describe('Calendar Dark Theme Visibility', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to login page and login
    await page.goto('/login');
    
    // Fill credentials (using test credentials)
    await page.fill('input[name="username"]', 'admin');
    await page.fill('input[name="password"]', 'password');
    await page.click('button[type="submit"]');
    
    // Wait for redirect to home page
    await page.waitForURL('/');
  });

  test('calendar values are visible in light theme', async ({ page }) => {
    // Navigate to calendar
    await page.goto('/calendar');
    await page.waitForLoadState('networkidle');

    // Ensure we're in light theme
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-bs-theme', 'light');
    });

    // Wait for calendar to load
    await page.waitForSelector('.calendar-table');

    // Check if calendar values are visible by looking for contrast
    const calendarValues = page.locator('.calendar-value');
    const count = await calendarValues.count();
    
    if (count > 0) {
      // Get first calendar value for testing
      const firstValue = calendarValues.first();
      
      // Check if the element is visible
      await expect(firstValue).toBeVisible();
      
      // Check computed styles for good contrast
      const styles = await firstValue.evaluate(el => {
        const computed = window.getComputedStyle(el);
        return {
          backgroundColor: computed.backgroundColor,
          color: computed.color,
          fontWeight: computed.fontWeight
        };
      });
      
      // Verify it has appropriate styling
      expect(styles.backgroundColor).toContain('rgb(25, 135, 84)'); // Success green
      expect(styles.color).toContain('rgb(255, 255, 255)'); // White text
    }
  });

  test('calendar values are visible in dark theme', async ({ page }) => {
    // Navigate to calendar
    await page.goto('/calendar');
    await page.waitForLoadState('networkidle');

    // Switch to dark theme
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-bs-theme', 'dark');
    });

    // Wait for theme to apply
    await page.waitForTimeout(500);

    // Wait for calendar to load
    await page.waitForSelector('.calendar-table');

    // Check if calendar values are visible in dark theme
    const calendarValues = page.locator('.calendar-value');
    const count = await calendarValues.count();
    
    if (count > 0) {
      // Get first calendar value for testing
      const firstValue = calendarValues.first();
      
      // Check if the element is visible
      await expect(firstValue).toBeVisible();
      
      // Check computed styles for good contrast in dark theme
      const styles = await firstValue.evaluate(el => {
        const computed = window.getComputedStyle(el);
        return {
          backgroundColor: computed.backgroundColor,
          color: computed.color,
          fontWeight: computed.fontWeight,
          borderColor: computed.borderColor
        };
      });
      
      // Verify dark theme styling is applied
      expect(styles.backgroundColor).toContain('rgb(40, 167, 69)'); // Brighter green for dark theme
      expect(styles.color).toContain('rgb(255, 255, 255)'); // White text
      expect(styles.fontWeight).toBe('600'); // Bold font
      expect(styles.borderColor).toContain('rgb(52, 206, 87)'); // Green border
    }
  });

  test('calendar empty values are visible in dark theme', async ({ page }) => {
    // Navigate to calendar
    await page.goto('/calendar');
    await page.waitForLoadState('networkidle');

    // Switch to dark theme
    await page.evaluate(() => {
      document.documentElement.setAttribute('data-bs-theme', 'dark');
    });

    // Wait for theme to apply
    await page.waitForTimeout(500);

    // Check if calendar empty values are visible in dark theme
    const calendarEmpty = page.locator('.calendar-empty');
    const count = await calendarEmpty.count();
    
    if (count > 0) {
      // Get first empty value for testing
      const firstEmpty = calendarEmpty.first();
      
      // Check if the element is visible
      await expect(firstEmpty).toBeVisible();
      
      // Check computed styles for good contrast in dark theme
      const styles = await firstEmpty.evaluate(el => {
        const computed = window.getComputedStyle(el);
        return {
          backgroundColor: computed.backgroundColor,
          color: computed.color,
          fontWeight: computed.fontWeight,
          borderColor: computed.borderColor
        };
      });
      
      // Verify dark theme styling is applied for empty values
      expect(styles.backgroundColor).toContain('rgb(108, 78, 0)'); // Dark yellow/brown background
      expect(styles.color).toContain('rgb(255, 204, 0)'); // Bright yellow text
      expect(styles.fontWeight).toBe('600'); // Bold font
      expect(styles.borderColor).toContain('rgb(138, 102, 0)'); // Yellow border
    }
  });

  test('theme toggle works correctly', async ({ page }) => {
    // Navigate to calendar
    await page.goto('/calendar');
    await page.waitForLoadState('networkidle');

    // Find and click theme toggle
    const themeToggle = page.locator('.theme-toggle');
    await expect(themeToggle).toBeVisible();

    // Get initial theme
    const initialTheme = await page.evaluate(() => {
      return document.documentElement.getAttribute('data-bs-theme');
    });

    // Click theme toggle
    await themeToggle.click();
    
    // Wait for theme change
    await page.waitForTimeout(500);

    // Verify theme changed
    const newTheme = await page.evaluate(() => {
      return document.documentElement.getAttribute('data-bs-theme');
    });

    expect(newTheme).not.toBe(initialTheme);
    expect(['light', 'dark']).toContain(newTheme);
  });
});
