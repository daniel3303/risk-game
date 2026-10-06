"""Regenerate shared display geometry from the licensed vector cartography."""
import subprocess
subprocess.run(["npm", "--prefix", "client", "run", "generate:map"], check=True)
