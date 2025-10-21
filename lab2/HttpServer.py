import base64
import random
import socket
import os
import sys
import time
from concurrent.futures import ThreadPoolExecutor
from mimetypes import guess_type
from threading import Thread
from urllib.parse import unquote

from FileHelper import file_has_one_of_extensions
from Filter import IpRequestFilter
from HitCounter import HitCounter
from HttpHelper import *


class HtmlServer:

    page404 = build_http_response(404, b"<h1>404 Not Found</h1>")
    page_method_not_allowed = build_http_response(405, b"<h1>405 Method Not Allowed<h1>")
    page_too_many_requests = build_http_response(429, b"<h1>429 Too Many Requests<h1>", headers={"Connection": "close"})

    def __init__(self, host="0.0.0.0", port=8080, served_directory=None, allowed_extensions=(".html", ".htm", ".pdf", ".png")):
        self.host = host
        self.port = port
        self.allowed_extensions = allowed_extensions
        self.served_directory = os.path.abspath(served_directory or os.getcwd())
        self.filter = IpRequestFilter(5)
        self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        self.hit_counter = HitCounter(with_lock=True, sleeping=0)

        print("Serving directory:", self.served_directory)

    def handle_request(self, conn, addr):

        if not self.filter.process(addr[0]):
            conn.sendall(self.page_too_many_requests)
            conn.shutdown(socket.SHUT_WR)
            time.sleep(0.01)

            conn.close()
            return

        time.sleep(0.5 + random.random())

        result = receive_from_http_socket(conn, type="request")

        if not result:
            conn.close()
            return

        method, path, version, headers, body = result

        if method != "GET":
            conn.sendall(self.page_method_not_allowed)
            conn.close()
            return

        # Normalize path
        rel_path = unquote(path.lstrip("/"))
        filepath = os.path.abspath(os.path.join(self.served_directory, rel_path))

        # Prevent path escape
        if not os.path.exists(filepath) or os.path.commonpath(
                [self.served_directory, filepath]) != self.served_directory:
            conn.sendall(self.page404)
            conn.close()
            return

        # hit directory listing
        if os.path.isdir(filepath):

            self.hit_counter.hit(filepath)
            body = self.generate_file_listing_html(rel_path).encode()
            page = build_http_response(200, body)
            conn.sendall(page)
            conn.close()
            return

        if not file_has_one_of_extensions(filepath, allowed_extensions=self.allowed_extensions):
            conn.sendall(self.page404)
            conn.close()
            return

        # serve file
        with open(filepath, "rb") as f:

            self.hit_counter.hit(filepath)

            body = f.read()
            content_type = get_content_type(filepath)
            response = build_http_response(200, body, headers={"Content-Type": content_type})
            conn.sendall(response)
            conn.close()

    def generate_file_listing_html(self, rel_path=""):

        filepath = os.path.abspath(os.path.join(self.served_directory, rel_path))

        abs_path = os.path.join(self.served_directory, rel_path)
        html = "<html><body>"
        html += f"<h2>Index of [{self.hit_counter.hit_count(filepath)}]/{rel_path}</h2><ul>"

        if rel_path.strip("/"):
            parent_rel = os.path.dirname(rel_path.rstrip("/"))
            html += f'<li><a href="/{parent_rel}">../</a></li>'

        for entry in sorted(os.listdir(abs_path)):
            entry_path = os.path.join(abs_path, entry)
            entry_rel_path = os.path.join(rel_path, entry)
            if os.path.isdir(entry_path):
                html += f'<li>[{self.hit_counter.hit_count(entry_path)}]<b><a href="/{entry_rel_path}/">{entry}/</a></b></li>'

        for entry in sorted(os.listdir(abs_path)):
            entry_path = os.path.join(abs_path, entry)
            entry_rel_path = os.path.join(rel_path, entry)
            if os.path.isfile(entry_path) and file_has_one_of_extensions(file_path=entry_path, allowed_extensions=self.allowed_extensions):
                html += f'<li>[{self.hit_counter.hit_count(entry_path)}] <a href="/{entry_rel_path}">{entry}</a></li>'

        html += "</ul></body></html>"
        return html

    def bind_socket(self):
        for i in range(4):
            try:
                self.sock.bind((self.host, self.port))
                return
            except OSError as e:
                if i == 3:
                    print("Failed to bind socket:", e)
                    exit(1)
                print(f"Bind failed, retrying in 10 seconds... ({i + 1}/3)")
                time.sleep(10)

    def serve_forever(self):
        self.bind_socket()
        self.sock.listen(100)
        print(f"Server running on http://{self.host}:{self.port}")

        with ThreadPoolExecutor(max_workers=1000) as executor:
            while True:
                conn, addr = self.sock.accept()
                print("Connected by", addr)
                executor.submit(self.handle_request, conn, addr)
