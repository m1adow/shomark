# Use an official Python runtime as a parent image
FROM python:3.10-slim

# Install FFmpeg and build dependencies for confluent-kafka
RUN apt-get update && \
    apt-get install -y ffmpeg libsm6 libxext6 gcc librdkafka-dev && \
    apt-get clean && \
    rm -rf /var/lib/apt/lists/*

# Set the working directory in the container
WORKDIR /app

# Copy the requirements file into the container
COPY requirements.txt .

# Install Python dependencies
RUN pip install --no-cache-dir -r requirements.txt

# Pre-download the faster-whisper 'small' model to speed up execution
RUN python -c "from faster_whisper import WhisperModel; WhisperModel('base', device='cpu', compute_type='int8')"

# We don't COPY the script here because we will mount the folder via docker-compose.
# This allows you to edit the script and see video outputs without rebuilding.
CMD ["python", "main.py"]