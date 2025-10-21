import threading
from collections import defaultdict

class HitCounter:
    def __init__(self, with_lock=False):
        self.file_counter_map = defaultdict(int)
        self.with_lock = with_lock
        self.lock = threading.Lock()

    def hit(self, filename: str):
        if self.with_lock:
            with self.lock:
                self.file_counter_map[filename] += 1
        else:
            self.file_counter_map[filename] += 1

