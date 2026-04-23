#!/bin/bash
# Captures an RTSP stream and writes it as rotating MP4 segments.
# All parameters are configurable via environment variables (see below).
# Uses 'exec' so ffmpeg replaces the shell process, keeping PID 1 for
# Kubernetes health checks (pgrep -x ffmpeg).
#
# https://ffmpeg.org/ffmpeg-protocols.html#rtsp
# https://ffmpeg.org/ffmpeg-formats.html#segment_002c-stream_005fsegment_002c-ssegment
#
# Environment variables:
#   FFMPEG_GPU             - enable NVIDIA GPU acceleration (default: false)
#   FFMPEG_RTSP_TRANSPORT  - RTSP transport protocol (default: tcp)
#   FFMPEG_INPUT_URL       - RTSP stream URL (required)
#   FFMPEG_VIDEO_CODEC     - video codec; overrides GPU default if set (default: libx264 / h264_nvenc with GPU)
#   FFMPEG_PRESET          - encoding preset; overrides GPU default if set (default: ultrafast / p1 with GPU)
#   FFMPEG_SEGMENT_TIME    - duration of each segment in seconds (default: 10)
#   FFMPEG_SEGMENT_WRAP    - max segments before overwriting oldest (default: 100)
#   FFMPEG_OUTPUT_PATH     - output path with C-style printf pattern (default: /data/temp/segment_%04d.mp4)
set -euo pipefail

GPU="${FFMPEG_GPU:-false}"

# Set codec/preset defaults based on GPU mode
if [ "${GPU}" = "true" ]; then
  DEFAULT_CODEC="h264_nvenc"
  DEFAULT_PRESET="p1"
  HWACCEL_ARGS=(-hwaccel cuda)
else
  DEFAULT_CODEC="libx264"
  DEFAULT_PRESET="ultrafast"
  HWACCEL_ARGS=()
fi

# Build transport args only for RTSP URLs
INPUT_URL="${FFMPEG_INPUT_URL:?FFMPEG_INPUT_URL is required}"
TRANSPORT_ARGS=()
if echo "${INPUT_URL}" | grep -qi '^rtsp://'; then
  TRANSPORT_ARGS=(-rtsp_transport "${FFMPEG_RTSP_TRANSPORT:-tcp}")
fi

exec ffmpeg \
  "${HWACCEL_ARGS[@]+"${HWACCEL_ARGS[@]}"}" \
  "${TRANSPORT_ARGS[@]+"${TRANSPORT_ARGS[@]}"}" \
  -i "${INPUT_URL}" \
  -c:v "${FFMPEG_VIDEO_CODEC:-${DEFAULT_CODEC}}" \
  -preset "${FFMPEG_PRESET:-${DEFAULT_PRESET}}" \
  -f segment \
  -segment_time "${FFMPEG_SEGMENT_TIME:-10}" \
  -segment_wrap "${FFMPEG_SEGMENT_WRAP:-100}" \
  -reset_timestamps 1 \
  "${FFMPEG_OUTPUT_PATH:-/data/temp/segment_%04d.mp4}"
