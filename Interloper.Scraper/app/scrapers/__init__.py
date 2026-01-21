from .base import BaseScraper
from .booking_api import BookingApiScraper
from .hotels_api import HotelsApiScraper
from .registry import ScraperRegistry, scraper_registry

__all__ = [
    "BaseScraper",
    "BookingApiScraper",
    "HotelsApiScraper",
    "ScraperRegistry",
    "scraper_registry",
]
