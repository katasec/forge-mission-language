"""
Talk to a forge-served mission from Python.

Start the server first:
    forge serve missions/concepts/debate/agent.yaml

Then run this script:
    pip install requests
    python examples/python/client.py
"""

import requests

question = "Can large language models truly reason, or are they sophisticated pattern matchers?"

response = requests.post(
    "http://localhost:8080/v1/chat/completions",
    json={
        "model": "debate",
        "messages": [{"role": "user", "content": question}],
    },
)
response.raise_for_status()
print(response.json()["choices"][0]["message"]["content"])
