import type { Metadata } from 'next';
import { CopilotKit } from "@copilotkit/react-core";
import "./globals.css";
import "@copilotkit/react-ui/styles.css";

export const metadata: Metadata = {
  title: 'ALAN - Autonomous Learning Agent Network',
  description: 'Autonomous Learning Agent Network',
  icons: {
    icon: '/favicon.ico',
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body>
            <CopilotKit runtimeUrl="/api/copilotkit" agent="my_agent">
                {children}
            </CopilotKit>
        </body> 
    </html>
  );
}
