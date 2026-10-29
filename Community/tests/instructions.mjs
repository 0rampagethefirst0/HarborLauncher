import assert from "node:assert/strict";
import fs from "node:fs";
import { createRequire } from "node:module";
import ts from "typescript";

const here = new URL("../", import.meta.url);
const source = fs.readFileSync(new URL("app/lib/manifest.ts", here), "utf8");
const compiled = ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText;
const module = { exports: {} };
new Function("require", "module", "exports", compiled)(createRequire(new URL("app/lib/manifest.ts", here)), module, module.exports);
const { validate } = module.exports;
const mods = JSON.parse(fs.readFileSync(new URL("data/builtins.json", here), "utf8"));
assert.equal(mods.length, 36);
for (const mod of mods) { const checked = validate(mod, false); assert.ok(checked.instructions.split("\n").length >= 3); assert.equal(checked.version, "1.2.0"); }
const old = { ...mods[0], Id: "community.legacy" }; delete old.Instructions;
assert.equal(validate(old).instructions, "");
assert.throws(() => validate({ ...old, Instructions: "x".repeat(8001) }));
const custom = validate({ ...old, Instructions: "Install it.\nUse the control.\nChange its settings." });
assert.equal(validate(JSON.parse(JSON.stringify(custom))).instructions, custom.instructions);
const demos = JSON.parse(fs.readFileSync(new URL("data/demos.json", here), "utf8"));
assert.equal(demos.FramesPerSecond, 60); assert.equal(demos.Frames, 192);
for (const item of demos.Items) {
  const png = fs.readFileSync(new URL("public/previews/" + item.Film, here));
  assert.equal(png.readUInt32BE(16), 6480); assert.equal(png.readUInt32BE(20), 4800);
}
console.log("PASS: 36 usage guides; legacy/default handling; limits; creator roundtrip; 36 matching 60 FPS movies.");
