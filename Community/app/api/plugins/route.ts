import { eq, and, gte, sql } from "drizzle-orm";
import { getChatGPTUser } from "../../chatgpt-auth";
import { getDb } from "../../../db/index";
import { plugins } from "../../../db/schema";
import { validate } from "../../lib/manifest";

function sameOrigin(request: Request) { return request.headers.get("origin") === new URL(request.url).origin; }
export async function POST(request: Request) {
  const user = await getChatGPTUser();
  if (!user) return Response.json({ error: "Sign in before uploading." }, { status: 401 });
  if (!sameOrigin(request)) return Response.json({ error: "Use the community upload page." }, { status: 403 });
  try {
    if (Number(request.headers.get("content-length") ?? 0) > 1000000) return Response.json({ error: "Package exceeds 1 MB." }, { status: 413 });
    const reader = request.body?.getReader(); if (!reader) throw new Error("Empty package.");
    const chunks: Uint8Array[] = []; let length = 0;
    while (true) { const {done,value} = await reader.read(); if (done) break; length += value.byteLength; if (length > 1000000) { await reader.cancel(); throw new Error("Package exceeds 1 MB."); } chunks.push(value); }
    const bytes = new Uint8Array(length); let offset=0; for (const chunk of chunks) { bytes.set(chunk,offset); offset+=chunk.byteLength; }
    const text = new TextDecoder().decode(bytes);
    const mod = validate(JSON.parse(text)); const db = getDb();
    const [existing] = await db.select().from(plugins).where(eq(plugins.id, mod.id)).limit(1);
    if (existing && existing.owner !== user.userId) return Response.json({ error: "This plugin ID belongs to another creator." }, { status: 409 });
    if (!existing) {
      const [quota] = await db.select({ count: sql<number>`count(*)` }).from(plugins).where(and(eq(plugins.owner, user.userId), gte(plugins.created, Date.now() - 86400000)));
      if (quota.count >= 20) return Response.json({ error: "Daily limit reached: 20 new plugins per creator." }, { status: 429 });
    }
    const now = Date.now(); const values = { name: mod.name, category: mod.mainCategory, subcategory: mod.subcategory, manifest: JSON.stringify(mod), updated: now };
    const changed = await db.insert(plugins).values({ id: mod.id, owner: user.userId, created: now, ...values }).onConflictDoUpdate({ target: plugins.id, set: values, setWhere: eq(plugins.owner, user.userId) }).returning({id:plugins.id});
    if (!changed.length) return Response.json({error:"This plugin ID belongs to another creator."},{status:409});
    return Response.json({ id: mod.id, name: mod.name, published: true });
  } catch (error: any) { return Response.json({ error: error?.issues?.[0]?.message ?? error.message ?? "Upload failed." }, { status: 400 }); }
}
export async function GET() {
  const user = await getChatGPTUser(); if (!user) return Response.json({ error: "Sign in first." }, { status: 401 });
  const rows = await getDb().select({ manifest: plugins.manifest }).from(plugins).where(eq(plugins.owner, user.userId)).orderBy(plugins.name).limit(200);
  return Response.json(rows.map(row => JSON.parse(row.manifest)), { headers: { "Cache-Control": "no-store" } });
}
export async function DELETE(request: Request) {
  const user = await getChatGPTUser(); if (!user) return Response.json({ error: "Sign in first." }, { status: 401 });
  if (!sameOrigin(request)) return Response.json({ error: "Use your creator page." }, { status: 403 });
  const id = new URL(request.url).searchParams.get("id"); if (!id) return Response.json({ error: "Missing plugin ID." }, { status: 400 });
  await getDb().delete(plugins).where(and(eq(plugins.id, id), eq(plugins.owner, user.userId))); return Response.json({ removed: true });
}
