const { chromium } = require(process.env.CTSHIP_PLAYWRIGHT_PATH || 'playwright');
const path = require('node:path');
const os = require('node:os');

(async () => {
    const browser = await chromium.launch({
        executablePath: process.env.CTSHIP_BROWSER_PATH || 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
        headless: true,
        args: ['--use-fake-device-for-media-stream', '--use-fake-ui-for-media-stream']
    });
    const context = await browser.newContext({ permissions: ['camera'], viewport: { width: 1440, height: 1000 } });
    const page = await context.newPage();
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    const results = [];
    try {
        for (const width of [1440, 390, 320]) {
            await page.setViewportSize({ width, height: 1000 });
            await page.goto('http://127.0.0.1:5098/preview/dashboard', { waitUntil: 'networkidle' });
            await page.evaluate(() => document.fonts.ready);
            if (await page.locator('#hmoProviderPreview tbody tr').count() !== 5)
                throw new Error('HMO provider preview should show five sample facilities');
            if (await page.getByRole('link', { name: 'View all providers' }).getAttribute('href') !== '/Hmo/MyProviders')
                throw new Error('Provider full-list link is incorrect');
            const state = await page.evaluate(() => ({
                width: innerWidth,
                documentWidth: document.documentElement.scrollWidth,
                icons: document.querySelectorAll('.fa-solid.ct-kpi-icon').length,
                fontLoaded: document.fonts.check('900 16px "Font Awesome 6 Free"'),
                topbarBottom: document.querySelector('.ct-topbar')?.getBoundingClientRect().bottom,
                contentTop: document.querySelector('.ct-page')?.getBoundingClientRect().top,
                charts: [...document.querySelectorAll('canvas')].map(canvas => {
                    const pixels = canvas.getContext('2d').getImageData(0, 0, canvas.width, canvas.height).data;
                    let painted = 0;
                    for (let i = 3; i < pixels.length; i += 4) if (pixels[i]) painted++;
                    return { id: canvas.id, width: canvas.width, painted };
                })
            }));
            await page.screenshot({ path: path.join(os.tmpdir(), 'ctship-dashboard-' + width + '.png') });
            if (state.documentWidth > width) throw new Error('Page overflows at ' + width + ': ' + JSON.stringify(state));
            if (state.contentTop < state.topbarBottom) throw new Error('Topbar overlaps content: ' + JSON.stringify(state));
            if (!state.fontLoaded || state.icons < 20) throw new Error('KPI icons did not load');
            if (state.charts.some(chart => chart.painted < 100)) throw new Error('Blank chart: ' + JSON.stringify(state.charts));
            results.push(state);
        }
        await page.setViewportSize({ width: 1440, height: 1000 });
        await page.goto('http://127.0.0.1:5098/preview/enrolment', { waitUntil: 'networkidle' });
        await page.getByRole('button', { name: 'Open camera' }).click();
        await page.waitForFunction(() => document.querySelector('video')?.videoWidth > 0);
        await page.getByRole('button', { name: 'Capture photo' }).click();
        await page.waitForFunction(() => document.querySelector('input[name="PhotoFile"]').files.length === 1);
        const photo = await page.evaluate(() => {
            const file = document.querySelector('input[name="PhotoFile"]').files[0];
            return { name: file.name, type: file.type, size: file.size,
                cameraStopped: document.querySelector('video').srcObject === null };
        });
        if (!photo.cameraStopped || photo.size === 0 || photo.type !== 'image/jpeg') throw new Error('Camera capture failed');
        if (await page.locator('select#VulnerabilityCategorySelect option[value="__OTHER__"]').count()) throw new Error('Other category still offered');
        await page.screenshot({ path: path.join(os.tmpdir(), 'ctship-camera.png') });
        if (errors.length) throw new Error('Browser errors: ' + errors.join('; '));
        console.log(JSON.stringify({ viewports: results, photo, errors }, null, 2));
    } finally {
        await browser.close();
    }
})().catch(error => { console.error(error); process.exitCode = 1; });
