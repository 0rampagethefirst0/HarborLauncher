import { z } from "zod";

export const categories: Record<string, string[]> = {
  Desktop: ["Windows", "Dock", "Toolbar", "Dashboards"],
  Themes: ["Dark", "Light", "Colorful", "Minimal"],
  Tools: ["Productivity", "Monitoring", "Time", "Audio", "Utilities", "Shortcuts"],
};
const version = z.string().regex(/^\d+\.\d+(?:\.\d+){0,2}$/).refine(v=>v.split(".").every(p=>Number(p)<=2147483647), "Invalid version.");
const id = z.string().regex(/^[a-z0-9][a-z0-9._-]{1,79}$/);
const components = new Set(["clock", "world-clock", "system", "network", "storage", "uptime", "notes", "tasks", "focus", "stopwatch", "calculator", "converter", "folders", "clipboard", "layouts", "launchpad", "dashboard"]);
const tweaks = new Set(["window-topmost", "window-opacity", "window-placement", "window-memory", "dock-scroll-windows", "dock-middle-close", "dock-monitor-only", "dock-labels", "dock-wheel-volume", "toolbar-wheel-volume", "clock-seconds", "title-actions"]);
const features = new Set(["taskbar", "top-dock", "taskbar-folders", "media-controls", "dock-clock", "dock-stats", "active-title", "network-control", "volume-control", "dock-shortcuts", "desktop-icons", "file-manager", "file-preview", "file-thumbnails", "collection-tabs", "taskbar-dividers", "dock-dividers", "multi-monitor", "notification-center", "quick-settings"]);
const actions = new Set(["apps", "files", "mods", "notifications", "quick", "appearance", "profiles", "desktop", "columns", "grid", "stack", "undo", "launch", "copy", "mute", "volume-up", "volume-down", "previous", "play-pause", "next"]);
// Normalize property casing from both WPF exports and hand-written manifests.
export function keys(value: unknown): any {
  if (Array.isArray(value)) return value.map(keys);
  if (value && typeof value === "object") return Object.fromEntries(Object.entries(value).filter(([k]) => k !== "__proto__").map(([k,v]) => [k[0].toLowerCase() + k.slice(1), ["values"].includes(k.toLowerCase()) ? v : keys(v)]));
  return value;
}
const option = z.object({ key: z.string().regex(/^[a-zA-Z][a-zA-Z0-9._-]{0,63}$/), label: z.string().max(300), type: z.enum(["text", "multiline", "number", "color", "choice"]).default("text"), default: z.string().max(10000).default(""), choices: z.array(z.string().max(1000)).max(300).default([]) });
const schema = z.object({
  format: z.number().int().min(1).max(3).default(1), id, name: z.string().trim().min(1).max(120), author: z.string().trim().min(1).max(120), version: version.default("1.0.0"),
  category: z.string().max(100).default("Tools"), mainCategory: z.enum(["Desktop", "Themes", "Tools"]), subcategory: z.string().max(100), description: z.string().trim().min(8).max(8000), instructions: z.string().max(8000).default(""),
  kind: z.enum(["widget", "theme", "tweak", "feature"]), component: z.string().default(""), minimumHarborVersion: version.default("0.5.0"),
  tags: z.array(z.string().trim().min(1).max(60)).max(30).default([]), options: z.array(option).max(100).default([]), values: z.record(z.string().max(10000)).default({}),
  dependencies: z.array(z.object({ id, minimumVersion: version.default("0.1.0") })).max(30).default([]), conflicts: z.array(id).max(100).default([]),
  previewImage: z.string().max(700000).default(""), previewAccent: z.string().regex(/^(#[0-9a-fA-F]{6})?$/).default(""), layout: z.unknown().optional(),
});
export type Manifest = z.infer<typeof schema>;
export function validate(input: unknown, community = true): Manifest {
  const mod = schema.parse(keys(input));
  if (community && mod.id.startsWith("harbor.")) throw new Error("Built-in Harbor IDs are reserved. Use Create a plugin to make your own copy first.");
  if (!categories[mod.mainCategory].includes(mod.subcategory)) throw new Error("Choose a subcategory belonging to your main category.");
  if (!(mod.kind === "theme" || mod.kind === "widget" && components.has(mod.component) || mod.kind === "tweak" && mod.format === 3 && tweaks.has(mod.component) || mod.kind === "feature" && mod.format === 3 && features.has(mod.component))) throw new Error("Unsupported Harbor capability.");
  if (new Set(mod.options.map(o => o.key)).size !== mod.options.length || Object.keys(mod.values).length > 200) throw new Error("Invalid plugin options.");
  mod.tags = [...new Set(mod.tags.map(t => t.toLowerCase()))];
  if (mod.previewImage) {
    if (!/^data:image\/(png|jpeg);base64,[A-Za-z0-9+/]+=*$/.test(mod.previewImage)) throw new Error("Banner must be an embedded PNG or JPEG.");
    const bytes = Uint8Array.from(atob(mod.previewImage.split(",")[1]), c => c.charCodeAt(0));
    const png = bytes.length >= 24 && bytes[0] === 137 && bytes[1] === 80 && bytes[2] === 78 && bytes[3] === 71;
    const jpg = bytes.length >= 4 && bytes[0] === 255 && bytes[1] === 216 && bytes.at(-2) === 255 && bytes.at(-1) === 217;
    if (bytes.length > 500000 || !(png || jpg)) throw new Error("Banner must be a PNG or JPEG under 500 KB.");
    if (png) { const view = new DataView(bytes.buffer); if (view.getUint32(16) > 4096 || view.getUint32(20) > 4096) throw new Error("Banner dimensions must be at most 4096 pixels."); }
  }
  if (mod.component === "dashboard") {
    let count = 0; const ids = new Set<string>();
    function visit(n: any, depth: number) {
      if (!n || ++count > 128 || depth > 10 || !["column","row","wrap","text","progress","button","input","check","divider","component"].includes(n.type ?? "column")) throw new Error("Invalid widget layout.");
      if (n.type === "button" && !actions.has(n.action ?? "apps")) throw new Error("Unsupported widget action.");
      if (n.type === "component" && (!components.has(n.component) || n.component === "dashboard")) throw new Error("Unsupported embedded widget.");
      if (["input","check","component"].includes(n.type)) { if (!/^[a-zA-Z][a-zA-Z0-9_-]{0,39}$/.test(n.key ?? "") || ids.has(n.key)) throw new Error("Interactive widget keys must be unique."); ids.add(n.key); }
      for (const [key,min,max] of [["width",0,650],["height",0,800],["gap",0,80],["fontSize",8,120],["maximum",Number.MIN_VALUE,Number.MAX_VALUE]] as [string,number,number][]) if (n[key] !== undefined && (typeof n[key] !== "number" || !Number.isFinite(n[key]) || n[key] < min || n[key] > max)) throw new Error("Invalid widget size.");
      for (const key of ["text","key","color","background","source","argument"]) if (n[key] !== undefined && (typeof n[key] !== "string" || n[key].length > (key==="source" ? 100 : 10000))) throw new Error("Invalid widget text.");
      if (n.values !== undefined && (!n.values || typeof n.values !== "object" || Array.isArray(n.values) || Object.keys(n.values).length>100 || Object.values(n.values).some(v=>typeof v!=="string"||v.length>10000))) throw new Error("Invalid widget values.");
      if (n.children !== undefined && !Array.isArray(n.children)) throw new Error("Invalid widget children.");
      if (n.children?.length && !["column","row","wrap"].includes(n.type??"column")) throw new Error("Only containers can have children.");
      for (const child of n.children ?? []) visit(child, depth + 1);
    }
    if (mod.format < 2) throw new Error("Composed widgets need format 2."); visit(mod.layout, 1);
  }
  return mod;
}
