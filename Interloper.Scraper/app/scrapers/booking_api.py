"""Booking.com scraper using RapidAPI."""

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


class BookingApiScraper(BaseScraper):
    """Scraper that uses the Booking.com RapidAPI by Tipsters."""

    platform_name: str = "booking"
    BASE_URL = "https://booking-com.p.rapidapi.com"

    def _get_headers(self) -> dict[str, str]:
        """Get headers for RapidAPI requests."""
        return {
            "X-RapidAPI-Key": settings.rapidapi_key,
            "X-RapidAPI-Host": "booking-com.p.rapidapi.com",
            "Accept": "application/json",
        }

    async def _search_destination(self, city: str) -> Optional[str]:
        """Search for a destination ID by city name."""
        # Check cache first
        cached = await cache.get_destination(self.platform_name, city)
        if cached:
            logger.info(f"Cache HIT for destination: {city}")
            return cached.get("dest_id"), cached.get("dest_type")

        client = await self.get_client()

        url = f"{self.BASE_URL}/v1/hotels/locations"
        params = {"name": city, "locale": "en-gb"}

        try:
            logger.info(f"Searching destination for: {city}")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            if data and isinstance(data, list) and len(data) > 0:
                dest_id = data[0].get("dest_id")
                dest_type = data[0].get("dest_type", "city")
                logger.info(f"Found destination: {dest_id} (type: {dest_type})")

                # Cache the result
                await cache.set_destination(
                    self.platform_name,
                    city,
                    {"dest_id": dest_id, "dest_type": dest_type},
                )

                return dest_id, dest_type

            logger.warning(f"No destination found for: {city}")
            return None, None

        except Exception as e:
            logger.error(f"Error searching destination: {e}")
            return None, None

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
        """Search for hotels using the Booking.com API."""
        results: list[HotelResult] = []

        # First, find the destination ID
        dest_id, dest_type = await self._search_destination(city)
        if not dest_id:
            logger.warning(f"Could not find destination ID for {city}")
            return results

        # Rate limiting delay
        await asyncio.sleep(settings.request_delay_seconds)

        client = await self.get_client()

        url = f"{self.BASE_URL}/v1/hotels/search"
        params = {
            "dest_id": dest_id,
            "dest_type": dest_type or "city",
            "checkin_date": check_in.isoformat(),
            "checkout_date": check_out.isoformat(),
            "adults_number": str(guests),
            "room_number": "1",
            "page_number": "0",
            "units": "metric",
            "locale": "en-gb",
            "filter_by_currency": "USD",
            "order_by": "popularity",
        }

        try:
            logger.info(f"Searching hotels in {city} ({dest_id})")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            hotels = data.get("result", []) if isinstance(data, dict) else []
            logger.info(f"API returned {len(hotels)} hotels")

            for hotel in hotels:
                try:
                    result = self._parse_hotel(hotel, city, check_in, check_out)
                    if result:
                        # Apply filters
                        if max_price and result.price and result.price > max_price:
                            continue
                        if min_rating and result.rating and result.rating < min_rating:
                            continue
                        if free_cancellation and not self._has_free_cancellation(hotel):
                            continue
                        if hotel_name and hotel_name.lower() not in result.name.lower():
                            continue

                        results.append(result)

                except Exception as e:
                    logger.warning(f"Error parsing hotel: {e}")
                    continue

        except httpx.HTTPStatusError as e:
            logger.error(f"HTTP error searching hotels: {e.response.status_code}")
            logger.error(f"Response body: {e.response.text}")
            if e.response.status_code == 429:
                logger.error("Rate limit exceeded")
            elif e.response.status_code == 403:
                logger.error("API key invalid or subscription issue")
        except Exception as e:
            logger.error(f"Error searching hotels: {e}")

        logger.info(f"Returning {len(results)} hotels after filtering")
        return results

    def _parse_hotel(
        self,
        hotel: dict,
        city: str,
        check_in: date,
        check_out: date,
    ) -> Optional[HotelResult]:
        """Parse a hotel from the API response."""
        try:
            hotel_id = str(hotel.get("hotel_id", ""))
            name = hotel.get("hotel_name", "") or hotel.get("name", "")

            if not hotel_id or not name:
                return None

            # Extract price
            price = None
            price_data = hotel.get("min_total_price") or hotel.get("price_breakdown", {}).get("gross_price")
            if price_data:
                price = Decimal(str(price_data))

            # If no price in those fields, try composite_price_breakdown
            if not price:
                composite = hotel.get("composite_price_breakdown", {})
                gross = composite.get("gross_amount_per_night", {}).get("value")
                if gross:
                    price = Decimal(str(gross))

            # Extract rating (Booking uses 0-10 scale)
            rating = None
            review_score = hotel.get("review_score")
            if review_score:
                rating = self._normalize_rating(float(review_score), max_rating=10.0)

            # Extract review count
            review_count = hotel.get("review_nr") or hotel.get("review_count")

            # Build booking URL
            booking_url = hotel.get("url") or f"https://www.booking.com/hotel/{hotel_id}.html"

            # Extract address
            address = hotel.get("address") or hotel.get("address_trans", "")

            # Extract image
            image_url = hotel.get("main_photo_url") or hotel.get("max_photo_url", "")
            if image_url and not image_url.startswith("http"):
                image_url = f"https:{image_url}"

            # Cancellation policy
            cancellation = hotel.get("is_free_cancellable")
            cancellation_policy = "Free cancellation" if cancellation else None

            return HotelResult(
                platform=self.platform_name,
                hotel_id=hotel_id,
                name=name,
                price=price,
                currency=hotel.get("currency_code", "USD"),
                rating=rating,
                review_count=int(review_count) if review_count else None,
                address=address,
                city=city,
                cancellation_policy=cancellation_policy,
                booking_url=booking_url,
                image_url=image_url if image_url else None,
            )

        except Exception as e:
            logger.warning(f"Error parsing hotel data: {e}")
            return None

    def _has_free_cancellation(self, hotel: dict) -> bool:
        """Check if hotel has free cancellation."""
        return bool(hotel.get("is_free_cancellable"))
