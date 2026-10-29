import { sqliteTable, text, integer, index } from "drizzle-orm/sqlite-core";

export const plugins = sqliteTable("plugins", {
  id: text("id").primaryKey(),
  owner: text("owner").notNull(),
  name: text("name").notNull(),
  category: text("category").notNull(),
  subcategory: text("subcategory").notNull(),
  manifest: text("manifest").notNull(),
  updated: integer("updated").notNull(),
  created: integer("created").notNull(),
}, (table) => [index("plugins_updated").on(table.updated), index("plugins_owner").on(table.owner)]);
