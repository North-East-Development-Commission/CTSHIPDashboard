const { chromium } = require(process.env.CTSHIP_PLAYWRIGHT_PATH || 'playwright');
const path = require('node:path');
const os = require('node:os');

(async () => {
    const browser = await chromium.launch({
        executablePath: process.env.CTSHIP_BROWSER_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
        headless: true
    });
    const page = await browser.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    try {
        for (const width of [1440, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            for (const hmo of [false, true]) {
                await page.goto(`http://127.0.0.1:5098/preview/enrolment?errors=true&hmo=${hmo}`, { waitUntil: 'networkidle' });
                for (const field of ['FullName', 'Phone', 'NIN', 'DateOfBirth', 'ProviderId']) {
                    if (!(await page.locator(`[data-valmsg-for="${field}"]`).innerText()).trim())
                        throw new Error(`Missing inline error for ${field}`);
                }
                if (await page.locator('.validation-summary-errors li').count() !== 5)
                    throw new Error('Server validation summary does not list every field error');
                for (const field of ['Address', 'Phone', 'NIN']) {
                    const input = page.locator(`#${field}`);
                    if (await input.getAttribute('required') !== null || await input.getAttribute('data-val-required') !== null)
                        throw new Error(`${field} is incorrectly required`);
                }
                await page.screenshot({ path: path.join(os.tmpdir(), `ctship-enrollment-errors-${hmo ? 'hmo-' : ''}${width}.png`), fullPage: true });
                const documentWidth = await page.evaluate(() => document.documentElement.scrollWidth);
                if (documentWidth > width) throw new Error(`Enrollment form overflows at ${width}: ${documentWidth}`);
            }
            await page.goto('http://127.0.0.1:5098/preview/bulkerrors', { waitUntil: 'networkidle' });
            if (await page.locator('.alert-danger tbody tr').count() !== 3)
                throw new Error('Bulk errors table does not show all issues');
            if (await page.locator('.swal2-container').count())
                throw new Error('A popup obscures the inline bulk error report');
            if (!(await page.locator('.alert-danger').innerText()).includes("<script>invalid</script>"))
                throw new Error('Spreadsheet values are not displayed as escaped text');
            if (await page.locator('.alert-danger script').count())
                throw new Error('Spreadsheet error content was rendered as executable HTML');
            await page.screenshot({ path: path.join(os.tmpdir(), `ctship-bulk-errors-${width}.png`), fullPage: true });
            if (await page.evaluate(() => document.documentElement.scrollWidth) > width)
                throw new Error(`Bulk errors overflow the page at ${width}`);
        }
        await page.goto('http://127.0.0.1:5098/preview/enrolment', { waitUntil: 'networkidle' });
        await page.locator('#Phone').fill('invalid-phone');
        await page.locator('#NIN').fill('0');
        await page.getByRole('button', { name: 'REGISTER ENROLLEE' }).click();
        for (const field of ['FullName', 'DateOfBirth', 'Phone', 'NIN', 'State', 'HmoId', 'ProviderId']) {
            if (!(await page.locator(`[data-valmsg-for="${field}"]`).innerText()).trim())
                throw new Error(`Client validation did not show ${field} error`);
        }
        await page.route('**/Enrollees/GetLgasByState?**', route => route.fulfill({ json: ['Jere'] }));
        await page.route('**/Enrollees/GetWardsByLga?**', route => route.fulfill({ json: ['Test Ward'] }));
        await page.locator('#FullName').fill('Test Enrollee');
        await page.locator('#DateOfBirth').fill('1992-04-18');
        await page.locator('#Phone').fill('');
        await page.locator('#NIN').fill('');
        await page.locator('#HmoId').selectOption('1');
        await page.locator('#ProviderId').selectOption('1');
        await page.locator('#State').selectOption('Borno');
        await page.locator('#LGA').selectOption('Jere');
        await page.locator('#WardSelect').selectOption('__OTHER__');
        await page.getByRole('button', { name: 'REGISTER ENROLLEE' }).click();
        if (!(await page.locator('[data-valmsg-for="CustomWard"]').innerText()).includes('Enter the ward name'))
            throw new Error('Missing inline custom ward error');
        await page.locator('#CustomWard').fill('Test Ward');
        if (!(await page.evaluate(() => jQuery('.ct-enrollment-form').valid())))
            throw new Error('Valid form with blank Address, Phone, and NIN failed client validation');
        if (errors.length) throw new Error(errors.join('; '));
        console.log('PASS inline server/client errors and bulk error tables at desktop and mobile widths');
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
