import asyncio
import logging
from abc import ABC, abstractmethod
from datetime import date
from decimal import Decimal
from typing import Optional

import httpx
from fake_useragent import UserAgent
from tenacity import retry, stop_after_attempt, wait_exponential

from app.config import settings
from app.models import HotelResult

logger = logging.getLogger(__name__)


class BaseScraper(ABC):
    """Base class for all hotel scrapers."""

    platform_name: str = "unknown"

    def __init__(self):
        self.ua = UserAgent()
        self._client: Optional[httpx.AsyncClient] = None

    async def get_client(self) -> httpx.AsyncClient:
        """Get or create an HTTP client."""
        if self._client is None or self._client.is_closed:
            self._client = httpx.AsyncClient(
                timeout=httpx.Timeout(settings.request_timeout_seconds),
                follow_redirects=True,
                headers=self._get_headers(),
            )
        return self._client

    async def close(self):
        """Close the HTTP client."""
        if self._client and not self._client.is_closed:
            await self._client.aclose()
            self._client = None

    def _get_headers(self) -> dict[str, str]:
        """Get request headers with random user agent."""
        return {
            "User-Agent": self.ua.random,
            "Accept": "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8",
            "Accept-Language": "en-US,en;q=0.5",
            "Accept-Encoding": "gzip, deflate",
            "Connection": "keep-alive",
        }

    @retry(
        stop=stop_after_attempt(3),
        wait=wait_exponential(multiplier=1, min=2, max=10),
    )
    async def _fetch_page(self, url: str) -> str:
        """Fetch a page with retry logic."""
        client = await self.get_client()

        # Rate limiting delay
        await asyncio.sleep(settings.request_delay_seconds)

        logger.debug(f"Fetching: {url}")
        response = await client.get(url)
        response.raise_for_status()
        return response.text

    @abstractmethod
    async def search(
        self,
        city: str,
        check_in: date,
        check_out: date,
        guests: int = 2,
        hotel_name: Optional[str] = None,
        max_price: Optional[Decimal] = None,
        min_rating: Optional[Decimal] = None,
        free_cancellation: bool = False,
    ) -> list[HotelResult]:
        """
        Search for hotels matching the criteria.

        Args:
            city: City to search in
            check_in: Check-in date
            check_out: Check-out date
            guests: Number of guests
            hotel_name: Specific hotel to search for (optional)
            max_price: Maximum price per night (optional)
            min_rating: Minimum rating (optional)
            free_cancellation: Only show free cancellation (optional)

        Returns:
            List of HotelResult objects
        """
        pass

    def _normalize_rating(
        self, rating: Optional[float], max_rating: float = 10.0
    ) -> Optional[Decimal]:
        """Normalize rating to 0-5 scale."""
        if rating is None:
            return None
        normalized = (rating / max_rating) * 5.0
        return Decimal(str(round(normalized, 1)))
