import requests
import time
import threading
from concurrent.futures import ThreadPoolExecutor

host = "localhost"
port = 8080
url = f"http://{host}:{port}/"

def make_request(results):
    try:
        r = requests.get(url)
        status = r.status_code
        if status == 200:
            results["200"] += 1
        elif status == 429:
            results["429"] += 1
        else:
            results["other"].append(status)
    except Exception as e:
        results["error"].append(str(e))

def run_test(requests_per_second):
    period = 1.0 / requests_per_second
    print(f"Running test: {requests_per_second} requests/sec for 5 seconds")

    for second in range(5):
        results = {"200": 0, "429": 0, "other": [], "error": []}
        start = time.time()

        with ThreadPoolExecutor(max_workers=requests_per_second) as executor:
            futures = [executor.submit(make_request, results) for _ in range(requests_per_second)]
            for f in futures:
                f.result()

        elapsed = time.time() - start
        print(f"Second {second+1}: 200={results['200']} | 429={results['429']} | other={results['other']} | errors={len(results['error'])}")

        sleep_time = 1 - elapsed
        if sleep_time > 0:
            time.sleep(sleep_time)

if __name__ == "__main__":
    run_test(4)
