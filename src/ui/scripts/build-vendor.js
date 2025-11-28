import { fileURLToPath } from 'url';
import path from 'path';
import fs from 'fs/promises';
import { minify } from 'terser';
import postcss from 'postcss';
import cssnano from 'cssnano';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);
const rootDir = path.resolve(__dirname, '..');
const repoRoot = path.resolve(rootDir, '..');
const distDir = path.join(rootDir, 'dist');
const vendorDistDir = path.join(distDir, 'vendor');

async function ensureDir(target) {
    await fs.mkdir(target, { recursive: true });
}

async function writeFile(target, contents) {
    await ensureDir(path.dirname(target));
    await fs.writeFile(target, contents, 'utf8');
}

async function buildAirDatepicker() {
    const sourceCss = path.join(rootDir, 'node_modules', 'air-datepicker', 'air-datepicker.css');
    const sourceJs = path.join(rootDir, 'node_modules', 'air-datepicker', 'air-datepicker.js');

    const cssContent = await fs.readFile(sourceCss, 'utf8');
    const cssResult = await postcss([cssnano({ preset: 'default' })]).process(cssContent, { from: sourceCss });
    await writeFile(path.join(vendorDistDir, 'air-datepicker', 'air-datepicker.min.css'), cssResult.css);

    const jsContent = await fs.readFile(sourceJs, 'utf8');
    const jsResult = await minify(jsContent, {
        compress: true,
        mangle: true,
        ecma: 2019
    });

    if (!jsResult.code) {
        throw new Error('Failed to minify air-datepicker JavaScript.');
    }

    await writeFile(path.join(vendorDistDir, 'air-datepicker', 'air-datepicker.min.js'), jsResult.code);
}

async function buildBootstrapIcons() {
    const sourceCss = path.join(rootDir, 'node_modules', 'bootstrap-icons', 'font', 'bootstrap-icons.min.css');
    const sourceFonts = path.join(rootDir, 'node_modules', 'bootstrap-icons', 'font', 'fonts');

    const cssContent = await fs.readFile(sourceCss, 'utf8');
    await writeFile(path.join(vendorDistDir, 'bootstrap-icons', 'bootstrap-icons.css'), cssContent);

    const fontsTarget = path.join(vendorDistDir, 'bootstrap-icons', 'fonts');
    await ensureDir(fontsTarget);
    await fs.rm(fontsTarget, { recursive: true, force: true });
    await fs.mkdir(fontsTarget, { recursive: true });
    await fs.cp(sourceFonts, fontsTarget, { recursive: true });
}

async function copyVendorToProjects() {
    const targets = [
        path.join(repoRoot, 'LogMyDay.App', 'wwwroot', 'vendor'),
        path.join(repoRoot, 'LogMyDay.App.Mobile', 'wwwroot', 'vendor')
    ];

    const vendorEntries = await fs.readdir(vendorDistDir, { withFileTypes: true });

    for (const target of targets) {
        await ensureDir(target);
        for (const entry of vendorEntries) {
            if (!entry.isDirectory()) {
                continue;
            }

            const sourcePath = path.join(vendorDistDir, entry.name);
            const targetPath = path.join(target, entry.name);
            await fs.rm(targetPath, { recursive: true, force: true });
            await fs.cp(sourcePath, targetPath, { recursive: true });
        }
    }
}

async function main() {
    await ensureDir(distDir);
    await fs.rm(vendorDistDir, { recursive: true, force: true });

    await Promise.all([
        buildAirDatepicker(),
        buildBootstrapIcons()
    ]);

    await copyVendorToProjects();
}

main().catch((error) => {
    console.error('[build-vendor] Failed to process vendor assets:', error);
    process.exitCode = 1;
});
