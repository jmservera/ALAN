import React, { useEffect, useState } from 'react';
import { CopilotKit } from '@copilotkit/react-core';
import { CopilotPopup } from '@copilotkit/react-ui';
import '@copilotkit/react-ui/styles.css';

interface AlanCopilotChatProps {
  chatApiBaseUrl: string;
  agentName?: string;
}

export const AlanCopilotChat: React.FC<AlanCopilotChatProps> = ({ chatApiBaseUrl, agentName = 'alan-agent' }) => {
  // CopilotKit expects an AG-UI compatible endpoint
  const agentEndpoint = `${chatApiBaseUrl}/agui`;

  const [runtimeOnline, setRuntimeOnline] = useState<'unknown' | 'online' | 'offline'>('unknown');
  const [errorMessage, setErrorMessage] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;

    const checkRuntime = async () => {
      try {
        const res = await fetch(`${agentEndpoint}/info`, { method: 'GET' });
        if (!cancelled && res.ok) {
          setRuntimeOnline('online');
          setErrorMessage(null);
          return;
        }
        throw new Error(`Status ${res.status}`);
      } catch (err) {
        if (!cancelled) {
          setRuntimeOnline('offline');
          setErrorMessage(`Copilot runtime not reachable at ${agentEndpoint}/info (${(err as Error).message}).`);
        }
      }
    };

    checkRuntime();

    return () => {
      cancelled = true;
    };
  }, [agentEndpoint]);

  if (runtimeOnline === 'offline') {
    return (
      <div style={{ padding: '1rem', border: '1px solid #f5c2c7', background: '#f8d7da', color: '#842029', borderRadius: '4px' }}>
        <strong>Copilot unavailable.</strong>
        <div style={{ marginTop: '0.5rem' }}>{errorMessage}</div>
        <div style={{ marginTop: '0.5rem' }}>
          Ensure the Chat API is running and accessible. Default dev URL: <code>http://localhost:5041/api/agui</code>.
        </div>
      </div>
    );
  }

  if (runtimeOnline === 'unknown') {
    return <div>Connecting to Copilot runtime…</div>;
  }

  return (
    <CopilotKit
      runtimeUrl={agentEndpoint}
      agent={agentName}
      showDevConsole={process.env.NODE_ENV !== 'production'}
    >
      <div style={{ height: '600px', display: 'flex', flexDirection: 'column' }}>
        <CopilotPopup
          instructions="You are ALAN (Autonomous Learning Agent Network), an AI assistant with access to accumulated knowledge and memories. Help users by leveraging your context and learned experiences."
          labels={{
            title: "Chat with ALAN",
            initial: "Hi! I'm ALAN. How can I assist you today?",
          }}
          defaultOpen={true}
        />
      </div>
    </CopilotKit>
  );
};
