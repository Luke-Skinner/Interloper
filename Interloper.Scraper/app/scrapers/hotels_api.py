"""Hotels.com scraper using RapidAPI."""

import asyncio
import logging
from datetime import date
from decimal import Decimal
from typing import Optional

import httpx

from app.cache import cache
from app.config import settings
from app.models import HotelResult
from app.scrapers.base import BaseScraper

logger = logging.getLogger(__name__)


class HotelsApiScraper(BaseScraper):
    """Scraper that uses the Hotels.com Provider RapidAPI by Tipsters."""

    platform_name: str = "hotels_com"
    BASE_URL = "https://hotels-com-provider.p.rapidapi.com"

    def _get_headers(self) -> dict[str, str]:
        """Get headers for RapidAPI requests."""
        return {
            "X-RapidAPI-Key": settings.rapidapi_key,
            "X-RapidAPI-Host": "hotels-com-provider.p.rapidapi.com",
            "Accept": "application/json",
        }

    async def _search_destination(self, city: str) -> Optional[dict]:
        """Search for a destination by city name."""
        # Check cache first
        cached = await cache.get_destination(self.platform_name, city)
        if cached:
            logger.info(f"Cache HIT for Hotels.com destination: {city}")
            return cached

        client = await self.get_client()

        url = f"{self.BASE_URL}/v2/regions"
        params = {
            "query": city,
            "domain": "US",
            "locale": "en_US",
        }

        try:
            logger.info(f"Searching Hotels.com destination for: {city}")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            # Extract first region/destination
            regions = data.get("data", [])
            if regions and len(regions) > 0:
                region = regions[0]
                region_id = region.get("gaiaId") or region.get("regionId")
                logger.info(f"Found Hotels.com region: {region_id}")

                result = {"region_id": region_id, "data": region}

                # Cache the result
                await cache.set_destination(self.platform_name, city, result)

                return result

            logger.warning(f"No Hotels.com destination found for: {city}")
            return None

        except Exception as e:
            logger.error(f"Error searching Hotels.com destination: {e}")
            return None

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
        """Search for hotels using the Hotels.com API."""
        results: list[HotelResult] = []

        # First, find the region/destination
        dest_info = await self._search_destination(city)
        if not dest_info:
            logger.warning(f"Could not find Hotels.com destination for {city}")
            return results

        region_id = dest_info["region_id"]

        # Rate limiting delay
        await asyncio.sleep(settings.request_delay_seconds)

        client = await self.get_client()

        # Use v3 endpoint for better data
        url = f"{self.BASE_URL}/v3/hotels/search"
        params = {
            "region_id": region_id,
            "checkin_date": check_in.isoformat(),
            "checkout_date": check_out.isoformat(),
            "adults_number": str(guests),
            "domain": "US",
            "locale": "en_US",
            "sort_order": "REVIEW",
            "page_number": "1",
        }

        try:
            logger.info(f"Searching Hotels.com v3 in {city} (region: {region_id})")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            # Log response structure for debugging
            if isinstance(data, dict):
                logger.info(f"Hotels.com v3 response keys: {list(data.keys())}")
                # Check nested data structure
                if "data" in data:
                    inner_data = data.get("data", {})
                    if isinstance(inner_data, dict):
                        logger.info(f"Hotels.com v3 data keys: {list(inner_data.keys())}")

            # Parse hotels from response - v3 may nest under 'data'
            properties = []
            if "data" in data:
                inner = data.get("data", {})
                if isinstance(inner, dict):
                    properties = inner.get("properties", [])
                    if not properties:
                        properties = inner.get("hotels", [])
                    if not properties:
                        properties = inner.get("propertySearch", {}).get("properties", [])
            if not properties:
                properties = data.get("properties", [])
            if not properties:
                properties = data.get("hotels", [])
            if not properties:
                properties = data.get("results", [])

            logger.info(f"Hotels.com API returned {len(properties)} hotels")

            # Log first property structure for debugging
            if properties and len(properties) > 0:
                first = properties[0]
                if isinstance(first, dict):
                    logger.info(f"Hotels.com first property keys: {list(first.keys())}")

            for prop in properties:
                try:
                    result = self._parse_hotel(prop, city)
                    if result:
                        # Apply filters
                        if max_price and result.price and result.price > max_price:
                            continue
                        if min_rating and result.rating and result.rating < min_rating:
                            continue
                        if free_cancellation and not self._has_free_cancellation(prop):
                            continue
                        if hotel_name and hotel_name.lower() not in result.name.lower():
                            continue

                        results.append(result)

                except Exception as e:
                    logger.warning(f"Error parsing Hotels.com hotel: {e}")
                    continue

        except httpx.HTTPStatusError as e:
            logger.error(f"HTTP error searching Hotels.com: {e.response.status_code}")
            logger.error(f"Response body: {e.response.text}")
            if e.response.status_code == 429:
                logger.error("Rate limit exceeded for Hotels.com")
            elif e.response.status_code == 403:
                logger.error("API key invalid or subscription issue for Hotels.com")
        except Exception as e:
            logger.error(f"Error searching Hotels.com: {e}")

        logger.info(f"Returning {len(results)} Hotels.com hotels after filtering")
        return results

    def _parse_hotel(self, prop: dict, city: str) -> Optional[HotelResult]:
        """Parse a hotel/property from the v3 API response."""
        try:
            hotel_id = str(prop.get("id", ""))
            name = prop.get("name", "")

            if not hotel_id or not name:
                return None

            # Extract price from v3 structure
            price = None
            price_info = prop.get("price", {})
            # v3 structure: price.lead.amount or price.displayMessages
            lead = price_info.get("lead", {})
            if isinstance(lead, dict):
                lead_amount = lead.get("amount")
                if lead_amount:
                    price = Decimal(str(lead_amount))

            # Also try strikeOut or formatted price
            if not price:
                strike = price_info.get("strikeOut", {})
                if isinstance(strike, dict):
                    strike_amount = strike.get("amount")
                    if strike_amount:
                        price = Decimal(str(strike_amount))

            # Extract rating from guestRating (v3 structure)
            rating = None
            guest_rating = prop.get("guestRating", {})
            if isinstance(guest_rating, dict):
                score = guest_rating.get("rating")
                if score:
                    # Hotels.com uses 0-10 scale
                    rating = self._normalize_rating(float(score), max_rating=10.0)

            # Extract review count
            review_count = None
            if isinstance(guest_rating, dict):
                review_count = guest_rating.get("totalCount")

            # Build booking URL from link or construct it
            link_info = prop.get("link", {})
            if isinstance(link_info, dict):
                booking_url = link_info.get("uri", f"https://www.hotels.com/ho{hotel_id}")
                if booking_url and not booking_url.startswith("http"):
                    booking_url = f"https://www.hotels.com{booking_url}"
            else:
                booking_url = f"https://www.hotels.com/ho{hotel_id}"

            # Extract image from mediaSection
            image_url = None
            media_section = prop.get("mediaSection", {})
            if isinstance(media_section, dict):
                gallery = media_section.get("gallery", {})
                if isinstance(gallery, dict):
                    images = gallery.get("media", [])
                    if images and len(images) > 0:
                        image_url = images[0].get("url", "")

            return HotelResult(
                platform=self.platform_name,
                hotel_id=hotel_id,
                name=name,
                price=price,
                currency="USD",
                rating=rating,
                review_count=int(review_count) if review_count else None,
                address="",  # v3 doesn't include address in search results
                city=city,
                booking_url=booking_url,
                image_url=image_url if image_url else None,
            )

        except Exception as e:
            logger.warning(f"Error parsing Hotels.com data: {e}")
            logger.debug(f"Property data: {prop}")
            return None

    def _has_free_cancellation(self, prop: dict) -> bool:
        """Check if property has free cancellation."""
        # Check various possible locations for cancellation info
        price_info = prop.get("price", {})
        options = price_info.get("options", [])
        for opt in options:
            if opt.get("freeCancel"):
                return True

        # Also check property-level flag
        return bool(prop.get("freeCancellation"))
