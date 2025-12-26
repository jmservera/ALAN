'use client';

import Dashboard from '@/components/Dashboard';


import { CopilotSidebar } from '@copilotkit/react-ui';

export default function Home() {

  return (
    <main>
      <div className="app-container">
        <CopilotSidebar
          defaultOpen={false}          
          clickOutsideToClose={true}
          labels={{
            title: "ALAN Assistant",
            initial: "Ask me anything about the agent's state, thoughts, or actions.",
          }}
        >
          <Dashboard />
        </CopilotSidebar>
      </div>
    </main>
  );
}
