## Laboratory Work #4: Leader-Follower key-value storage system


```bash
$env:WRITE_QUORUM = "2"
echo $env:WRITE_QUORUM
```

```bash
docker compose down
docker compose up
```

```bash
curl -Method POST "http://localhost:8080/put" -Headers @{ "Content-Type"="application/json" } -Body '{"key": "key", "value": "value"}'
```

```bash
curl.exe http://localhost:8080/get?key=key
```

<img src="client/latency_plot.png" width="80%">
