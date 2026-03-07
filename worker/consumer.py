import json
import logging
from confluent_kafka import Consumer, KafkaError

from config import Config
from service import VideoHighlightService
from producer import EventProducer

logger = logging.getLogger(__name__)


class VideoConsumer:
    """Kafka consumer that listens for video-processing tasks."""

    def __init__(self, config: Config, service: VideoHighlightService, producer: EventProducer) -> None:
        self._service = service
        self._producer = producer
        self._topic = config.kafka_topic
        self._consumer = Consumer({
            "bootstrap.servers": config.kafka_bootstrap_servers,
            "group.id": config.kafka_group_id,
            "auto.offset.reset": "earliest",
            "enable.auto.commit": False,
            "max.poll.interval.ms": config.kafka_max_poll_interval_ms,
            "session.timeout.ms": config.kafka_session_timeout_ms,
        })

    def run(self) -> None:
        """Subscribe to the topic and process messages in a loop."""
        self._consumer.subscribe([self._topic])
        logger.info("Subscribed to topic: %s — waiting for messages…", self._topic)

        try:
            while True:
                msg = self._consumer.poll(timeout=1.0)
                if msg is None:
                    continue

                if msg.error():
                    if msg.error().code() == KafkaError._PARTITION_EOF:
                        continue
                    logger.error("Kafka error: %s", msg.error())
                    continue

                self._handle_message(msg)
        except KeyboardInterrupt:
            logger.info("Shutting down consumer…")
        finally:
            self._consumer.close()

    def _handle_message(self, msg) -> None:
        """Parse and process a single Kafka message."""
        try:
            value = json.loads(msg.value().decode("utf-8"))
            logger.info("Received message: %s", json.dumps(value, ensure_ascii=False))
            result = self._service.process(value)
            if result:
                self._producer.send_completion(result)
            self._consumer.commit(msg)
            logger.info("Message committed successfully")
        except json.JSONDecodeError:
            logger.error("Invalid JSON in message: %s", msg.value())
            self._consumer.commit(msg)  # skip malformed messages
        except KeyError as e:
            logger.error("Missing required field in message: %s", e)
            self._consumer.commit(msg)
        except Exception:
            logger.exception("Failed to process message")
            # Don't commit — message will be redelivered
