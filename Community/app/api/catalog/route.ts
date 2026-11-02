import { desc } from "drizzle-orm";
import { getDb } from "../../../db/index";
import { plugins } from "../../../db/schema";

export async function GET(request: Request) {
  const page = Number(new URL(request.url).searchParams.get("page") ?? 0);
  if (!Number.isInteger(page) || page < 0 || page > 833) return Response.json({ error: "Invalid page." }, { status: 400 });
  const rows = await getDb().select({ manifest: plugins.manifest }).from(plugins).orderBy(desc(plugins.updated), plugins.id).limit(12).offset(page * 12);
  return Response.json(rows.map(row => JSON.parse(row.manifest)), { headers: { "Cache-Control": "public, max-age=30" } });
}
