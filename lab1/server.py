import os
import sys

from HttpServer import HtmlServer
from FileHelper import parse_args

if __name__ == "__main__":

    args = parse_args()

    host = args.get('host', "0.0.0.0")
    port = int(args.get('port', 8080))
    dir = args.get("dir", "served/")

    server = HtmlServer(port=port, host=host, served_directory=dir)
    server.serve_forever()

