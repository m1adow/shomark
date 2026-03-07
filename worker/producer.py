import json
import logging
from confluent_kafka import Producer as KafkaProducer

from config import Config

logger = logging.getLogger(__name__)


class EventProducer:
    """Publishes processing-result events to Kafka."""

    def __init__(self, config: Config) -> None:
        self._producer = KafkaProducer({
            "bootstrap.servers": config.kafka_bootstrap_servers,
        })
        self._topic = config.kafka_completion_topic

    def send_completion(self, message: dict) -> None:
        """Send a processing-complete event."""
        payload = json.dumps(message, ensure_ascii=False).encode("utf-8")
        self._producer.produce(self._topic, value=payload, callback=self._on_delivery)
        self._producer.flush()

    @staticmethod
    def _on_delivery(err, msg) -> None:
        if err:
            logger.error("Completion message delivery failed: %s", err)
        else:
            logger.info("Completion message delivered to %s [%d]", msg.topic(), msg.partition())
