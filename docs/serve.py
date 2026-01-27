#!/usr/bin/env python3
"""
Simple HTTP server for Blazor WASM - LOCALHOST ONLY for security
Use this on Tails Linux for offline operation
"""
import http.server
import socketserver

PORT = 8000
HOST = '127.0.0.1'  # Localhost only - NOT accessible from network

class MyHTTPRequestHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        # Add headers for WASM
        self.send_header('Cross-Origin-Embedder-Policy', 'require-corp')
        self.send_header('Cross-Origin-Opener-Policy', 'same-origin')
        super().end_headers()

with socketserver.TCPServer((HOST, PORT), MyHTTPRequestHandler) as httpd:
    print(f"[OK] Serving on http://{HOST}:{PORT}")
    print(f"[!] LOCALHOST ONLY - Not accessible from network (secure)")
    print(f"Open Tor Browser and navigate to: http://127.0.0.1:{PORT}")
    print("Press Ctrl+C to stop")
    httpd.serve_forever()
