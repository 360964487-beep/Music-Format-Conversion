FROM node:22-bookworm-slim AS assets
WORKDIR /build
COPY scripts/prepare-pwa.mjs scripts/pwa-dependencies.json scripts/
COPY web web
RUN node scripts/prepare-pwa.mjs

FROM python:3.12-slim-bookworm
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends ffmpeg ca-certificates \
    && rm -rf /var/lib/apt/lists/* \
    && pip install --no-cache-dir yt-dlp==2026.10.26
COPY --from=assets /build/web web
COPY scripts/video_service.py scripts/video_service.py
USER 65534:65534
EXPOSE 10000
CMD ["python", "-u", "scripts/video_service.py"]
