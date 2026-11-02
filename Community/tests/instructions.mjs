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
assert.ok(mods.length >= 57);
assert.equal(new Set(mods.map(m => m.Id)).size, mods.length);
assert.ok(mods.some(m => m.Id === "harbor.tokyo-dragon"));
for (const mod of mods) {
  const checked = validate(mod, false);
  assert.ok(checked.instructions.split("\n").length >= 3);
  assert.equal(checked.version, "1.8.2");
  assert.equal(checked.minimumHarborVersion, "0.14.2");
  if (checked.kind === "theme") for (const key of ["background", "panel", "accent", "text", "muted", "raised", "border", "wallpaper", "logo", "fire"]) assert.ok(checked.options.some(o => o.key === key && o.type === "color"));
  if (checked.kind === "widget") {
    assert.equal(checked.minimumHarborVersion, "0.14.2");
    const access = checked.options.find(o => o.key === "access");
    assert.equal(access.default, "Edge tab");
    assert.deepEqual(access.choices, ["Edge tab", "Floating control", "Dock button", "Desktop panel"]);
    assert.ok(checked.instructions.includes("Click it to expand"));
    assert.ok(checked.options.find(o => o.key === "snap").choices.includes("Center box: upper left"));
    assert.ok(checked.options.some(o => o.key === "always-on"));
    const visibility = checked.options.find(o => o.key === "visibility");
    assert.equal(visibility.default, "Apps screen only");
    assert.deepEqual(visibility.choices, ["Always available", "Apps screen only", "With chosen apps"]);
    assert.equal(checked.options.find(o => o.key === "visible-apps").type, "multiline");
    assert.ok(checked.instructions.includes("With chosen apps"));
  }
  if (checked.kind === "feature") {
    assert.equal(checked.format, 3);
    assert.equal(checked.minimumHarborVersion, "0.14.2");
    assert.ok(checked.instructions.includes("start disabled"));
    for (const dependency of checked.dependencies) assert.ok(mods.some(m => m.Id === dependency.id));
  }
}
const features = mods.map(m => validate(m, false)).filter(m => m.kind === "feature");
assert.equal(features.length, 20);
for (const name of ["Bulky", "Minimalists Dock", "Now Playing", "Harbor Home"]) assert.ok(features.some(m => m.name === name));
assert.throws(() => validate({ ...mods[0], Id: "community.unsupported", Kind: "feature", Format: 3, Component: "unknown" }));
assert.throws(() => validate({ ...mods[0], Id: "community.oldfeature", Kind: "feature", Format: 2, Component: "taskbar" }));
assert.equal(validate({ ...mods[0], Id: "community.optionalbar", Kind: "feature", Format: 3, Component: "taskbar" }).kind, "feature");
const old = { ...mods[0], Id: "community.legacy" }; delete old.Instructions;
assert.equal(validate(old).instructions, "");
assert.throws(() => validate({ ...old, Instructions: "x".repeat(8001) }));
const custom = validate({ ...old, Instructions: "Install it.\nUse the control.\nChange its settings." });
assert.equal(validate(JSON.parse(JSON.stringify(custom))).instructions, custom.instructions);
const demos = JSON.parse(fs.readFileSync(new URL("data/demos.json", here), "utf8"));
assert.equal(demos.FramesPerSecond, 90); assert.equal(demos.Frames, 288);
assert.equal(demos.Items.length, mods.length);
assert.equal(new Set(demos.Items.map(item => item.Id)).size, mods.length);
for (const item of demos.Items) {
  assert.ok(mods.some(m => m.Id === item.Id));
  const png = fs.readFileSync(new URL("public/previews/" + item.Film, here));
  assert.equal(png.readUInt32BE(16), 6480); assert.equal(png.readUInt32BE(20), 7200);
}
console.log("PASS: " + mods.length + " usage guides; all theme color roles; tools with Apps-only defaults and chosen-app rules; 20 optional shell features and dependencies; capability validation; legacy/default handling; creator roundtrip; " + demos.Items.length + " matching 90 FPS movies.");
