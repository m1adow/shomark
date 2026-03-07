import logging

from config import Config
from storage import StorageClient
from transcriber import Transcriber
from llm import LLMClient
from highlight_finder import HighlightFinder
from video_processor import VideoProcessor
from service import VideoHighlightService
from producer import EventProducer
from consumer import VideoConsumer

logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s [%(levelname)s] %(name)s: %(message)s",
)
logger = logging.getLogger(__name__)


def main() -> None:
    """Wire up all components and start the Kafka consumer loop."""
    config = Config()

    # Infrastructure clients
    storage = StorageClient(config)
    llm = LLMClient(config)

    # Domain components
    transcriber = Transcriber(config)
    highlight_finder = HighlightFinder(llm, config)
    video_processor = VideoProcessor()

    # Service (orchestration)
    service = VideoHighlightService(
        storage=storage,
        transcriber=transcriber,
        highlight_finder=highlight_finder,
        video_processor=video_processor,
    )

    # Producer (completion events)
    producer = EventProducer(config)

    # Consumer (Kafka listener)
    consumer = VideoConsumer(config, service, producer)

    logger.info("Worker starting — listening on topic '%s'", config.kafka_topic)
    consumer.run()


if __name__ == "__main__":
    main()