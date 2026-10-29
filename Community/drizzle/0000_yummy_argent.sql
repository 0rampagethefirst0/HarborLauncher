CREATE TABLE `plugins` (
	`id` text PRIMARY KEY NOT NULL,
	`owner` text NOT NULL,
	`name` text NOT NULL,
	`category` text NOT NULL,
	`subcategory` text NOT NULL,
	`manifest` text NOT NULL,
	`updated` integer NOT NULL,
	`created` integer NOT NULL
);
--> statement-breakpoint
CREATE INDEX `plugins_updated` ON `plugins` (`updated`);--> statement-breakpoint
CREATE INDEX `plugins_owner` ON `plugins` (`owner`);