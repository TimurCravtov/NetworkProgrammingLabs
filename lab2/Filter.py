import threading
import time


class IpRequestFilter:
    def __init__(self, requests_per_second: int):
        self.requests_per_second = requests_per_second
        self.current_second = int(time.time().__floor__())
        self.current_second_map = {}
        self.request_update_lock = threading.Lock()

    def process(self, address: str) -> bool:

        now = int(time.time())

        with self.request_update_lock:

            if now > self.current_second:
                self.current_second = now
                self.current_second_map = {}

            count = self.current_second_map.get(address, 0) + 1
            self.current_second_map[address] = count

            return count <= self.requests_per_second

