import threading
import time
from concurrent.futures import ThreadPoolExecutor
from client import HttpClient

client = HttpClient("localhost", 8080)

print_lock = threading.Lock()

def make_request(i):
    local_client = HttpClient("localhost", 8080)  # new client per thread
    print(f"Request {i+1}")

    status, response = local_client.request("", "GET")

    print(response.decode())


# set sleeping in server 0.5 sec, withLock = false
def some_requests():

    with ThreadPoolExecutor(max_workers=10) as executor:
        for i in range(10):
            executor.submit(make_request, i)

start = time.time_ns()
some_requests()
end = time.time_ns()
print(f"Finished in {(end-start)/10**9}s")

