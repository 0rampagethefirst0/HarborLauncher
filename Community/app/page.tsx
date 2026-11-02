import { getChatGPTUser, chatGPTSignInPath, chatGPTSignOutPath } from "./chatgpt-auth";
import Hub from "./hub";
export const dynamic = "force-dynamic";
export default async function Home() {
  const user = await getChatGPTUser();
  return <Hub signedIn={!!user} signIn={chatGPTSignInPath("/")} signOut={chatGPTSignOutPath("/")} />;
}
