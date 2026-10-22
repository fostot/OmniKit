"""Opt-in native Codex isolation proof against a synthetic loopback provider.

Run after building CliChatTests. No real inference, login, credential-file access,
request dumps, or account output. Retains only empty workspaces and a synthetic
persona under the project's ignored .local directory.
"""
import http.server
import json
import os
from pathlib import Path
import subprocess
import threading
import uuid

PROJECT = Path(__file__).resolve().parents[2]
APPHOST = Path(__file__).parent / "bin/Debug/net10.0-windows/CliChatTests.exe"
NATIVE_ROOT = Path(os.environ["LOCALAPPDATA"]) / "OpenAI/Codex/bin"
NATIVE = max(NATIVE_ROOT.glob("*/codex.exe"), key=lambda path: path.stat().st_mtime)
EXPECTED_VERSION = "codex-cli 0.163.0-alpha.11.1"
ALLOWED_TOOLS = {"list_mcp_resources", "list_mcp_resource_templates", "read_mcp_resource", "request_user_input"}
ALLOWED_ENV = {name.upper() for name in ["SystemRoot", "WINDIR", "PATH", "PATHEXT", "USERPROFILE", "HOME",
    "LOCALAPPDATA", "APPDATA", "ProgramData", "ProgramFiles", "ProgramFiles(x86)", "TEMP", "TMP", "CODEX_HOME"]}
ENV = {key: value for key, value in os.environ.items() if key.upper() in ALLOWED_ENV}
ENV["NO_COLOR"] = "1"
CREATE_NO_WINDOW = 0x08000000


def text_parts(value):
    if isinstance(value, str):
        return value
    if isinstance(value, list):
        return "\n".join(text_parts(part) for part in value)
    if isinstance(value, dict) and value.get("type") in ("text", "input_text", "output_text"):
        return value.get("text", "")
    return json.dumps(value)


