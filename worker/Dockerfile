# NVIDIA CUDA 12.1 base — provides libcublas.so.12, libcudnn.so.8 at system level
FROM nvidia/cuda:12.1.1-cudnn8-runtime-ubuntu22.04

ENV DEBIAN_FRONTEND=noninteractive

# Install Python 3.10, FFmpeg and build dependencies
RUN apt-get update && \
    apt-get install -y python3.10 python3-pip ffmpeg libsm6 libxext6 gcc librdkafka-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/* && \
    ln -sf /usr/bin/python3.10 /usr/bin/python && \
    ln -sf /usr/bin/pip3 /usr/bin/pip

# Set the working directory in the container
WORKDIR /app

# Copy the requirements file into the container
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download the faster-whisper base model to speed up first run
RUN python -c "from faster_whisper import WhisperModel; WhisperModel('base', device='cpu', compute_type='int8')"

# We don't COPY the script here because we will mount the folder via docker-compose.
# This allows you to edit the script and see video outputs without rebuilding.
CMD ["python", "main.py"]