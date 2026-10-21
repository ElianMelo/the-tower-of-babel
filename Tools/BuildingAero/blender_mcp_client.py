"""Run an authoring file through the installed Blender MCP, without touching other scenes."""
import argparse
import asyncio
import json
import os
from pathlib import Path

from mcp import ClientSession, StdioServerParameters
from mcp.client.stdio import stdio_client


async def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("file", type=Path)
    parser.add_argument("--timeout", type=float, default=180)
    args = parser.parse_args()
    params = StdioServerParameters(
        command=r"C:\Users\Elian\AppData\Local\BlenderMCP\bin\uvx.exe",
        args=["--python", r"C:\Users\Elian\.cache\codex-runtimes\codex-primary-runtime\dependencies\python\python.exe", "mcp-for-blender==2.2.2"],
        env={**os.environ, "BLENDER_HOST": "127.0.0.1", "BLENDER_PORT": "9876",
             "BLENDER_MCP_DISABLE_TELEMETRY": "1", "PYTHONIOENCODING": "utf-8"},
    )
    async with stdio_client(params) as (read, write):
        async with ClientSession(read, write) as session:
            await session.initialize()
            result = await asyncio.wait_for(session.call_tool("execute_blender_code", {
                "code": args.file.read_text(encoding="utf-8-sig"),
                "user_prompt": "Create the authorized white-and-blue Frutiger Aero building kit for the-tower-of-babel; preserve the user's original Blender scene.",
            }), timeout=args.timeout)
            failed = bool(result.isError)
            for part in result.content:
                if part.type == "text":
                    print(part.text)
                    failed |= part.text.startswith('Error executing code:')
            if failed:
                raise SystemExit(1)


asyncio.run(main())
