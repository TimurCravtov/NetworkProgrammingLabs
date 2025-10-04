import socket
import ssl
import sys
import os
from urllib.parse import urlparse, unquote, quote

from FileHelper import parse_args
from HttpHelper import build_http_request


class HttpClient:
    def __init__(self, host, port=80, https=False):
        self.host = host
        self.port = port
        self.https = https
        self.client_socket = None

    def init_socket(self):

        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)

        if self.https:
            context = ssl.create_default_context()
            self.client_socket = context.wrap_socket(sock, server_hostname=self.host)
        else:
            self.client_socket = sock


    def request(self, url: str, method: str = "GET", body="", headers=None):
        if headers is None:
            headers = {}

        self.init_socket()
        self.client_socket.connect((self.host, self.port))

        if method not in ("GET", "POST", "PUT", "DELETE"):
            raise ValueError("method must be GET, POST, PUT, or DELETE")

        # Build the request
        request = build_http_request(method, url, body, headers)

        self.client_socket.send(request)

        # Receive the response
        data = b""
        while True:
            response = self.client_socket.recv(4096)
            if not response:
                break
            data += response

        self.client_socket.close()

        # Split headers and body safely
        header_bytes, _, body_bytes = data.partition(b"\r\n\r\n")
        header_text = header_bytes.decode(errors="ignore")
        headers_lines = header_text.split("\r\n")[1:]
        headers_dict = {}
        for line in headers_lines:
            if ": " in line:
                key, value = line.split(": ", 1)
                headers_dict[key.lower()] = value

        content_type = headers_dict.get("content-type", None)
        return body_bytes, content_type



def main():

    args = parse_args()

    host, port, filename, download_path, https = (
        args.get("host", "localhost"),
        int(args.get("port", 8080)),
        quote(args.get("fname", "")),
        args.get("dpath", "."),
        args.get("https", False)
    )

    client = HttpClient(host, port, https=https)

    body_bytes, content_type = client.request(filename, "GET")

    print("Content-Type:", content_type)

    if content_type and "html" in content_type:
        print(body_bytes.decode(errors="ignore"))
    elif content_type and ("image" in content_type or "pdf" in content_type):
        filename = unquote(os.path.basename(filename)) or "downloaded_file"
        save_path = os.path.join(download_path, filename)

        os.makedirs(os.path.dirname(save_path), exist_ok=True)

        with open(save_path, "wb") as f:
            f.write(body_bytes)

        print(f"Downloaded to: {save_path}")
    else:
        print("Unknown file type:", content_type)


if __name__ == "__main__":
    main()

# py client.py --fname served/spiderman photo (real).png --dpath tpm
