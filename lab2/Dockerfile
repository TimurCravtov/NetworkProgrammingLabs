FROM python:3.11-slim

WORKDIR /app

COPY FileHelper.py .
COPY HitCounter.py .
COPY server.py .
COPY HttpHelper.py .
COPY HttpServer.py .
COPY Filter.py .

EXPOSE 8080
CMD ["python", "server.py"]