def probe(replace_model_instructions):
    captured = []
    calls = [
        {"id": "fc_resources", "call_id": "call_resources", "type": "function_call", "name": "list_mcp_resources", "arguments": "{}", "status": "completed"},
        {"id": "fc_templates", "call_id": "call_templates", "type": "function_call", "name": "list_mcp_resource_templates", "arguments": "{}", "status": "completed"},
        {"id": "fc_read", "call_id": "call_read", "type": "function_call", "name": "read_mcp_resource", "arguments": json.dumps({"server": "omnikit_missing_fixture_server", "uri": "omnikit-fixture://unavailable"}), "status": "completed"},
    ]

    class Handler(http.server.BaseHTTPRequestHandler):
        protocol_version = "HTTP/1.1"

        def log_message(self, *_):
            pass

        def do_GET(self):
            self.send_response(404)
            self.send_header("Content-Length", "0")
            self.end_headers()

        def do_POST(self):
            length = int(self.headers.get("Content-Length", "0"))
            if length > 2_000_000:
                raise RuntimeError("Synthetic request exceeded bound")
            request = json.loads(self.rfile.read(length))
            captured.append(request)
            first = len(captured) == 1
            output = calls if first else [{"id": "msg_final", "type": "message", "role": "assistant", "status": "completed",
                "content": [{"type": "output_text", "text": "OMNIKIT_SYNTHETIC_OK", "annotations": []}]}]
            response = {"id": "resp_fixture_" + str(len(captured)), "status": "completed", "output": output,
                "usage": {"input_tokens": 1, "output_tokens": 1, "total_tokens": 2}}
            events = [{"type": "response.created", "response": {**response, "status": "in_progress", "output": []}}]
            for index, item in enumerate(output):
                events.append({"type": "response.output_item.added", "output_index": index, "item": item})
                events.append({"type": "response.output_item.done", "output_index": index, "item": item})
            events.append({"type": "response.completed", "response": response})
            body = "".join("event: " + event["type"] + "\ndata: " + json.dumps(event) + "\n\n" for event in events).encode()
            self.send_response(200)
            self.send_header("Content-Type", "text/event-stream")
            self.send_header("Content-Length", str(len(body)))
            self.end_headers()
            self.wfile.write(body)

    scratch = PROJECT / ".local" / ("cli-resource-probe-" + uuid.uuid4().hex)
    work = scratch / "empty-work"
    work.mkdir(parents=True)
    server = http.server.ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    threading.Thread(target=server.serve_forever, daemon=True).start()
    args = json.loads(subprocess.check_output([str(APPHOST), "--write-probe-args", str(work)], text=True,
        encoding="utf-8", env=ENV, creationflags=CREATE_NO_WINDOW))
    settings = {
        "model_provider": "omnikit_probe",
        "model_providers.omnikit_probe.name": "Synthetic local probe",
        "model_providers.omnikit_probe.base_url": "http://127.0.0.1:" + str(server.server_address[1]) + "/v1",
        "model_providers.omnikit_probe.wire_api": "responses",
        "model_providers.omnikit_probe.requires_openai_auth": False,
    }
    if replace_model_instructions:
        persona = scratch / "synthetic-persona.txt"
        persona.write_text("You are a synthetic transport-test companion. Use only the requested synthetic resource checks.", encoding="utf-8")
        settings["model_instructions_file"] = str(persona)
    for key, value in settings.items():
        args[-1:-1] = ["-c", key + "=" + json.dumps(value)]
    process = subprocess.Popen([str(NATIVE), *args], stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.PIPE,
        text=True, encoding="utf-8", env=ENV, creationflags=CREATE_NO_WINDOW)
    try:
        stdout, _stderr = process.communicate("Run the three requested synthetic resource checks, then reply OMNIKIT_SYNTHETIC_OK.", timeout=30)
    except subprocess.TimeoutExpired:
        subprocess.run(["taskkill.exe", "/PID", str(process.pid), "/T", "/F"], stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL, creationflags=CREATE_NO_WINDOW, check=False)
        process.communicate()
        raise RuntimeError("Synthetic CLI probe timed out; no provider output displayed")
    finally:
        server.shutdown()
        server.server_close()
    assert len(captured) >= 2, "CLI did not return synthetic resource results"
    names = {tool.get("name") for tool in captured[0].get("tools", [])}
    assert names == ALLOWED_TOOLS, "CLI exposed an unapproved tool manifest"
    returned = {item.get("call_id"): text_parts(item.get("output"))
        for item in captured[-1].get("input", []) if item.get("type") == "function_call_output"}
    resources = returned.get("call_resources", "")
    templates = returned.get("call_templates", "")
    read = returned.get("call_read", "").lower()
    assert '"resources": []' in resources or '"resources":[]' in resources, "Resource list was not confirmed empty"
    assert '"resourceTemplates": []' in templates or '"resourceTemplates":[]' in templates, "Resource template list was not confirmed empty"
    assert "omnikit_missing_fixture_server" in read and ("unknown" in read or "not found" in read), "Resource read was not refused for the nonexistent server"
    assert process.returncode == 0 and "OMNIKIT_SYNTHETIC_OK" in stdout, "Synthetic final response failed"
    print(json.dumps({"mode": "replacement-instructions" if replace_model_instructions else "production-arguments",
        "requests": len(captured), "only_four_allowed_helpers": True, "resources_empty": True,
        "resource_templates_empty": True, "unknown_resource_server_rejected": True,
        "global_guidance_included": "Global Codex Working Guidance" in json.dumps(captured[0].get("input", []))}))


version = subprocess.check_output([str(NATIVE), "--version"], text=True, encoding="utf-8", env=ENV,
    creationflags=CREATE_NO_WINDOW).strip()
assert version == EXPECTED_VERSION, "Native Codex version has not been approved for this probe"
probe(False)
probe(True)
