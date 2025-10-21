import threading
import time
from collections import defaultdict

class HitCounter:
    def __init__(self, with_lock=False, sleeping: float = 0):
        self.sleeping = sleeping
        self.file_counter_map = defaultdict(int)
        self.with_lock = with_lock
        self.lock = threading.Lock()

    def hit(self, filename: str):
        if self.with_lock:
            with self.lock:
                self.increment_hit(filename)
        else:
            self.increment_hit(filename)

    def increment_hit(self, filename: str):
        current = self.hit_count(filename)
        time.sleep(self.sleeping)
        self.file_counter_map[filename] = current + 1

    def hit_count(self, filename: str):
        return self.file_counter_map[filename]
