import json
import logging
import queue
from concurrent.futures import ThreadPoolExecutor

from confluent_kafka import Consumer, KafkaError

from config import Config
from service import VideoHighlightService
from producer import EventProducer

logger = logging.getLogger(__name__)


class VideoConsumer:
    """Kafka consumer that listens for video-processing tasks.

    Messages are dispatched to a thread pool so up to ``worker_concurrency``
    jobs run in parallel (vertical scaling).  The ``confluent_kafka.Consumer``
    is only ever touched from the main thread (poll + commit), satisfying its
    thread-safety requirements.

    Horizontal scaling is achieved by running multiple worker containers in the
    same Kafka consumer group — Kafka distributes topic partitions between them
    automatically, no extra configuration required.
    """

    def __init__(self, config: Config, service: VideoHighlightService, producer: EventProducer) -> None:
        self._service = service
        self._producer = producer
        self._topic = config.kafka_topic
        self._max_workers = config.worker_concurrency
        self._consumer = Consumer({
            "bootstrap.servers": config.kafka_bootstrap_servers,
            "group.id": config.kafka_group_id,
            "auto.offset.reset": "earliest",
            "enable.auto.commit": False,
            "max.poll.interval.ms": config.kafka_max_poll_interval_ms,
            "session.timeout.ms": config.kafka_session_timeout_ms,
        })

    def run(self) -> None:
        """Subscribe to the topic and process messages concurrently."""
        self._consumer.subscribe([self._topic])
        logger.info(
            "Subscribed to topic: %s — waiting for messages… (concurrency=%d)",
            self._topic, self._max_workers,
        )

        done_queue: queue.SimpleQueue = queue.SimpleQueue()
        active = 0
        paused = False

        try:
            with ThreadPoolExecutor(max_workers=self._max_workers) as pool:
                while True:
                    # Drain all completed results (non-blocking).
                    while True:
                        try:
                            original_msg, success = done_queue.get_nowait()
                        except queue.Empty:
                            break
                        active -= 1
                        if success:
                            self._consumer.commit(original_msg)
                            logger.info("Message committed successfully")
                        else:
                            # Do not commit — Kafka will redeliver the message.
                            logger.warning("Message NOT committed; will be redelivered")

                    # Resume consumption when a worker slot has freed up.
                    if paused and active < self._max_workers:
                        self._consumer.resume(self._consumer.assignment())
                        paused = False

                    # Always poll to maintain the Kafka heartbeat, even when at capacity.
                    msg = self._consumer.poll(timeout=1.0)

                    if msg is None:
                        continue
                    if msg.error():
                        if msg.error().code() != KafkaError._PARTITION_EOF:
                            logger.error("Kafka error: %s", msg.error())
                        continue

                    active += 1
                    pool.submit(self._process_task, msg, done_queue)

                    # Pause partition when at capacity — poll() still runs for heartbeat,
                    # but Kafka won't deliver new messages until we resume.
                    if active >= self._max_workers:
                        self._consumer.pause(self._consumer.assignment())
                        paused = True

        except KeyboardInterrupt:
            logger.info("Shutting down consumer…")
        finally:
            self._consumer.close()

    def _process_task(self, msg, done_queue: queue.SimpleQueue) -> None:
        """Parse and process a single Kafka message in a worker thread.

        Posts ``(msg, success)`` to *done_queue* when finished so the main
        thread can commit (or skip committing) the offset.
        """
        try:
            value = json.loads(msg.value().decode("utf-8"))
            logger.info("Received message: %s", json.dumps(value, ensure_ascii=False))
            result = self._service.process(value)
            if result:
                self._producer.send_completion(result)
            done_queue.put((msg, True))
        except json.JSONDecodeError:
            logger.error("Invalid JSON in message: %s", msg.value())
            done_queue.put((msg, True))   # skip malformed — commit to advance offset
        except KeyError as e:
            logger.error("Missing required field in message: %s", e)
            done_queue.put((msg, True))   # skip malformed — commit to advance offset
        except Exception:
            logger.exception("Failed to process message")
            done_queue.put((msg, False))  # do not commit — triggers redelivery

