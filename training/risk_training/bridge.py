import json
import os
import selectors
import subprocess


class Bridge:
    """One persistent rules process per environment; stdout is JSONL only."""

    def __init__(self):
        self.process = subprocess.Popen(
            ["dotnet", os.environ.get("RISK_BRIDGE", "/bridge/Risk.Training.dll")],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            text=True,
            bufsize=1,
        )
        self.selector = selectors.DefaultSelector()
        self.selector.register(self.process.stdout, selectors.EVENT_READ)
        try:
            self.schema = self._read()
            if self.schema["version"] != 2:
                raise RuntimeError("Unsupported observation schema")
        except Exception:
            self.close()
            raise

    def _read(self):
        if not self.selector.select(timeout=60):
            raise TimeoutError("The rules process did not respond within 60 seconds")
        line = self.process.stdout.readline()
        if not line:
            raise RuntimeError("The rules process exited unexpectedly")
        result = json.loads(line)
        if result.get("error"):
            raise RuntimeError(result["error"])
        return result

    def request(self, **request):
        self.process.stdin.write(json.dumps(request) + "\n")
        self.process.stdin.flush()
        return self._read()

    def close(self):
        self.selector.close()
        if self.process.poll() is None:
            self.process.stdin.close()
            try:
                self.process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait()
        self.process.stdout.close()

    def __enter__(self):
        return self

    def __exit__(self, *_):
        self.close()
