#!/bin/bash

# Example script to interact with ALAN API
# Make sure the API is running before executing this script

API_URL="http://localhost:5000"

echo "=== ALAN Agent Control Script ==="
echo ""

# Function to check agent status
check_status() {
    echo "Checking agent status..."
    curl -s "$API_URL/api/agent/status" | jq '.'
    echo ""
}

# Function to send human input
send_input() {
    local input="$1"
    echo "Sending input: $input"
    curl -s -X POST "$API_URL/api/agent/input" \
        -H "Content-Type: application/json" \
        -d "{\"input\": \"$input\"}" | jq '.'
    echo ""
}

# Function to view memories
view_memories() {
    echo "Recent short-term memories:"
    curl -s "$API_URL/api/memory/short-term?limit=5" | jq '.[] | {timestamp: .timestamp, content: .content}'
    echo ""
}

# Function to view learnings
view_learnings() {
    echo "Recent learnings:"
    curl -s "$API_URL/api/memory/learnings?limit=3" | jq '.[] | {timestamp: .timestamp, content: .content}'
    echo ""
}

# Function to pause agent
pause_agent() {
    echo "Pausing agent..."
    curl -s -X POST "$API_URL/api/agent/pause" | jq '.'
    echo ""
}

# Function to resume agent
resume_agent() {
    echo "Resuming agent..."
    curl -s -X POST "$API_URL/api/agent/resume" | jq '.'
    echo ""
}

# Main menu
case "${1:-status}" in
    status)
        check_status
        ;;
    input)
        if [ -z "$2" ]; then
            echo "Usage: $0 input \"your message\""
            exit 1
        fi
        send_input "$2"
        ;;
    memories)
        view_memories
        ;;
    learnings)
        view_learnings
        ;;
    pause)
        pause_agent
        ;;
    resume)
        resume_agent
        ;;
    demo)
        echo "Running demo sequence..."
        echo ""
        check_status
        sleep 2
        send_input "Please focus on system monitoring"
        sleep 2
        view_memories
        sleep 2
        pause_agent
        sleep 2
        check_status
        sleep 2
        resume_agent
        sleep 2
        check_status
        ;;
    *)
        echo "Usage: $0 {status|input|memories|learnings|pause|resume|demo}"
        echo ""
        echo "Examples:"
        echo "  $0 status"
        echo "  $0 input \"Focus on code analysis\""
        echo "  $0 memories"
        echo "  $0 learnings"
        echo "  $0 demo"
        exit 1
        ;;
esac
