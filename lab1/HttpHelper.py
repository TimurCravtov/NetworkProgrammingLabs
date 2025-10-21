from socket import socket


def build_http_response(status_code: int, body: bytes, headers: dict = None) -> bytes:

    status_messages = {
        200: "OK",
        404: "Not Found",
        405: "Method Not Allowed",
    }
    status_message = status_messages.get(status_code, "OK")

    final_headers = {
        "Content-Length": str(len(body)),
        "Content-Type": "text/html",
    }

    if headers:
        final_headers.update(headers)

    headers_text = "".join(f"{key}: {value}\r\n" for key, value in final_headers.items())

    response = f"HTTP/1.1 {status_code} {status_message}\r\n{headers_text}\r\n".encode() + body
    return response

def build_http_request(method: str, path: str, host: str, headers: dict = None, body: bytes = b"") -> bytes:
    final_headers = {"Host": host, "Content-Length": str(len(body)), "Connection": "close"}
    if headers:
        final_headers.update(headers)

    headers_text = "".join(f"{k}: {v}\r\n" for k, v in final_headers.items())
    request_line = f"{method} {path} HTTP/1.1\r\n"
    return (request_line + headers_text + "\r\n").encode() + body

def get_content_type(file_path: str) -> str:

    dot_index = file_path.rfind(".")
    ext = file_path[dot_index:].lower() if dot_index != -1 else ""

    if ext in (".html", ".htm"):
        return "text/html"
    elif ext == ".pdf":
        return "application/pdf"
    elif ext == ".png":
        return "image/png"
    else:
        return "application/octet-stream"

import socket

def _receive_http_common(con: socket.socket):
    """Reads raw HTTP headers and body from socket, returns (first_line, headers, body)."""
    data = b""
    while b"\r\n\r\n" not in data:
        chunk = con.recv(4609)
        if not chunk:
            return None
        data += chunk

    header_part, rest = data.split(b"\r\n\r\n", 1)
    header_lines = header_part.decode(errors="replace").split("\r\n")

    if not header_lines:
        return None

    first_line = header_lines[0]
    headers = {}
    for line in header_lines[1:]:
        if ": " in line:
            k, v = line.split(": ", 1)
            headers[k.strip().lower()] = v.strip()

    # Read body
    body = rest
    content_length = int(headers.get("content-length", "0"))
    while len(body) < content_length:
        chunk = con.recv(4096)
        if not chunk:
            break
        body += chunk

    if content_length > 0:
        body = body[:content_length]
    else:
        body = b""

    return first_line, headers, body


def receive_http_request(s: socket.socket):
    result = _receive_http_common(s)
    if result is None:
        return None
    first_line, headers, body = result

    parts = first_line.split(" ", 2)
    if len(parts) != 3:
        return None
    method, path, version = parts

    return method, path, version, headers, body


def receive_http_response(s: socket.socket):
    result = _receive_http_common(s)
    if result is None:
        return None
    first_line, headers, body = result

    parts = first_line.split(" ", 2)
    if len(parts) != 3:
        return None
    version, status_code, status_text = parts

    return version, status_code, status_text, headers, body


def receive_from_http_socket(con: socket.socket, type="request"):
    if type == "request":
        return receive_http_request(con)
    elif type == "response":
        return receive_http_response(con)
    else:
        raise ValueError("type must be 'request' or 'response'")
