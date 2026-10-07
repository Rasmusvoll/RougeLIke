"""Send a Python file to the Blender Lab MCP add-on (127.0.0.1:9876) and print the result.

Usage: python blender_send.py script.py
The script runs inside Blender; it should set `result = {...}`.
"""
import json, socket, sys

code = open(sys.argv[1], encoding="utf-8").read()
s = socket.create_connection(("127.0.0.1", 9876), timeout=120)
s.sendall((json.dumps({"type": "execute", "code": code, "strict_json": False}) + "\0").encode())
buf = b""
while not buf.endswith(b"\0"):
    chunk = s.recv(65536)
    if not chunk:
        break
    buf += chunk
print(json.dumps(json.loads(buf.rstrip(b"\0").decode()), indent=1))
