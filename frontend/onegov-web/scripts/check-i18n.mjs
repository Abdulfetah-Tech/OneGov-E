// Reports missing/extra translation keys and placeholder mismatches against English.
// Exit code 1 when any key is missing or a placeholder differs, so CI can fail on it.
import { readFileSync, readdirSync } from 'node:fs';
import { join, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const dir = join(dirname(fileURLToPath(import.meta.url)), '..', 'public', 'i18n');

function flatten(node, prefix = '', out = new Map()) {
  for (const [key, value] of Object.entries(node)) {
    if (prefix === '' && key === '_meta') continue;
    const path = prefix ? `${prefix}.${key}` : key;
    if (typeof value === 'string') out.set(path, value);
    else flatten(value, path, out);
  }
  return out;
}

const placeholders = (text) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort().join(',');
const load = (file) => JSON.parse(readFileSync(join(dir, file), 'utf8'));

const english = flatten(load('en.json'));
let failed = false;

for (const file of readdirSync(dir).filter((f) => f.endsWith('.json') && f !== 'en.json').sort()) {
  const data = load(file);
  const entries = flatten(data);
  const missing = [...english.keys()].filter((k) => !entries.has(k));
  const extra = [...entries.keys()].filter((k) => !english.has(k));
  const mismatched = [...entries].filter(([k, v]) => english.has(k) && placeholders(v) !== placeholders(english.get(k))).map(([k]) => k);
  const review = data._meta?.reviewStatus ?? 'unknown';

  console.log(`${file}: ${entries.size}/${english.size} keys, review status: ${review}`);
  if (missing.length) console.log(`  missing: ${missing.join(', ')}`);
  if (extra.length) console.log(`  extra: ${extra.join(', ')}`);
  if (mismatched.length) console.log(`  placeholder mismatch: ${mismatched.join(', ')}`);
  if (missing.length || mismatched.length) failed = true;
}

if (failed) {
  console.error('i18n check failed');
  process.exit(1);
}
console.log('i18n check passed');
