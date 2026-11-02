import { getChatGPTUser, chatGPTSignInPath } from "../chatgpt-auth";
import UploadStudio from "./studio";
export const dynamic = "force-dynamic";
export default async function UploadPage() {
  const user = await getChatGPTUser();
  return <UploadStudio signedIn={!!user} signIn={chatGPTSignInPath("/upload")} />;
}
