"""Priceline scraper using RapidAPI."""

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


class PricelineApiScraper(BaseScraper):
    """Scraper that uses the Priceline RapidAPI."""

    platform_name: str = "priceline"
    BASE_URL = "https://priceline-com-provider.p.rapidapi.com"

    def _get_headers(self) -> dict[str, str]:
        """Get headers for RapidAPI requests."""
        return {
            "X-RapidAPI-Key": settings.rapidapi_key,
            "X-RapidAPI-Host": "priceline-com-provider.p.rapidapi.com",
            "Accept": "application/json",
        }

    async def _search_location(self, city: str) -> Optional[dict]:
        """Search for a location ID by city name."""
        # Check cache first
        cached = await cache.get_destination(self.platform_name, city)
        if cached:
            logger.info(f"Cache HIT for Priceline location: {city}")
            return cached

        client = await self.get_client()

        url = f"{self.BASE_URL}/v1/hotels/locations"
        params = {"name": city, "search_type": "ALL"}

        try:
            logger.info(f"Searching Priceline location for: {city}")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            # Extract first city result
            locations = data if isinstance(data, list) else data.get("data", [])
            if locations and len(locations) > 0:
                # Find the first city-type result
                for loc in locations:
                    loc_type = loc.get("type", "").lower()
                    if loc_type in ["city", "neighborhood", "area"]:
                        location_id = loc.get("id") or loc.get("cityId") or loc.get("itemId")
                        city_name = loc.get("cityName") or loc.get("name", city)
                        logger.info(f"Found Priceline location: {location_id} ({city_name})")

                        result = {
                            "location_id": str(location_id),
                            "city_name": city_name,
                            "data": loc,
                        }

                        # Cache the result
                        await cache.set_destination(self.platform_name, city, result)

                        return result

                # If no city found, use first result
                loc = locations[0]
                location_id = loc.get("id") or loc.get("cityId") or loc.get("itemId")
                logger.info(f"Found Priceline location (fallback): {location_id}")

                result = {
                    "location_id": str(location_id),
                    "city_name": loc.get("cityName") or loc.get("name", city),
                    "data": loc,
                }

                await cache.set_destination(self.platform_name, city, result)
                return result

            logger.warning(f"No Priceline location found for: {city}")
            return None

        except Exception as e:
            logger.error(f"Error searching Priceline location: {e}")
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
        """Search for hotels using the Priceline API."""
        results: list[HotelResult] = []

        # First, find the location
        location_info = await self._search_location(city)
        if not location_info:
            logger.warning(f"Could not find Priceline location for {city}")
            return results

        location_id = location_info["location_id"]

        # Rate limiting delay
        await asyncio.sleep(settings.request_delay_seconds)

        client = await self.get_client()

        # Use the search hotels endpoint
        url = f"{self.BASE_URL}/v1/hotels/search"
        params = {
            "location_id": location_id,
            "date_checkin": check_in.strftime("%Y-%m-%d"),
            "date_checkout": check_out.strftime("%Y-%m-%d"),
            "rooms_number": "1",
            "adults_number": str(guests),
            "sort_order": "PRICE",
        }

        try:
            logger.info(f"Searching Priceline hotels in {city} (location: {location_id})")
            response = await client.get(url, params=params)
            response.raise_for_status()
            data = response.json()

            # Log response structure for debugging
            if isinstance(data, dict):
                logger.info(f"Priceline response keys: {list(data.keys())}")

            # Parse hotels from response
            hotels = []
            if isinstance(data, list):
                hotels = data
            elif isinstance(data, dict):
                hotels = data.get("hotels", [])
                if not hotels:
                    hotels = data.get("data", {}).get("hotels", [])
                if not hotels:
                    hotels = data.get("results", [])

            logger.info(f"Priceline API returned {len(hotels)} hotels")

            # Log first hotel structure for debugging
            if hotels and len(hotels) > 0:
                first = hotels[0]
                if isinstance(first, dict):
                    logger.info(f"Priceline first hotel keys: {list(first.keys())}")

            for hotel in hotels:
                try:
                    result = self._parse_hotel(hotel, city)
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
                    logger.warning(f"Error parsing Priceline hotel: {e}")
                    continue

        except httpx.HTTPStatusError as e:
            logger.error(f"HTTP error searching Priceline: {e.response.status_code}")
            logger.error(f"Response body: {e.response.text}")
            if e.response.status_code == 429:
                logger.error("Rate limit exceeded for Priceline")
            elif e.response.status_code == 403:
                logger.error("API key invalid or subscription issue for Priceline")
        except Exception as e:
            logger.error(f"Error searching Priceline: {e}")

        logger.info(f"Returning {len(results)} Priceline hotels after filtering")
        return results

    def _parse_hotel(self, hotel: dict, city: str) -> Optional[HotelResult]:
        """Parse a hotel from the API response."""
        try:
            hotel_id = str(hotel.get("hotelId") or hotel.get("id") or "")
            name = hotel.get("name") or hotel.get("hotelName") or ""

            if not hotel_id or not name:
                return None

            # Extract price
            price = None
            price_data = hotel.get("ratesSummary", {})
            min_price = price_data.get("minPrice") or price_data.get("minRate")
            if min_price:
                price = Decimal(str(min_price))

            # Also try direct price fields
            if not price:
                direct_price = hotel.get("price") or hotel.get("avgNightlyRate")
                if direct_price:
                    price = Decimal(str(direct_price))

            # Extract rating (Priceline uses various scales)
            rating = None
            overall_rating = hotel.get("overallGuestRating") or hotel.get("guestRating")
            if overall_rating:
                # Priceline typically uses 0-10 scale
                rating = self._normalize_rating(float(overall_rating), max_rating=10.0)

            # Also try star rating
            if not rating:
                star_rating = hotel.get("starRating") or hotel.get("stars")
                if star_rating:
                    rating = Decimal(str(star_rating))

            # Extract review count
            review_count = hotel.get("reviewCount") or hotel.get("totalReviews")

            # Build booking URL
            hotel_slug = hotel.get("hotelSlug") or hotel_id
            booking_url = f"https://www.priceline.com/r-cityhotelid/{hotel_id}"

            # Extract address
            location = hotel.get("location", {})
            address = location.get("address", {}).get("line1", "")
            if not address:
                address = hotel.get("address") or ""

            # Extract image
            image_url = None
            images = hotel.get("images", [])
            if images and len(images) > 0:
                image_url = images[0].get("url") or images[0]
            if not image_url:
                thumbnail = hotel.get("thumbnail") or hotel.get("thumbnailUrl")
                if thumbnail:
                    image_url = thumbnail

            # Cancellation info
            cancellation = hotel.get("freeCancel") or hotel.get("freeCancellation")
            cancellation_policy = "Free cancellation" if cancellation else None

            return HotelResult(
                platform=self.platform_name,
                hotel_id=hotel_id,
                name=name,
                price=price,
                currency=hotel.get("currency", "USD"),
                rating=rating,
                review_count=int(review_count) if review_count else None,
                address=address,
                city=city,
                cancellation_policy=cancellation_policy,
                booking_url=booking_url,
                image_url=image_url if image_url else None,
            )

        except Exception as e:
            logger.warning(f"Error parsing Priceline data: {e}")
            return None

    def _has_free_cancellation(self, hotel: dict) -> bool:
        """Check if hotel has free cancellation."""
        return bool(hotel.get("freeCancel") or hotel.get("freeCancellation"))
