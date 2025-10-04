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

