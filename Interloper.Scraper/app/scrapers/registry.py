import logging
from typing import Optional

from app.config import settings
from .base import BaseScraper
from .booking_api import BookingApiScraper
from .hotels_api import HotelsApiScraper
from .priceline_api import PricelineApiScraper

logger = logging.getLogger(__name__)


class ScraperRegistry:
    """Registry of available scrapers."""

    def __init__(self):
        self._scrapers: dict[str, BaseScraper] = {}
        self._register_default_scrapers()

    def _register_default_scrapers(self):
        """Register API-based scrapers (requires RapidAPI key)."""
        if settings.rapidapi_key:
            self.register(BookingApiScraper())
            self.register(HotelsApiScraper())
            self.register(PricelineApiScraper())
            logger.info("Registered API-based scrapers (RapidAPI key configured)")
        else:
            logger.warning("RAPIDAPI_KEY not configured - no scrapers available")

    def register(self, scraper: BaseScraper):
        """Register a scraper instance."""
        self._scrapers[scraper.platform_name] = scraper
        logger.info(f"Registered scraper: {scraper.platform_name}")

    def get(self, platform: str) -> Optional[BaseScraper]:
        """Get a scraper by platform name."""
        return self._scrapers.get(platform)

    def get_all(self) -> list[BaseScraper]:
        """Get all registered scrapers."""
        return list(self._scrapers.values())

    def get_platforms(self) -> list[str]:
        """Get list of available platform names."""
        return list(self._scrapers.keys())

    async def close_all(self):
        """Close all scraper HTTP clients."""
        for scraper in self._scrapers.values():
            await scraper.close()


# Global registry instance
scraper_registry = ScraperRegistry()
