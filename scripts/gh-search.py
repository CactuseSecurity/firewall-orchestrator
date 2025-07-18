import requests
import os

token_file = os.path.expanduser("~/.github-token")
owner = "cactusesecurity"
repo = "firewall-orchestrator"
searchstring = 'fwreznet'

with open(token_file, 'r') as f:
    token = f.read().strip()

headers = {
    'Authorization': f'Bearer {token}'
}

issues = []
page = 1
per_page = 100

while True:
    url = f'https://api.github.com/repos/{owner}/{repo}/issues?state=all&per_page={per_page}&page={page}'
    r = requests.get(url, headers=headers)
    data = r.json()

    if not data:
        break

    for issue in data:
        if 'pull_request' in issue:
            continue # Skip pull requests, only keep actual issues
        if type(issue) is str:
            continue  # Skip any string responses (e.g., error messages)
        if searchstring.lower() in issue['title'].lower():
            issue_state = issue['state'].ljust(6)
            issues.append(f"#{issue['number']}: [state: {issue_state}] {issue['title']} ({issue['html_url']})")

    if len(data) < per_page:
        break

    page += 1

issues_sorted = sorted(set(issues))
for issue in issues_sorted:
    print(issue)
