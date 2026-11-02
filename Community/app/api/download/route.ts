import { eq } from "drizzle-orm";
import { getDb } from "../../../db/index";
import { plugins } from "../../../db/schema";
import builtins from "../../../data/builtins.json";

export async function GET(request: Request) {
  const id = new URL(request.url).searchParams.get("id") ?? "";
  const builtin = (builtins as any[]).find(mod => mod.Id === id || mod.id === id);
  const [row] = builtin ? [] : await getDb().select({ manifest: plugins.manifest }).from(plugins).where(eq(plugins.id, id)).limit(1);
  if (!builtin && !row) return Response.json({ error: "Plugin not found." }, { status: 404 });
  return new Response(builtin ? JSON.stringify(builtin, null, 2) : row.manifest, { headers: { "Content-Type": "application/json", "Content-Disposition": `attachment; filename="${id}.json"`, "X-Content-Type-Options": "nosniff" } });
}
