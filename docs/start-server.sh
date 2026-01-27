#!/bin/bash
# Simple script to start Python web server for SLIP-39 app on Tails Linux
# This serves the app on localhost only (127.0.0.1) for security
# Port 9876 is used to avoid conflicts with other services

PORT=9876
HOST="127.0.0.1"

echo "======================================================================"
echo "  SLIP-39 Wallet Backup - Local Web Server"
echo "======================================================================"
echo ""
echo "Starting Python web server..."
echo "  Host: $HOST (localhost only - NOT accessible from network)"
echo "  Port: $PORT"
echo ""
echo "Once started, open Tor Browser and navigate to:"
echo "  http://127.0.0.1:$PORT"
echo ""
echo "To stop the server: Press Ctrl+C"
echo ""
echo "======================================================================"
echo ""

# Start Python 3 HTTP server bound to localhost only
python3 -m http.server $PORT --bind $HOST
