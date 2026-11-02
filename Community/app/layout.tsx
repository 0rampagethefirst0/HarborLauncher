import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: "Harbor Mods — Make your desktop yours",
  description: "Desktop changes, themes and useful tools. Discover, create and share native Harbor plugins.",
  icons: {
    icon: "/favicon.svg",
    shortcut: "/favicon.svg",
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">{children}</body>
    </html>
  );
}
